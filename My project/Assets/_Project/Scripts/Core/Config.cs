namespace TypeDuel
{
    /// <summary>Valores de balanceamento e rede do MVP. Mover para ScriptableObject quando houver varios modos.</summary>
    public static class Config
    {
        public const int MaxPlayers = 2;
        public const int MinPlayersToStart = 2;
        public const int Port = 7777;
        public const int MaxNameLength = 12;

        public const int WordCount = 100;
        public const int TrainingWordCount = 300;
        public const float TrainingSeconds = 60f;
        public const int ExtraTypedLimit = 20;
        public const float CountdownSeconds = 3f;

        public const float PowerWordFraction = 0.15f;
        public const int MaxHeldPowers = 3;
        public const int AutocompleteWords = 10;
        public const float TypingVolume = 0.045f;
        public const float GlitchSeconds = 15f;
        public const int BombEraseChars = 10;
        public const int ChangeRewindChars = 20;

        public const float ConnectTimeoutSeconds = 6f;
    }
}
