using Chess.AI;
using Chess.Core;
using UnityEngine;

namespace Chess.Game
{
    public enum PlayerSide
    {
        White = 0,
        Black = 1,
        Random = 2
    }

    /// <summary>Player preferences, persisted through PlayerPrefs so they survive a restart.</summary>
    public static class GameSettings
    {
        private const string KeyDifficulty = "chess.difficulty";
        private const string KeySide = "chess.side";
        private const string KeySound = "chess.sound";
        private const string KeyVolume = "chess.volume";
        private const string KeyLegalMoves = "chess.showLegalMoves";
        private const string KeyLastMove = "chess.showLastMove";
        private const string KeyTheme = "chess.theme";
        private const string KeyAnimationSpeed = "chess.animationSpeed";

        public static Difficulty Difficulty { get; set; } = Difficulty.Medium;
        public static PlayerSide PreferredSide { get; set; } = PlayerSide.White;
        public static bool SoundEnabled { get; set; } = true;
        public static float Volume { get; set; } = 0.8f;
        public static bool ShowLegalMoves { get; set; } = true;
        public static bool ShowLastMove { get; set; } = true;
        public static int ThemeIndex { get; set; }

        /// <summary>Multiplier on piece animation: higher is snappier.</summary>
        public static float AnimationSpeed { get; set; } = 1f;

        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            Difficulty = (Difficulty)PlayerPrefs.GetInt(KeyDifficulty, (int)Difficulty.Medium);
            PreferredSide = (PlayerSide)PlayerPrefs.GetInt(KeySide, (int)PlayerSide.White);
            SoundEnabled = PlayerPrefs.GetInt(KeySound, 1) == 1;
            Volume = PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            ShowLegalMoves = PlayerPrefs.GetInt(KeyLegalMoves, 1) == 1;
            ShowLastMove = PlayerPrefs.GetInt(KeyLastMove, 1) == 1;
            ThemeIndex = PlayerPrefs.GetInt(KeyTheme, 0);
            AnimationSpeed = PlayerPrefs.GetFloat(KeyAnimationSpeed, 1f);

            Clamp();
        }

        public static void Save()
        {
            Clamp();

            PlayerPrefs.SetInt(KeyDifficulty, (int)Difficulty);
            PlayerPrefs.SetInt(KeySide, (int)PreferredSide);
            PlayerPrefs.SetInt(KeySound, SoundEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(KeyVolume, Volume);
            PlayerPrefs.SetInt(KeyLegalMoves, ShowLegalMoves ? 1 : 0);
            PlayerPrefs.SetInt(KeyLastMove, ShowLastMove ? 1 : 0);
            PlayerPrefs.SetInt(KeyTheme, ThemeIndex);
            PlayerPrefs.SetFloat(KeyAnimationSpeed, AnimationSpeed);
            PlayerPrefs.Save();
        }

        private static void Clamp()
        {
            Volume = Mathf.Clamp01(Volume);
            AnimationSpeed = Mathf.Clamp(AnimationSpeed, 0.5f, 3f);

            if (ThemeIndex < 0 || ThemeIndex >= Visual.BoardTheme.All.Length) ThemeIndex = 0;
            if (Difficulty < Difficulty.Easy || Difficulty > Difficulty.Expert) Difficulty = Difficulty.Medium;
            if (PreferredSide < PlayerSide.White || PreferredSide > PlayerSide.Random) PreferredSide = PlayerSide.White;
        }

        /// <summary>Resolves <see cref="PlayerSide.Random"/> into an actual colour for a new game.</summary>
        public static PieceColor ResolveHumanColor()
        {
            switch (PreferredSide)
            {
                case PlayerSide.Black: return PieceColor.Black;
                case PlayerSide.Random: return Random.value < 0.5f ? PieceColor.White : PieceColor.Black;
                default: return PieceColor.White;
            }
        }

        public static Visual.BoardTheme Theme => Visual.BoardTheme.Get(ThemeIndex);
    }
}
