using Systems.Grid.GridGenerationGroup.Utils;
using Systems.Utils.Jobs.HashMapUtilityJobs;
using Components.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems.Grid.GridGenerationGroup
{
    [UpdateInGroup(typeof(GridGenerationSystemGroup), OrderFirst = true)]
    public class GridExpansionPhaseSystem : SystemBase
    {
        private EndInitializationEntityCommandBufferSystem _ecbSystem;
        private EntityQuery _expandingTilesQuery;
        private NativeArray<float3> _hexTileOffsets;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();

            _hexTileOffsets =
                new NativeArray<float3>(6, Allocator.Persistent)
                {
                    [HexDirection.Top] = new float3(0f, 0f, -2.1f),
                    [HexDirection.TopRight] = new float3(-1.81865f, 0f, -1.05f),
                    [HexDirection.BottomRight] = new float3(-1.81865f, 0f, 1.05f),
                    [HexDirection.Bottom] = new float3(0f, 0f, 2.1f),
                    [HexDirection.BottomLeft] = new float3(1.81865f, 0f, 1.05f),
                    [HexDirection.TopLeft] = new float3(1.81865f, 0f, -1.05f)
                };

            _expandingTilesQuery = GetEntityQuery(
                    ComponentType.ReadWrite<TileComponent>(),
                    ComponentType.ReadWrite<GridGenerationComponent>()
                );
            _expandingTilesQuery.SetSharedComponentFilter(
                    new GridGenerationComponent(GridGenerationPhase.Expansion)
                );
        }

        protected override void OnDestroy() { _hexTileOffsets.Dispose(); }

        protected override void OnUpdate()
        {
            if (_expandingTilesQuery.IsEmpty) return;

            #region ComputeMaxCount

            var expandingTileCount = _expandingTilesQuery.CalculateEntityCount();
            var maxTileCount = expandingTileCount * 6;

            #endregion
            #region InstantiateContainers

            var adjTileLinksMap = new NativeMultiHashMap<float3, TileLink>(
                    maxTileCount,
                    Allocator.TempJob
                );

            var tileEntityArray = _expandingTilesQuery.ToEntityArray(Allocator.TempJob);
            var tileComponentArray =
                _expandingTilesQuery.ToComponentDataArray<TileComponent>(Allocator.TempJob);

            #endregion
            #region InstantiateAdjTiles

            var computeAdjTilesJob =
                new ComputeAdjacentTilesJob
                {
                    TileEntityArray = tileEntityArray,
                    TileComponentArray = tileComponentArray,
                    HexTileOffsets = _hexTileOffsets,
                    MapWriter = adjTileLinksMap.AsParallelWriter(),
                    EcbWriter = _ecbSystem.CreateCommandBuffer()
                                          .AsParallelWriter()
                }.Schedule(expandingTileCount, 1);
            Dependency = JobHandle.CombineDependencies(Dependency, computeAdjTilesJob);

            var uniqueKeys = new NativeList<float3>(maxTileCount, Allocator.TempJob);
            var getUniqueMultHMapKeysJob = new GetUniqueMultHMapKeysJob<float3, TileLink>
                                           {
                                               MultiHashMap = adjTileLinksMap,
                                               Keys = uniqueKeys
                                           }.Schedule(computeAdjTilesJob);

            var instantiateAdjTilesJob =
                new InstantiateAdjacentTilesJob
                {
                    AdjTileLinksMap = adjTileLinksMap,
                    AdjTileLinksKeys = uniqueKeys,
                    EcbWriter = _ecbSystem.CreateCommandBuffer()
                                          .AsParallelWriter()
                }.Schedule(uniqueKeys, 1, getUniqueMultHMapKeysJob);

            Dependency = JobHandle.CombineDependencies(Dependency, instantiateAdjTilesJob);

            #endregion
            #region CleanUp

            adjTileLinksMap.Dispose(instantiateAdjTilesJob);
            uniqueKeys.Dispose(instantiateAdjTilesJob);

            #endregion

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct ComputeAdjacentTilesJob : IJobParallelFor
        {
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<TileComponent> TileComponentArray;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<Entity> TileEntityArray;
            [ReadOnly]
            public NativeArray<float3> HexTileOffsets;

            [WriteOnly]
            public NativeMultiHashMap<float3, TileLink>.ParallelWriter MapWriter;
            [WriteOnly]
            public EntityCommandBuffer.ParallelWriter EcbWriter;

            public void Execute(int i)
            {
                var tile = TileEntityArray[i];
                var tileComponent = TileComponentArray[i];

                for (var j = 0; j < 6; ++j)
                {
                    if (tileComponent.AdjacentTiles[j] != Entity.Null) continue;

                    var gridPos = tileComponent.Position + HexTileOffsets[j];
                    MapWriter.Add(gridPos, new TileLink {Tile = tile, Index = j});
                }

                EcbWriter.SetSharedComponent(
                        i,
                        tile,
                        new GridGenerationComponent(GridGenerationPhase.End)
                    );
            }
        }

        [BurstCompile]
        private struct InstantiateAdjacentTilesJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<float3> AdjTileLinksKeys;
            [ReadOnly]
            public NativeMultiHashMap<float3, TileLink> AdjTileLinksMap;

            [WriteOnly]
            public EntityCommandBuffer.ParallelWriter EcbWriter;

            public void Execute(int i)
            {
                var tileKey = AdjTileLinksKeys[i];
                var tileLinksEnumerator = AdjTileLinksMap.GetValuesForKey(tileKey);
                tileLinksEnumerator.MoveNext();

                var curTileLink = tileLinksEnumerator.Current;
                var tile = EcbWriter.Instantiate(i, curTileLink.Tile);

                var offset = new float3(0f, 0.3f, 0f);
                var tileTranslation = new Translation {Value = tileKey + offset};

                var tileBuffer = TileBuffer.Empty;

                do
                {
                    curTileLink = tileLinksEnumerator.Current;
                    tileBuffer[curTileLink.Index] = curTileLink.Tile;
                } while (tileLinksEnumerator.MoveNext());

                var tileCmpnt = new TileComponent
                                {
                                    Position = tileKey, State = 0, AdjacentTiles = tileBuffer
                                };

                EcbWriter.SetComponent(i, tile, tileCmpnt);
                EcbWriter.SetComponent(i, tile, tileTranslation);
                EcbWriter.SetSharedComponent(
                        i,
                        tile,
                        new GridGenerationComponent(GridGenerationPhase.Linking)
                    );
            }
        }
    }
}