using Systems.Grid.GridGeneration.Utils;
using Systems.Utils.Jobs.HashMapUtilityJobs;
using Components.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems.Grid.GridGeneration
{
    public class GridExpansionSystem : GridGenerationSystemBase
    {
        private EntityQuery _expandingTilesQuery;
        private NativeArray<GridPosition> _hexTileOffsets;
        private int _ite;

        protected override void OnCreate()
        {
            base.OnCreate();

            _expandingTilesQuery = GetEntityQuery(TilesBaseQuery);
            _expandingTilesQuery.SetSharedComponentFilter(GridGenerationComponent.ExpansionPhase);

            _hexTileOffsets =
                new NativeArray<GridPosition>(6, Allocator.Persistent)
                {
                    [HexDirection.Top] = new float3(0f, 0f, -2.1f),
                    [HexDirection.TopRight] = new float3(-1.81865f, 0f, -1.05f),
                    [HexDirection.BottomRight] = new float3(-1.81865f, 0f, 1.05f),
                    [HexDirection.Bottom] = new float3(0f, 0f, 2.1f),
                    [HexDirection.BottomLeft] = new float3(1.81865f, 0f, 1.05f),
                    [HexDirection.TopLeft] = new float3(1.81865f, 0f, -1.05f)
                };

            _ite = 0;
        }

        protected override void OnDestroy() { _hexTileOffsets.Dispose(); }

        protected override void OnUpdate()
        {
            if (_expandingTilesQuery.IsEmpty) return;

            var tilesArray = _expandingTilesQuery.ToEntityArray(Allocator.Temp);
            var commandBuffer = EcbSystem.CreateCommandBuffer();
            for (var i = 0; i < tilesArray.Length; ++i)
                commandBuffer.AddSharedComponent(
                        tilesArray[i],
                        _ite < 4
                            ? GridGenerationComponent.InnerNodeLinkingPhase
                            : GridGenerationComponent.ReadyPhase
                    );
            tilesArray.Dispose();

            var maxTileLinksCount = _expandingTilesQuery.CalculateEntityCount() * 6;
            var adjTileLinksMap = new NativeMultiHashMap<GridPosition, TileLink>(
                    maxTileLinksCount,
                    Allocator.TempJob
                );

            Dependency =
                new ComputeAdjacentTilesJob
                {
                    EntityTypeHandle = GetEntityTypeHandle(),
                    TileComponentTypeHandle = GetComponentTypeHandle<TileComponent>(true),
                    HexTileOffsets = _hexTileOffsets,
                    MapWriter = adjTileLinksMap.AsParallelWriter()
                }.Schedule(_expandingTilesQuery, Dependency);

            var uniqueKeys = new NativeList<GridPosition>(maxTileLinksCount, Allocator.TempJob);
            Dependency = new GetUniqueMultHMapKeysJob<GridPosition, TileLink>
                         {
                             MultiHashMap = adjTileLinksMap,
                             Keys = uniqueKeys
                         }.Schedule(Dependency);

            Dependency = new InstantiateAdjacentTilesJob
                         {
                             AdjTileLinksKeys = uniqueKeys,
                             AdjTileLinksMap = adjTileLinksMap,
                             EcbWriter = EcbSystem.CreateCommandBuffer()
                         }.Schedule(Dependency);

            adjTileLinksMap.Dispose(Dependency);
            uniqueKeys.Dispose(Dependency);

            EcbSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct ComputeAdjacentTilesJob : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<TileComponent> TileComponentTypeHandle;
            [ReadOnly]
            public EntityTypeHandle EntityTypeHandle;
            [ReadOnly]
            public NativeArray<GridPosition> HexTileOffsets;

            [WriteOnly]
            public NativeMultiHashMap<GridPosition, TileLink>.ParallelWriter MapWriter;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var tileComponentArray = chunk.GetNativeArray(TileComponentTypeHandle);
                var tileEntityArray = chunk.GetNativeArray(EntityTypeHandle);

                for (var i = 0; i < chunk.Count; ++i)
                {
                    var tile = tileEntityArray[i];
                    var tileComponent = tileComponentArray[i];

                    for (var j = 0; j < 6; ++j)
                    {
                        if (tileComponent.AdjacentTiles[j] != Entity.Null) continue;

                        var gridPos = tileComponent.Position + HexTileOffsets[j];
                        MapWriter.Add(gridPos, new TileLink {Tile = tile, Index = j});
                    }
                }
            }
        }

        //[BurstCompile]
        private struct InstantiateAdjacentTilesJob : IJob
        {
            [ReadOnly]
            public NativeList<GridPosition> AdjTileLinksKeys;
            [ReadOnly]
            public NativeMultiHashMap<GridPosition, TileLink> AdjTileLinksMap;

            [WriteOnly]
            public EntityCommandBuffer EcbWriter;

            public void Execute()
            {
                for (var i = 0; i < AdjTileLinksKeys.Length; ++i)
                {
                    var tileKey = AdjTileLinksKeys[i];
                    var tileLinksEnumerator = AdjTileLinksMap.GetValuesForKey(tileKey);
                    tileLinksEnumerator.MoveNext();

                    var curTileLink = tileLinksEnumerator.Current;
                    var tile = EcbWriter.Instantiate(curTileLink.Tile);

                    var offset = new float3(0f, 0.3f, 0f);
                    var tileTranslation = new Translation {Value = tileKey + offset};

                    var tileBuffer = TileBuffer.Empty;

                    do
                    {
                        curTileLink = tileLinksEnumerator.Current;
                        tileBuffer[(curTileLink.Index + 3) % 6] = curTileLink.Tile;
                    } while (tileLinksEnumerator.MoveNext());

                    var tileCmpnt = new TileComponent
                                    {
                                        Position = tileKey, State = 0, AdjacentTiles = tileBuffer
                                    };
                    EcbWriter.SetComponent(tile, tileCmpnt);
                    EcbWriter.SetComponent(tile, tileTranslation);
                    EcbWriter.SetSharedComponent(
                            tile,
                            GridGenerationComponent.OuterNodeLinkingPhase
                        );
                }
            }
        }
    }
}