using System;

namespace Chess.Core
{
    /// <summary>
    /// Zobrist hashing. Keys are generated from a fixed seed so that hashes are stable
    /// between runs, which keeps the transposition table and repetition detection deterministic.
    /// </summary>
    public static class Zobrist
    {
        private const int MaxPieceCode = 24;

        public static readonly ulong[,] PieceKeys = new ulong[MaxPieceCode, 64];
        public static readonly ulong[] CastlingKeys = new ulong[16];
        public static readonly ulong[] EnPassantFileKeys = new ulong[9];
        public static readonly ulong SideToMoveKey;

        static Zobrist()
        {
            var rng = new Random(2361912);

            for (int piece = 0; piece < MaxPieceCode; piece++)
                for (int square = 0; square < 64; square++)
                    PieceKeys[piece, square] = NextULong(rng);

            for (int i = 0; i < CastlingKeys.Length; i++)
                CastlingKeys[i] = NextULong(rng);

            // Index 8 is "no en passant square" and stays zero-cost by convention.
            for (int i = 0; i < EnPassantFileKeys.Length; i++)
                EnPassantFileKeys[i] = NextULong(rng);
            EnPassantFileKeys[8] = 0UL;

            SideToMoveKey = NextULong(rng);
        }

        private static ulong NextULong(Random rng)
        {
            var buffer = new byte[8];
            rng.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }

        public static ulong Compute(Board board)
        {
            ulong key = 0UL;

            for (int square = 0; square < 64; square++)
            {
                byte piece = board.Squares[square];
                if (piece != Piece.None)
                    key ^= PieceKeys[piece, square];
            }

            key ^= CastlingKeys[board.CastlingRights & 15];
            key ^= EnPassantFileKeys[board.EnPassantSquare == Squares.None ? 8 : Squares.FileOf(board.EnPassantSquare)];

            if (board.SideToMove == PieceColor.Black)
                key ^= SideToMoveKey;

            return key;
        }
    }
}
