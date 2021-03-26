using Components.Movement;
using Unity.Entities;
using UnityEngine;

namespace Systems
{
    public class MoveIntentionSystem : SystemBase
    {
        private LayerMask _gridLayerMask;

        protected override void OnCreate() {
            base.OnCreate();
            _gridLayerMask = LayerMask.NameToLayer("Grid");
        }

        protected override void OnUpdate() {
            if (Input.GetMouseButtonDown(1))
            {
                // FIXME: Move this to inputManager and create mouseClickEvent
                // ReSharper disable once PossibleNullReferenceException
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (!Physics.Raycast(ray, out var hitData, 1000, ~_gridLayerMask))
                    return;
                var cursorPos = hitData.transform.position;

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