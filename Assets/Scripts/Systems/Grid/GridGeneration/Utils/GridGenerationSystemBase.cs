using Components.Grid;
using Unity.Entities;

namespace Systems.Grid.GridGeneration.Utils
{
    [UpdateInGroup(typeof(GridGenerationSystemGroup))]
    public abstract class GridGenerationSystemBase : SystemBase
    {
        protected readonly EntityQueryDesc TilesBaseQuery =
            new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(TileComponent), typeof(GridGenerationComponent)}
            };
        protected EndFixedStepSimulationEntityCommandBufferSystem ecbSystem;

        protected override void OnCreate()
        {
            ecbSystem = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        }
    }
}