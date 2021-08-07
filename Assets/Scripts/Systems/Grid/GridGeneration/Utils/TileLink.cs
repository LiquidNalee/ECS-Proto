using System;
using Unity.Entities;

namespace Systems.Grid.GridGeneration.Utils
{
    [Serializable]
    public struct TileLink
    {
        public int index;
        public Entity adjTile;
        public Entity tile;
    }
}