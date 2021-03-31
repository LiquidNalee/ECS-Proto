using BovineLabs.Event.Systems;
using Components.Inputs;
using Components.Movement;
using Unity.Entities;

namespace Systems
{
    public class
        RightClickEventConsumerSystem : ConsumeSingleEventSystemBase<RightClickEvent>
    {
        protected override void OnEvent(RightClickEvent e) {
            Entities.WithAll<ActorComponent>()
                    .ForEach(
                        (Entity id, ref ActorComponent actor) =>
                        {
                            actor.Destination = e.Position;
                        }
                    )
                    .Schedule();
        }
    }
}