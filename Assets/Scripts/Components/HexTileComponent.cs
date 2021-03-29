using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components
{
    [Serializable]
    public struct HexTileComponent: IComponentData
    {
        public float3 Position;
    }
}