using BovineLabs.Event.Systems;
using Components.Controls;
using Components.Movement;
using Unity.Entities;

namespace Systems.Controls
{
    public class UnitDestinationSystem : ConsumeSingleEventSystemBase<RightClickEvent>
    {
        protected override void OnEvent(RightClickEvent e) {
            Entities.WithAll<UnitComponent>()
                    .ForEach(
                        (Entity id, ref UnitComponent actor) =>
                        {
                            actor.Destination = e.Position;
                        }
                    )
                    .ScheduleParallel();
        }
    }
}