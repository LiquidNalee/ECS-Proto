using System;
using Unity.Entities;

namespace Components.Controls
{
    [Serializable]
    public struct LeftClickEvent : IComponentData
    {
        public Entity Entity;
    }
}