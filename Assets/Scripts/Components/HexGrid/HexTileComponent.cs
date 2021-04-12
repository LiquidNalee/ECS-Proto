using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components.HexGrid
{
    [Serializable]
    public struct HexTileComponent : IComponentData
    {
        public float3 Position;
    }

    [InternalBufferCapacity(6)]
    public struct AdjacentTileBufferElement : IBufferElementData
    {
        public Entity Value;

        public static implicit operator Entity(AdjacentTileBufferElement e) { return e.Value; }
    }
}