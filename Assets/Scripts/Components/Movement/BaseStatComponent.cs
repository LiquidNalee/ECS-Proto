using System;
using Unity.Entities;

namespace Components.Movement
{
    [Serializable]
    public struct BaseStatComponent: IComponentData
    {
        public float BaseValue;
        public float Value;
    }
}