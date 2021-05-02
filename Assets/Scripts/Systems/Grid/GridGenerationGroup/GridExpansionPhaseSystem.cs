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
    public class GridExpansionPhaseSystem : GridGenerationSystemBase
    {
        private NativeArray<GridPosition> _hexTileOffsets;

        protected override void OnCreate()
        {
            base.OnCreate();

            TilesQuery.SetSharedComponentFilter(
                    new GridGenerationComponent(GridGenerationPhase.Expansion)
                );

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
        }

        protected override void OnDestroy() { _hexTileOffsets.Dispose(); }

        protected override void OnUpdate()
        {
            if (TilesQuery.IsEmpty) return;

            var tilesArray = TilesQuery.ToEntityArray(Allocator.Temp);
            var commandBuffer = EcbSystem.CreateCommandBuffer();
            for (var i = 0; i < tilesArray.Length; ++i)
                commandBuffer.AddSharedComponent(
                        tilesArray[i],
                        new GridGenerationComponent(GridGenerationPhase.End)
                    );
            tilesArray.Dispose();

            var maxTileLinksCount = TilesQuery.CalculateEntityCount() * 6;
            var adjTileLinksMap = new NativeMultiHashMap<GridPosition, TileLink>(
                    maxTileLinksCount,
                    Allocator.TempJob
                );

            Dependency = new ComputeAdjacentTilesJob
                         {
                             EntityTypeHandle = GetEntityTypeHandle(),
                             TileComponentTypeHandle =
                                 GetComponentTypeHandle<TileComponent>(true),
                             HexTileOffsets = _hexTileOffsets,
                             MapWriter = adjTileLinksMap.AsParallelWriter()
                         }.Schedule(TilesQuery);

            var uniqueKeys = new NativeList<GridPosition>(
                    maxTileLinksCount,
                    Allocator.TempJob
                );
            Dependency = new GetUniqueMultHMapKeysJob<GridPosition, TileLink>
                         {
                             MultiHashMap = adjTileLinksMap,
                             Keys = uniqueKeys
                         }.Schedule(Dependency);

            Dependency = new InstantiateAdjacentTilesJob
                         {
                             AdjTileLinksMap = adjTileLinksMap,
                             AdjTileLinksKeys = uniqueKeys,
                             EcbWriter = EcbSystem.CreateCommandBuffer()
                                                  .AsParallelWriter()
                         }.Schedule(uniqueKeys, 1, Dependency);

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
        private struct InstantiateAdjacentTilesJob : IJobParallelForDefer
        {
            [ReadOnly]
            public NativeList<GridPosition> AdjTileLinksKeys;
            [ReadOnly]
            public NativeMultiHashMap<GridPosition, TileLink> AdjTileLinksMap;

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
                    tileBuffer[(curTileLink.Index + 3) % 6] = curTileLink.Tile;
                } while (tileLinksEnumerator.MoveNext());

                var tileCmpnt = new TileComponent
                                {
                                    Position = tileKey, State = 0,
                                    AdjacentTiles = tileBuffer
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