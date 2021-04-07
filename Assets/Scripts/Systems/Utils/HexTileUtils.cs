using Unity.Mathematics;

namespace Systems.Utils
{
    public struct HexDirection
    {
        public static readonly float StepDistance = 1.05f;

        public static readonly float3 Top = new float3(0f, 0f, -2.1f);
        public static readonly float3 TopRight = new float3(-1.81865f, 0f, -1.05f);
        public static readonly float3 BottomRight = new float3(-1.81865f, 0f, 1.05f);
        public static readonly float3 Bottom = new float3(0f, 0f, 2.1f);
        public static readonly float3 BottomLeft = new float3(1.81865f, 0f, 1.05f);
        public static readonly float3 TopLeft = new float3(1.81865f, 0f, -1.05f);
    }
}