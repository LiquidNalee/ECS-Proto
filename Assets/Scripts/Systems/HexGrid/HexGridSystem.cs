using Unity.Entities;
using Unity.Mathematics;

namespace Systems.HexGrid
{
    // [DisableAutoCreation]
    public class HexGridSystem : SystemBase
    {
        protected override void OnUpdate() { }

        public struct HexDirection
        {
            public static readonly float3 Top = new float3(0f, 0f, -2.1f);
            public static readonly float3 TopRight = new float3(-1.81865f, 0f, -1.05f);
            public static readonly float3 DownRight = new float3(-1.81865f, 0f, 1.05f);
            public static readonly float3 Down = new float3(0f, 0f, 2.1f);
            public static readonly float3 DownLeft = new float3(1.81865f, 0f, 1.05f);
            public static readonly float3 TopLeft = new float3(1.81865f, 0f, -1.05f);
        }
    }
}