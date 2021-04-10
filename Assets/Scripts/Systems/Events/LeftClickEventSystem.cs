using Systems.Utils;
using Components.Controls;
using Unity.Physics;
using static Systems.Utils.ClickEventUtils;

namespace Systems.Events
{
    public class LeftClickEventSystem : ClickEventSystem<LeftClickEvent>
    {
        protected override void OnCreate()
        {
            base.OnCreate();

            _buttonID = (int) ButtonID.Left;
            _filter = new CollisionFilter
                      {
                          BelongsTo = ~0u,
                          CollidesWith =
                              (uint) (PhysicsUtils.CollisionLayer.Unit |
                                      PhysicsUtils.CollisionLayer.Grid),
                          GroupIndex = 0
                      };
        }

        protected override LeftClickEvent EventFromRaycastHit(RaycastHit hit, ClickState state)
        {
            return new LeftClickEvent {Entity = hit.Entity, Hit = hit, State = (ushort) state};
        }
    }
}