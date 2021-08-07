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

            var withoutTileLinkBuffer =
                new EntityQueryDesc {None = new ComponentType[] {typeof(TileLinkUpdate)}};
            _outerNodesQuery = GetEntityQuery(
                    TilesBaseQuery,
                    withoutTileLinkBuffer
                );
            _outerNodesQuery.SetSharedComponentFilter(
                    GridGenerationComponent.OuterNodeLinkingPhase
                );
        }

        protected override void OnUpdate()
        {
            if (_outerNodesQuery.IsEmpty) return;

            NativeArray<Entity> linkingTilesArray =
                _outerNodesQuery.ToEntityArray(Allocator.TempJob);
            EntityCommandBuffer commandBuffer = ecbSystem.CreateCommandBuffer();

            foreach (Entity entity in linkingTilesArray)
                commandBuffer.SetSharedComponent(
                        entity,
                        GridGenerationComponent.ExpansionPhase
                    );

            Dependency =
                new GetOuterNodesLinksUpdatesJob
                {
                    entityTypeHandle = GetEntityTypeHandle(),
                    tileComponentTypeHandle = GetComponentTypeHandle<TileComponent>(true),
                    ecb = ecbSystem.CreateCommandBuffer()
                }.Schedule(_outerNodesQuery);

            ecbSystem.AddJobHandleForProducer(Dependency);
        }


        [BurstCompile]
        private struct GetOuterNodesLinksUpdatesJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<TileComponent> tileComponentTypeHandle;

            [WriteOnly]
            public EntityCommandBuffer ecb;

            public void Execute(ArchetypeChunk chunk,
                                int chunkIndex,
                                int firstEntityInIndex)
            {
                NativeArray<Entity> linkingTilesArray = chunk.GetNativeArray(entityTypeHandle);
                NativeArray<TileComponent> tileComponentArray =
                    chunk.GetNativeArray(tileComponentTypeHandle);
                var markedNodes = new NativeHashSet<Entity>(chunk.Count, Allocator.Temp);

                for (var i = 0; i < chunk.Count; ++i)
                {
                    Entity tile = linkingTilesArray[i];
                    TileComponent tileComponent = tileComponentArray[i];

                    for (var j = 0; j < 6; ++j)
                    {
                        Entity adjTile = tileComponent.AdjacentTiles[j];

                        if (adjTile != Entity.Null)
                        {
                            if (!markedNodes.Contains(adjTile))
                            {
                                markedNodes.Add(adjTile);
                                ecb.AddBuffer<TileLinkUpdate>(adjTile);
                            }

                            var tileLinkUpdate =
                                new TileLinkUpdate
                                {
                                    Tile = adjTile, Index = (j + 3) % 6, AdjTile = tile
                                };
                            ecb.AppendToBuffer(adjTile, tileLinkUpdate);
                        }
                    }
                }
            }
        }
    }
}