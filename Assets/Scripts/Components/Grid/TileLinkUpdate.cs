using System;
using Unity.Entities;

namespace Components.Grid
{
    [Serializable]
    [InternalBufferCapacity(6)]
    public struct TileLinkUpdate : IBufferElementData
    {
        public int Index;
        public Entity AdjTile;
        public Entity Tile;
    }
}