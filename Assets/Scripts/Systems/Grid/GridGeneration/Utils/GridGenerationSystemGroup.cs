using Unity.Entities;

namespace Systems.Grid.GridGeneration.Utils
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(EndFixedStepSimulationEntityCommandBufferSystem))]
    public class GridGenerationSystemGroup : ComponentSystemGroup
    {
    }
}