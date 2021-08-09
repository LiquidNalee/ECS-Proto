using Systems.Grid.GridGeneration.Utils;
using Systems.Utils.Jobs.HashMapUtilityJobs;
using Components.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems.Grid.GridGeneration {
    public class GridExpansionSystem : GridGenerationSystemBase {
        private EntityQuery _expandingTilesQuery;
        private NativeArray<GridPosition> _hexTileOffsets;
        private int _ite;

        protected override void OnCreate()
        {
            base.OnCreate();

            _expandingTilesQuery = GetEntityQuery(GridGenerationRequiredComponents);
            _expandingTilesQuery.SetSharedComponentFilter(GridGenerationComponent.ExpansionPhase);

            _hexTileOffsets =
                new NativeArray<GridPosition>(6, Allocator.Persistent) {
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

            NativeArray<Entity> tilesArray = _expandingTilesQuery.ToEntityArray(Allocator.Temp);
            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            foreach (Entity tile in tilesArray)
                ecb.AddSharedComponent(
                        tile,
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
                new ComputeAdjacentTilesJob {
                                                entityTypeHandle = GetEntityTypeHandle(),
                                                tileComponentTypeHandle =
                                                    GetComponentTypeHandle<TileComponent>(true),
                                                hexTileOffsets = _hexTileOffsets,
                                                mapWriter = adjTileLinksMap.AsParallelWriter()
                                            }.Schedule(_expandingTilesQuery, Dependency);

            var uniqueKeys = new NativeList<GridPosition>(maxTileLinksCount, Allocator.TempJob);
            Dependency =
                new GetUniqueMultHMapKeysJob<GridPosition, TileLink> {
                    multiHashMap = adjTileLinksMap, keys = uniqueKeys
                }.Schedule(Dependency);

            Dependency =
                new InstantiateAdjacentTilesJob {
                                                    adjTileLinksKeys = uniqueKeys,
                                                    adjTileLinksMap = adjTileLinksMap,
                                                    ecbWriter = ecbSystem.CreateCommandBuffer()
                                                }.Schedule(Dependency);

            adjTileLinksMap.Dispose(Dependency);
            uniqueKeys.Dispose(Dependency);

            ecbSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct ComputeAdjacentTilesJob : IJobChunk {
            [ReadOnly]
            public ComponentTypeHandle<TileComponent> tileComponentTypeHandle;
            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;
            [ReadOnly]
            public NativeArray<GridPosition> hexTileOffsets;

            [WriteOnly]
            public NativeMultiHashMap<GridPosition, TileLink>.ParallelWriter mapWriter;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                NativeArray<TileComponent> tileComponentArray =
                    chunk.GetNativeArray(tileComponentTypeHandle);
                NativeArray<Entity> tileEntityArray = chunk.GetNativeArray(entityTypeHandle);

                for (var i = 0; i < chunk.Count; ++i) {
                    Entity tile = tileEntityArray[i];
                    TileComponent tileComponent = tileComponentArray[i];

                    for (var j = 0; j < 6; ++j) {
                        if (tileComponent.AdjacentTiles[j] != Entity.Null) continue;

                        float3 gridPos = tileComponent.Position + hexTileOffsets[j];
                        mapWriter.Add(gridPos, new TileLink {tile = tile, index = j});
                    }
                }
            }
        }

        //[BurstCompile]
        private struct InstantiateAdjacentTilesJob : IJob {
            [ReadOnly]
            public NativeList<GridPosition> adjTileLinksKeys;
            [ReadOnly]
            public NativeMultiHashMap<GridPosition, TileLink> adjTileLinksMap;

            [WriteOnly]
            public EntityCommandBuffer ecbWriter;

            public void Execute()
            {
                for (var i = 0; i < adjTileLinksKeys.Length; ++i) {
                    GridPosition tileKey = adjTileLinksKeys[i];
                    NativeMultiHashMap<GridPosition, TileLink>.Enumerator tileLinksEnumerator =
                        adjTileLinksMap.GetValuesForKey(tileKey);
                    tileLinksEnumerator.MoveNext();

                    TileLink curTileLink = tileLinksEnumerator.Current;
                    Entity tile = ecbWriter.Instantiate(curTileLink.tile);

                    var offset = new float3(0f, 0.3f, 0f);
                    var tileTranslation = new Translation {Value = tileKey + offset};

                    TileBuffer tileBuffer = TileBuffer.Empty;

                    do {
                        curTileLink = tileLinksEnumerator.Current;
                        tileBuffer[(curTileLink.index + 3) % 6] = curTileLink.tile;
                    } while (tileLinksEnumerator.MoveNext());

                    var tileCmpnt =
                        new TileComponent {
                                              Position = tileKey,
                                              State = 0,
                                              AdjacentTiles = tileBuffer
                                          };
                    ecbWriter.SetComponent(tile, tileCmpnt);
                    ecbWriter.SetComponent(tile, tileTranslation);
                    ecbWriter.SetSharedComponent(
                            tile,
                            GridGenerationComponent.OuterNodeLinkingPhase
                        );
                }
            }
        }
    }
}