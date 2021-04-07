using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Components.Controls
{
    [Serializable]
    public struct LeftClickEvent : IComponentData
    {
        public float3 Position;
        public ushort State;
        public Entity Entity;
        public RaycastHit Hit;
    }
}