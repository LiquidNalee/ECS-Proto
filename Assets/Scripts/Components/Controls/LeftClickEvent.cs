using System;
using Unity.Entities;
using Unity.Physics;

namespace Components.Controls
{
    [Serializable]
    public struct LeftClickEvent : IComponentData
    {
        public ushort State;
        public Entity Entity;
        public RaycastHit Hit;
    }
}