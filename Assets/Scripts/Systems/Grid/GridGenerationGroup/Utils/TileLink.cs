﻿using System;
using Unity.Entities;

namespace Systems.Grid.GridGenerationGroup.Utils
{
    [Serializable]
    public struct TileLink
    {
        public int Index;
        public Entity AdjTile;
        public Entity Tile;
    }
}