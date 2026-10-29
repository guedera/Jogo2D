using UnityEngine;

namespace TypeDuel
{
    public enum PowerType : byte
    {
        Bobomb = 0,
        Glitch = 1,
        Change = 2,
    }

    public static class PowerInfo
    {
        public const int Count = 3;

        public static string Name(PowerType t)
        {
            switch (t)
            {
                case PowerType.Bobomb: return "BOBOMB";
                case PowerType.Glitch: return "GLITCH";
                default: return "CHANGE";
            }
        }

        public static Color Color(PowerType t)
        {
            switch (t)
            {
                case PowerType.Bobomb: return new Color(1f, 0.54f, 0.24f);
                case PowerType.Glitch: return new Color(0.70f, 0.42f, 1f);
                default: return new Color(0.24f, 0.78f, 1f);
            }
        }
    }
}
