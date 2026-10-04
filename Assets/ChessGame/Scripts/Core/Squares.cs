using System;

namespace Chess.Core
{
    /// <summary>
    /// Square indices run 0..63 with 0 = a1 and 63 = h8, i.e. index = rank * 8 + file.
    /// </summary>
    public static class Squares
    {
        public const int None = -1;

        public static int At(int file, int rank) => rank * 8 + file;

        public static int FileOf(int square) => square & 7;

        public static int RankOf(int square) => square >> 3;

        public static bool IsValid(int square) => square >= 0 && square < 64;

        /// <summary>True when the two squares are on the board and no further than <paramref name="max"/> files apart.</summary>
        public static bool OnBoardStep(int from, int to, int max = 2)
        {
            if (!IsValid(to)) return false;
            return Math.Abs(FileOf(from) - FileOf(to)) <= max;
        }

        public static string ToAlgebraic(int square)
        {
            if (!IsValid(square)) return "-";
            return string.Concat((char)('a' + FileOf(square)), (char)('1' + RankOf(square)));
        }

        public static int FromAlgebraic(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2) return None;
            int file = char.ToLowerInvariant(text[0]) - 'a';
            int rank = text[1] - '1';
            if (file < 0 || file > 7 || rank < 0 || rank > 7) return None;
            return At(file, rank);
        }

        /// <summary>Light squares are the ones where file and rank have the same parity (a1 is dark).</summary>
        public static bool IsLightSquare(int square) => ((FileOf(square) + RankOf(square)) & 1) == 1;
    }
}
