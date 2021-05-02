using System;

namespace Systems.Utils.Physics
{
    [Flags]
    public enum CollisionLayer
    {
        Grid = 1 << 0,
        Unit = 1 << 1,
        Environment = 1 << 20
    }
}