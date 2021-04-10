using System;
using Unity.Physics;

namespace Systems.Utils
{
    public static class PhysicsUtils
    {
        [Flags]
        public enum CollisionLayer
        {
            Grid = 1 << 0,
            Unit = 1 << 1
        }

        public static CollisionFilter GridFilter => DefaultFilter((uint) CollisionLayer.Grid);

        public static CollisionFilter UnitFilter => DefaultFilter((uint) CollisionLayer.Unit);

        private static CollisionFilter DefaultFilter(uint colliderFlag)
        {
            return new CollisionFilter
                   {
                       BelongsTo = ~0u, CollidesWith = colliderFlag, GroupIndex = 0
                   };
        }
    }
}