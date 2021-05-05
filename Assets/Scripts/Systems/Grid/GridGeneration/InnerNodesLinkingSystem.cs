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

            var linkingTilesArray = _innerNodesQuery.ToEntityArray(Allocator.TempJob);
            var commandBuffer = EcbSystem.CreateCommandBuffer();

            foreach (var entity in linkingTilesArray)
                commandBuffer.SetSharedComponent(entity, GridGenerationComponent.ExpansionPhase);

            Dependency =
                new GetOuterNodesLinksUpdatesJob
                {
                    TileComponentTypeHandle = GetComponentTypeHandle<TileComponent>(true),
                    TileLinkUpdateBufferTypeHandle = GetBufferTypeHandle<TileLinkUpdate>(true),
                    Ecb = EcbSystem.CreateCommandBuffer()
                }.Schedule(_innerNodesQuery);

            EcbSystem.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct GetOuterNodesLinksUpdatesJob : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<TileComponent> TileComponentTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<TileLinkUpdate> TileLinkUpdateBufferTypeHandle;

            [WriteOnly]
            public EntityCommandBuffer Ecb;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityInIndex)
            {
                var tileComponentArray = chunk.GetNativeArray(TileComponentTypeHandle);
                var tileLinkUpdateBufferAccessor =
                    chunk.GetBufferAccessor(TileLinkUpdateBufferTypeHandle);
                var markedNodes = new NativeHashSet<Entity>(chunk.Count * 6, Allocator.Temp);

                for (var i = 0; i < chunk.Count; ++i)
                {
                    var tileComponent = tileComponentArray[i];
                    var tileLinkUpdateBuffer = tileLinkUpdateBufferAccessor[i];
                    var tileBuffer = tileComponent.AdjacentTiles.Clone();

                    foreach (var tileLinkUpdate in tileLinkUpdateBuffer)
                        tileBuffer[tileLinkUpdate.Index] = tileLinkUpdate.AdjTile;

                    foreach (var tileLinkUpdate in tileLinkUpdateBuffer)
                    {
                        var index = tileLinkUpdate.Index;

                        var adjTile = tileLinkUpdate.AdjTile;
                        var adjTileRight = tileBuffer[(index + 1) % 6];
                        var adjTileLeft = tileBuffer[(index + 5) % 6];

                        var adjTileLinkUpdates =
                            new NativeList<TileLinkUpdate>(4, Allocator.Temp)
                            {
                                new TileLinkUpdate
                                {Tile = adjTile, Index = (index + 2) % 6, AdjTile = adjTileRight},
                                new TileLinkUpdate
                                {Tile = adjTileRight, Index = (index + 5) % 6, AdjTile = adjTile},
                                new TileLinkUpdate
                                {Tile = adjTileLeft, Index = (index + 2) % 6, AdjTile = adjTile},
                                new TileLinkUpdate
                                {Tile = adjTile, Index = (index + 5) % 6, AdjTile = adjTileLeft}
                            };

                        foreach (var adjTileLinkUpdate in adjTileLinkUpdates)
                        {
                            if (!markedNodes.Contains(adjTileLinkUpdate.Tile))
                            {
                                markedNodes.Add(adjTileLinkUpdate.Tile);
                                Ecb.AddBuffer<TileLinkUpdate>(adjTileLinkUpdate.Tile);
                            }

                            Ecb.AppendToBuffer(adjTileLinkUpdate.Tile, adjTileLinkUpdate);
                        }

                        adjTileLinkUpdates.Dispose();
                    }
                }

                markedNodes.Dispose();
            }
        }
    }
}