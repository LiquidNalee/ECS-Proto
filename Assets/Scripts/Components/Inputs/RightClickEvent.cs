using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components.Inputs
{
    [Serializable]
    public struct RightClickEvent : IComponentData
    {
        public float3 Position;
        public Entity Entity;
    }
}