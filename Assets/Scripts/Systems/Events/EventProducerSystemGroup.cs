using Unity.Entities;
using Unity.Physics.Systems;

namespace Systems.Events
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(EndFixedStepSimulationEntityCommandBufferSystem))]
    [UpdateAfter(typeof(BuildPhysicsWorld))]
    public class EventProducerSystemGroup : ComponentSystemGroup
    {
    }
}