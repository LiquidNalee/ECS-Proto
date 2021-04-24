using Components.Grid;
using Unity.Entities;

namespace Systems.Grid.GridGenerationGroup
{
    [UpdateInGroup(typeof(GridGenerationSystemGroup))]
    public abstract class GridGenerationSystemBase : SystemBase
    {
        protected EndInitializationEntityCommandBufferSystem EcbSystem;
        protected EntityQuery TilesQuery;

        protected override void OnCreate()
        {
            EcbSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();

            TilesQuery = GetEntityQuery(
                    ComponentType.ReadOnly<TileComponent>(),
                    ComponentType.ReadWrite<GridGenerationComponent>()
                );
        }
    }
}