using BovineLabs.Event.Systems;
using Components.Controls;
using Components.Movement;
using Unity.Entities;

namespace Systems.Controls
{
    [UpdateInGroup(typeof(ControlSystemGroup))]
    public class UnitDestinationSystem : ConsumeSingleEventSystemBase<RightClickEvent>
    {
        private EndFixedStepSimulationEntityCommandBufferSystem _ecbSystem;

        protected override void OnStartRunning()
        {
            _ecbSystem =
                World.GetExistingSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        protected override void OnEvent(RightClickEvent e)
        {
            Entities.WithAll<UnitComponent>()
                    .ForEach((ref UnitComponent actor) => { actor.Destination = e.Position; })
                    .ScheduleParallel();

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}