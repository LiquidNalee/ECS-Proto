using Unity.Physics;

namespace Systems.Utils
{
    public static class PhysicsUtils
    {
        public enum CollisionLayer
        {
            Grid = 1 << 0
        }

        public static CollisionFilter GridCollisionFilter => new CollisionFilter{
            BelongsTo = ~0u,
            CollidesWith = (uint) CollisionLayer.Grid,
            GroupIndex = 0
        };
    }
}