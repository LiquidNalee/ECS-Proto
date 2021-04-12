using System;
using Unity.Entities;

namespace Components.Events.Physics
{
    [Serializable]
    public struct StatefulTriggerEvent : IBufferElementData
    {
        public PhysicsEventState State;

        // ReSharper disable once InconsistentNaming
        public bool _isStale;
        public Entity Entity;
    }
}