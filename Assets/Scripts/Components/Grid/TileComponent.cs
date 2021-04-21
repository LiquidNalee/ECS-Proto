using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components.Grid
{
    [Serializable]
    public struct TileComponent : IComponentData
    {
        public float3 Position;
        public TileState State;
        public TileBuffer AdjacentTiles;
    }

    [Flags]
    public enum TileState : byte
    {
        Walkable,
        Burning
    }

    public enum HexDirection
    {
        Top = 0,
        TopRight = 1,
        BottomRight = 2,
        Bottom = 3,
        BottomLeft = 4,
        TopLeft = 5
    }
}