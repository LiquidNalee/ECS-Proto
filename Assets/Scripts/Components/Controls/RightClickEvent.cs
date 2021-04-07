using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components.Controls
{
    [Serializable]
    public struct RightClickEvent : IComponentData
    {
        public float3 Position;
        public ushort State;
        public Entity Entity;
    }
}