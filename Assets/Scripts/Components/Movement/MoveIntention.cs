using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components.Movement
{
    [Serializable]
    public struct MoveIntentionEvent: IComponentData
    {
        public float3 Destination;
    }
}