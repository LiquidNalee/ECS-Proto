using System;
using Unity.Entities;

namespace Components.Grid {
    [Serializable]
    public struct GridGenerationComponent : ISharedComponentData {
        public GridGenerationPhase Phase;

        public static GridGenerationComponent ExpansionPhase =>
            new GridGenerationComponent {Phase = GridGenerationPhase.Expansion};

        public static GridGenerationComponent CollisionCheckPhase =>
            new GridGenerationComponent {Phase = GridGenerationPhase.CollisionCheck};

        public static GridGenerationComponent OuterNodeLinkingPhase =>
            new GridGenerationComponent {Phase = GridGenerationPhase.OuterNodeLinking};

        public static GridGenerationComponent InnerNodeLinkingPhase =>
            new GridGenerationComponent {Phase = GridGenerationPhase.InnerNodeLinking};

        public static GridGenerationComponent NodeUpdatingPhase =>
            new GridGenerationComponent {Phase = GridGenerationPhase.NodeUpdating};

        public static GridGenerationComponent ReadyPhase =>
            new GridGenerationComponent {Phase = GridGenerationPhase.Ready};
    }

    public enum GridGenerationPhase : byte {
        Expansion,
        CollisionCheck,
        OuterNodeLinking,
        InnerNodeLinking,
        NodeUpdating,
        Ready
    }
}