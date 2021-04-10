namespace Systems.Utils
{
    public static class ClickEventUtils
    {
        public enum ButtonID
        {
            Left = 0,
            Right = 1
        }

        public enum ClickState : ushort
        {
            Null = 0,
            Down = 1,
            Hold = 2,
            Up = 3
        }
    }
}