using System.Linq;
using Systems.Grid.GridGeneration.Utils;
using Components.Grid;
using Components.Grid.Tags;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Systems.Grid.GridGeneration {
    public class InnerNodesLinkingSystem : GridGenerationSystemBase {
        private EntityQuery _innerNodesQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            ComponentType[] innerNodeRequiredComponents = {
                                                              ComponentType
                                                                  .ReadWrite<TileLinkUpdate>(),
                                                              ComponentType.Exclude<TileLinkLock>()
                                                          };
            _innerNodesQuery = EntityManager.CreateEntityQuery(
                    GridGenerationRequiredComponents.Concat(innerNodeRequiredComponents)
                                                    .ToArray()
                );
            _innerNodesQuery.SetSharedComponentFilter(
                    GridGenerationComponent.InnerNodeLinkingPhase
                );
        }

        protected override void OnUpdate()
        {
            if (_innerNodesQuery.IsEmpty) return;

            NativeArray<Entity> linkingTilesArray =
                _innerNodesQuery.ToEntityArray(Allocator.TempJob);
            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();

            foreach (Entity tile in linkingTilesArray)
                ecb.AddSharedComponent(tile, new TileLinkLock());
            linkingTilesArray.Dispose();

            Dependency =
                new GetOuterNodesLinksUpdatesJob {
                                                     tileComponentTypeHandle =
                                                         GetComponentTypeHandle<TileComponent>(
                                                                 true
                                                             ),
                                                     tileLinkUpdateBufferTypeHandle =
                                                         GetBufferTypeHandle<TileLinkUpdate>(true),
                                                     ecb = ecbSystem.CreateCommandBuffer()
                                                         .AsParallelWriter()
                                                 }.Schedule(_innerNodesQuery);

            ecbSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct GetOuterNodesLinksUpdatesJob : IJobChunk {
            [ReadOnly]
            public ComponentTypeHandle<TileComponent> tileComponentTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<TileLinkUpdate> tileLinkUpdateBufferTypeHandle;

            [WriteOnly]
            public EntityCommandBuffer.ParallelWriter ecb;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityInIndex)
            {
                NativeArray<TileComponent> tileComponentArray =
                    chunk.GetNativeArray(tileComponentTypeHandle);
                BufferAccessor<TileLinkUpdate> tileLinkUpdateBufferAccessor =
                    chunk.GetBufferAccessor(tileLinkUpdateBufferTypeHandle);
                var markedNodes = new NativeHashSet<Entity>(chunk.Count * 6, Allocator.Temp);

                for (var i = 0; i < chunk.Count; ++i)
                    UpdateInnerNodeLinks(
                            chunkIndex,
                            tileComponentArray[i],
                            tileLinkUpdateBufferAccessor[i],
                            markedNodes
                        );

                markedNodes.Dispose();
            }

            private void UpdateInnerNodeLinks(
                    int chunkIndex,
                    TileComponent tileComponent,
                    DynamicBuffer<TileLinkUpdate> tileLinkUpdateBuffer,
                    NativeHashSet<Entity> markedNodes
                )
            {
                TileBuffer tileBuffer = tileComponent.AdjacentTiles.Clone();

                for (var j = 0; j < tileLinkUpdateBuffer.Length; ++j) {
                    TileLinkUpdate tileLinkUpdate = tileLinkUpdateBuffer[j];
                    tileBuffer[tileLinkUpdate.Index] = tileLinkUpdate.AdjTile;
                }

                for (var j = 0; j < tileLinkUpdateBuffer.Length; ++j) {
                    NativeList<TileLinkUpdate> adjTileLinkUpdates =
                        GetAdjTileLinkUpdates(
                                tileLinkUpdateBuffer[j],
                                tileBuffer
                            );

                    for (var k = 0; k < adjTileLinkUpdates.Length; ++k)
                        AppendAdjTileLinkUpdateToBuffer(
                                chunkIndex,
                                adjTileLinkUpdates[k],
                                markedNodes
                            );

                    adjTileLinkUpdates.Dispose();
                }
            }

            private NativeList<TileLinkUpdate> GetAdjTileLinkUpdates(
                    TileLinkUpdate tileLinkUpdate,
                    TileBuffer tileBuffer
                )
            {
                var index = tileLinkUpdate.Index;
                Entity adjTile = tileLinkUpdate.AdjTile;
                Entity adjTileRight = tileBuffer[(index + 1) % 6];
                Entity adjTileLeft = tileBuffer[(index + 5) % 6];

                var adjTileLinkUpdates =
                    new NativeList<TileLinkUpdate>(4, Allocator.Temp) {
                        new TileLinkUpdate {
                                               Tile = adjTile,
                                               Index = (index + 2) % 6,
                                               AdjTile = adjTileRight
                                           },
                        new TileLinkUpdate {
                                               Tile = adjTileRight,
                                               Index = (index + 5) % 6,
                                               AdjTile = adjTile
                                           },
                        new TileLinkUpdate {
                                               Tile = adjTileLeft,
                                               Index = (index + 2) % 6,
                                               AdjTile = adjTile
                                           },
                        new TileLinkUpdate {
                                               Tile = adjTile,
                                               Index = (index + 5) % 6,
                                               AdjTile = adjTileLeft
                                           }
                    };

                return adjTileLinkUpdates;
            }

            private void AppendAdjTileLinkUpdateToBuffer(
                    int chunkIndex,
                    TileLinkUpdate adjTileLinkUpdate,
                    NativeHashSet<Entity> markedNodes
                )
            {
                if (!markedNodes.Contains(adjTileLinkUpdate.Tile)) {
                    markedNodes.Add(adjTileLinkUpdate.Tile);
                    // FIXME: Colliding Expanding Nodes will override each other's buffer
                    ecb.AddBuffer<TileLinkUpdate>(chunkIndex, adjTileLinkUpdate.Tile);
                }

                ecb.AppendToBuffer(
                        chunkIndex,
                        adjTileLinkUpdate.Tile,
                        adjTileLinkUpdate
                    );
            }
        }
    }
}