using Components.Grid;
using Unity.Entities;

namespace Systems.Grid.GridGeneration.Utils {
    [UpdateInGroup(typeof(GridGenerationSystemGroup))]
    public abstract class GridGenerationSystemBase : SystemBase {
        protected readonly ComponentType[] GridGenerationRequiredComponents = {
            ComponentType.ReadWrite<TileComponent>(),
            ComponentType.ReadWrite<GridGenerationComponent>()
        };
        protected EndFixedStepSimulationEntityCommandBufferSystem ecbSystem;

        protected override void OnCreate()
        {
            ecbSystem = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        }
    }
}