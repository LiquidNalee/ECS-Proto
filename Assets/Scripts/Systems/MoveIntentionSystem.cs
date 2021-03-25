using Components.Movement;
using Unity.Entities;
using UnityEngine;

namespace Systems
{
    // [DisableAutoCreation]
    public class MoveIntentionSystem : SystemBase
    {
        protected override void OnUpdate() {
            if (Input.GetMouseButtonDown(1))
            {
                // FIXME: Move this to inputManager and create mouseClickEvent
                // ReSharper disable once PossibleNullReferenceException
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                // FIXME: Add Walkable layer
                if (!Physics.Raycast(ray, out var hitData, 1000)) return;
                var cursorPos = hitData.point;

                Entities.WithAll<ActorComponent>()
                        .ForEach(
                            (Entity id, ref ActorComponent actor) =>
                            {
                                actor.Destination = cursorPos;
                            }
                        )
                        .Schedule();
            }
        }
    }
}