using System;
using Unity.Entities;

namespace Components.Grid
{
    [Serializable]
    public struct GridGenerationComponent : ISharedComponentData
    {
        public GridGenerationPhase Phase;

        public GridGenerationComponent(GridGenerationPhase phase) { Phase = phase; }

        public static implicit operator GridGenerationPhase(GridGenerationComponent cmpnt)
        {
            return cmpnt.Phase;
        }
    }

    public enum GridGenerationPhase : byte
    {
        Expansion,
        CollisionCheck,
        Linking,
        End
    }
}