using Unity.Entities;

namespace Systems.Grid.GridGenerationGroup
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(EndInitializationEntityCommandBufferSystem))]
    public class GridGenerationSystemGroup : ComponentSystemGroup
    {
    }
}