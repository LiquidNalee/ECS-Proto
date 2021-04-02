using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components.Controls
{
    [Serializable]
    public struct LeftClickEvent : IComponentData
    {
        public float3 Position;
        public Entity Entity;
    }
}