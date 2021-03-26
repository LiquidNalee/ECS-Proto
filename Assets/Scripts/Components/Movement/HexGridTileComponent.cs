using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components.Movement
{
    [Serializable]
    public struct HexGridTileComponent: IComponentData
    {
        public float3 Position;
    }
}