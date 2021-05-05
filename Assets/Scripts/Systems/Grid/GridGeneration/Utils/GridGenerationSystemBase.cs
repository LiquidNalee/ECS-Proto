using Components.Grid;
using Unity.Entities;

namespace Systems.Grid.GridGeneration.Utils
{
    [UpdateInGroup(typeof(GridGenerationSystemGroup))]
    public abstract class GridGenerationSystemBase : SystemBase
    {
        protected EndFixedStepSimulationEntityCommandBufferSystem EcbSystem;
        protected EntityQueryDesc TilesBaseQuery;

        protected override void OnCreate()
        {
            EcbSystem = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
            TilesBaseQuery = new EntityQueryDesc
                             {
                                 All = new ComponentType[]
                                       {
                                           typeof(TileComponent),
                                           typeof(GridGenerationComponent)
                                       }
                             };
        }
    }
}