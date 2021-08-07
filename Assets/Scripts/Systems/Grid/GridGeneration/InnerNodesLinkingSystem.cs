using Systems.Grid.GridGeneration.Utils;
using Components.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Systems.Grid.GridGeneration
{
    public class InnerNodesLinkingSystem : GridGenerationSystemBase
    {
        private EntityQuery _innerNodesQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            var withTileLinkBuffer = new EntityQueryDesc
                                     {
                                         All = new ComponentType[] {typeof(TileLinkUpdate)}
                                     };
            _innerNodesQuery = GetEntityQuery(TilesBaseQuery, withTileLinkBuffer);
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
                ecb.SetSharedComponent(tile, GridGenerationComponent.ExpansionPhase);

            Dependency =
                new GetOuterNodesLinksUpdatesJob
                {
                    tileComponentTypeHandle = GetComponentTypeHandle<TileComponent>(true),
                    tileLinkUpdateBufferTypeHandle = GetBufferTypeHandle<TileLinkUpdate>(true),
                    ecb = ecbSystem.CreateCommandBuffer()
                }.Schedule(_innerNodesQuery);

            ecbSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct GetOuterNodesLinksUpdatesJob : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<TileComponent> tileComponentTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<TileLinkUpdate> tileLinkUpdateBufferTypeHandle;

            [WriteOnly]
            public EntityCommandBuffer ecb;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityInIndex)
            {
                NativeArray<TileComponent> tileComponentArray =
                    chunk.GetNativeArray(tileComponentTypeHandle);
                BufferAccessor<TileLinkUpdate> tileLinkUpdateBufferAccessor =
                    chunk.GetBufferAccessor(tileLinkUpdateBufferTypeHandle);
                var markedNodes = new NativeHashSet<Entity>(chunk.Count * 6, Allocator.Temp);

                for (var i = 0; i < chunk.Count; ++i)
                {
                    TileComponent tileComponent = tileComponentArray[i];
                    DynamicBuffer<TileLinkUpdate> tileLinkUpdateBuffer =
                        tileLinkUpdateBufferAccessor[i];
                    TileBuffer tileBuffer = tileComponent.AdjacentTiles.Clone();

                    foreach (TileLinkUpdate tileLinkUpdate in tileLinkUpdateBuffer)
                        tileBuffer[tileLinkUpdate.Index] = tileLinkUpdate.AdjTile;

                    foreach (TileLinkUpdate tileLinkUpdate in tileLinkUpdateBuffer)
                    {
                        var index = tileLinkUpdate.Index;

                        Entity adjTile = tileLinkUpdate.AdjTile;
                        Entity adjTileRight = tileBuffer[(index + 1) % 6];
                        Entity adjTileLeft = tileBuffer[(index + 5) % 6];

                        var adjTileLinkUpdates =
                            new NativeList<TileLinkUpdate>(4, Allocator.Temp)
                            {
                                new TileLinkUpdate
                                {
                                    Tile = adjTile, Index = (index + 2) % 6, AdjTile = adjTileRight
                                },
                                new TileLinkUpdate
                                {
                                    Tile = adjTileRight, Index = (index + 5) % 6, AdjTile = adjTile
                                },
                                new TileLinkUpdate
                                {
                                    Tile = adjTileLeft, Index = (index + 2) % 6, AdjTile = adjTile
                                },
                                new TileLinkUpdate
                                {
                                    Tile = adjTile, Index = (index + 5) % 6, AdjTile = adjTileLeft
                                }
                            };

                        foreach (TileLinkUpdate adjTileLinkUpdate in adjTileLinkUpdates)
                        {
                            if (!markedNodes.Contains(adjTileLinkUpdate.Tile))
                            {
                                markedNodes.Add(adjTileLinkUpdate.Tile);
                                ecb.AddBuffer<TileLinkUpdate>(adjTileLinkUpdate.Tile);
                            }

                            ecb.AppendToBuffer(adjTileLinkUpdate.Tile, adjTileLinkUpdate);
                        }

                        adjTileLinkUpdates.Dispose();
                    }
                }

                markedNodes.Dispose();
            }
        }
    }
}