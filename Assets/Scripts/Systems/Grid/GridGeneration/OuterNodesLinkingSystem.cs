using Systems.Grid.GridGeneration.Utils;
using Components.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Systems.Grid.GridGeneration
{
    public class OuterNodesLinkingSystem : GridGenerationSystemBase
    {
        private EntityQuery _outerNodesQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            var withoutTileLinkBuffer = new EntityQueryDesc
                                        {
                                            None = new ComponentType[] {typeof(TileLinkUpdate)}
                                        };
            _outerNodesQuery = GetEntityQuery(TilesBaseQuery, withoutTileLinkBuffer);
            _outerNodesQuery.SetSharedComponentFilter(
                    GridGenerationComponent.OuterNodeLinkingPhase
                );
        }

        protected override void OnUpdate()
        {
            if (_outerNodesQuery.IsEmpty) return;

            var linkingTilesArray = _outerNodesQuery.ToEntityArray(Allocator.TempJob);
            var commandBuffer = EcbSystem.CreateCommandBuffer();

            foreach (var entity in linkingTilesArray)
                commandBuffer.SetSharedComponent(entity, GridGenerationComponent.ExpansionPhase);

            Dependency =
                new GetOuterNodesLinksUpdatesJob
                {
                    EntityTypeHandle = GetEntityTypeHandle(),
                    TileComponentTypeHandle = GetComponentTypeHandle<TileComponent>(true),
                    Ecb = EcbSystem.CreateCommandBuffer()
                }.Schedule(_outerNodesQuery);

            EcbSystem.AddJobHandleForProducer(Dependency);
        }


        [BurstCompile]
        private struct GetOuterNodesLinksUpdatesJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<TileComponent> TileComponentTypeHandle;

            [WriteOnly]
            public EntityCommandBuffer Ecb;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityInIndex)
            {
                var linkingTilesArray = chunk.GetNativeArray(EntityTypeHandle);
                var tileComponentArray = chunk.GetNativeArray(TileComponentTypeHandle);
                var markedNodes = new NativeHashSet<Entity>(chunk.Count, Allocator.Temp);

                for (var i = 0; i < chunk.Count; ++i)
                {
                    var tile = linkingTilesArray[i];
                    var tileComponent = tileComponentArray[i];

                    for (var j = 0; j < 6; ++j)
                    {
                        var adjTile = tileComponent.AdjacentTiles[j];

                        if (adjTile != Entity.Null)
                        {
                            if (!markedNodes.Contains(adjTile))
                            {
                                markedNodes.Add(adjTile);
                                Ecb.AddBuffer<TileLinkUpdate>(adjTile);
                            }

                            var tileLinkUpdate = new TileLinkUpdate
                                                 {
                                                     Tile = adjTile,
                                                     Index = (j + 3) % 6,
                                                     AdjTile = tile
                                                 };
                            Ecb.AppendToBuffer(adjTile, tileLinkUpdate);
                        }
                    }
                }
            }
        }
    }
}