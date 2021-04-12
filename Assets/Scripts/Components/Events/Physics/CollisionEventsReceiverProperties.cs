using System;
using Unity.Entities;

namespace Components.Events.Physics
{
    [Serializable]
    public struct CollisionEventsReceiverProperties : IComponentData
    {
        public bool UsesCollisionDetails;
    }
}