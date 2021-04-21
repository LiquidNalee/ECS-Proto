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
}