namespace Systems.Utils
{
    public static class HexUtils
    {
        public enum HexDirection
        {
            Top = 0,
            TopRight = 1,
            BottomRight = 2,
            Bottom = 3,
            BottomLeft = 4,
            TopLeft = 5
        }

        public static readonly float StepDistance = 1.05f;
    }
}