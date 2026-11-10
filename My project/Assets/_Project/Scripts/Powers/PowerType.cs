using UnityEngine;

namespace TypeDuel
{
    public enum PowerType : byte
    {
        Bobomb = 0,
        Glitch = 1,
        Change = 2,
        Autocomplete = 3,
    }

    public static class PowerInfo
    {
        public const int Count = 4;

        public static string Name(PowerType t)
        {
            switch (t)
            {
                case PowerType.Bobomb: return "BOBOMB";
                case PowerType.Glitch: return "GLITCH";
                case PowerType.Autocomplete: return "AUTOCOMPLETE";
                default: return "CHANGE";
            }
        }

        public static Color Color(PowerType t)
        {
            switch (t)
            {
                case PowerType.Bobomb: return new Color(1f, 0.54f, 0.24f);
                case PowerType.Autocomplete: return new Color(0.3f, 0.9f, 0.65f);
                case PowerType.Glitch: return new Color(0.70f, 0.42f, 1f);
                default: return new Color(0.24f, 0.78f, 1f);
            }
        }
    }
}
