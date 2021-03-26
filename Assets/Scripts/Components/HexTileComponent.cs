using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components
{
    [Serializable]
    public class HexTileComponent: IComponentData
    {
        public int3 Position;
    }
}