using System;

namespace Chess.Core
{
    [Flags]
    public enum MoveFlags : byte
    {
        None = 0,
        Capture = 1 << 0,
        DoublePawnPush = 1 << 1,
        EnPassant = 1 << 2,
        KingSideCastle = 1 << 3,
        QueenSideCastle = 1 << 4,
        Promotion = 1 << 5
    }

    public readonly struct Move : IEquatable<Move>
    {
        public static readonly Move Null = new Move(0, 0, MoveFlags.None, PieceType.None);

        public readonly byte From;
        public readonly byte To;
        public readonly MoveFlags Flags;
        public readonly PieceType Promotion;

        public Move(int from, int to, MoveFlags flags = MoveFlags.None, PieceType promotion = PieceType.None)
        {
            From = (byte)from;
            To = (byte)to;
            Flags = flags;
            Promotion = promotion;
        }

        public bool IsNull => From == To;
        public bool IsCapture => (Flags & MoveFlags.Capture) != 0;
        public bool IsEnPassant => (Flags & MoveFlags.EnPassant) != 0;
        public bool IsPromotion => (Flags & MoveFlags.Promotion) != 0;
        public bool IsDoublePawnPush => (Flags & MoveFlags.DoublePawnPush) != 0;
        public bool IsCastle => (Flags & (MoveFlags.KingSideCastle | MoveFlags.QueenSideCastle)) != 0;
        public bool IsKingSideCastle => (Flags & MoveFlags.KingSideCastle) != 0;
        public bool IsQueenSideCastle => (Flags & MoveFlags.QueenSideCastle) != 0;

        public bool Equals(Move other)
        {
            return From == other.From && To == other.To && Promotion == other.Promotion;
        }

        public override bool Equals(object obj) => obj is Move other && Equals(other);

        public override int GetHashCode() => From | (To << 8) | ((int)Promotion << 16);

        public static bool operator ==(Move a, Move b) => a.Equals(b);
        public static bool operator !=(Move a, Move b) => !a.Equals(b);

        /// <summary>Long algebraic notation, e.g. "e2e4" or "e7e8q".</summary>
        public string ToUci()
        {
            string s = Squares.ToAlgebraic(From) + Squares.ToAlgebraic(To);
            if (IsPromotion)
            {
                switch (Promotion)
                {
                    case PieceType.Queen: s += "q"; break;
                    case PieceType.Rook: s += "r"; break;
                    case PieceType.Bishop: s += "b"; break;
                    case PieceType.Knight: s += "n"; break;
                }
            }
            return s;
        }

        public override string ToString() => ToUci();
    }
}
