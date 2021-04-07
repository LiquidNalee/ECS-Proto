using Systems.Utils;
using Components.Controls;
using Unity.Physics;

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
                BelongsTo = ~0u,
                CollidesWith = (uint) PhysicsUtils.CollisionLayer.Grid,
                GroupIndex = 0
            };
        }

        protected override RightClickEvent EventFromRaycastHit(RaycastHit hit,
            ClickState state)
        {
            return new RightClickEvent
            {
                Entity = hit.Entity,
                Position = _physicsWorld.Bodies[hit.RigidBodyIndex]
                    .WorldFromBody.pos,
                Hit = hit,
                State = (ushort) state
            };
        }
    }
}