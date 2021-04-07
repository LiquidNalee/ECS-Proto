using Systems.Events;
using Unity.Entities;

namespace Systems.Controls
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(EndFixedStepSimulationEntityCommandBufferSystem))]
    [UpdateAfter(typeof(EventProducerSystemGroup))]
    public class ControlSystemGroup : ComponentSystemGroup
    {
    }
}