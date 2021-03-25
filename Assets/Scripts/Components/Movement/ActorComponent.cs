using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components.Movement
{
    [Serializable]
    public struct ActorComponent: IComponentData
    {
        public float3 Position;
        public float3 Destination;
    }
}