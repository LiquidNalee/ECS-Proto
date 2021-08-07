using Systems.Utils.Physics;
using Components.Controls;
using Unity.Physics;
using static Systems.Utils.ClickEventUtils;

namespace Systems.Events
{
    public class RightClickEventSystem : ClickEventSystem<RightClickEvent>
    {
        protected override void OnCreate()
        {
            base.OnCreate();

            _buttonID = (int) ButtonID.Right;
            _filter = new CollisionFilter
                      {
                          BelongsTo = ~0u, CollidesWith = (uint) CollisionLayer.Grid, GroupIndex = 0
                      };
        }

        protected override RightClickEvent EventFromRaycastHit(RaycastHit hit, ClickState state)
        {
            return new RightClickEvent {Entity = hit.Entity, Hit = hit, State = (ushort) state};
        }
    }
}