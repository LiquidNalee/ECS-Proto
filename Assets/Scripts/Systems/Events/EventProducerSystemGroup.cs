using Unity.Entities;
using Unity.Physics.Systems;

namespace Systems.Events
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(StepPhysicsWorld))]
    [UpdateBefore(typeof(EndFramePhysicsSystem))]
    public class EventProducerSystemGroup : ComponentSystemGroup
    {
    }
}