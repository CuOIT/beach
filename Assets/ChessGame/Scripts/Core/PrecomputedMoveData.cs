using System.Collections.Generic;

namespace Chess.Core
{
    /// <summary>
    /// Static lookup tables built once on first use: ray lengths for sliding pieces and
    /// explicit target lists for knights, kings and pawn attacks.
    /// </summary>
    public static class PrecomputedMoveData
    {
        /// <summary>N, S, W, E, NW, SE, NE, SW. Indices 0-3 are rook rays, 4-7 are bishop rays.</summary>
        public static readonly int[] DirectionOffsets = { 8, -8, -1, 1, 7, -7, 9, -9 };

        public const int DirNorth = 0, DirSouth = 1, DirWest = 2, DirEast = 3;
        public const int DirNorthWest = 4, DirSouthEast = 5, DirNorthEast = 6, DirSouthWest = 7;

        /// <summary>NumSquaresToEdge[square][direction] = how many squares can be travelled before leaving the board.</summary>
        public static readonly int[][] NumSquaresToEdge = new int[64][];

        public static readonly int[][] KnightMoves = new int[64][];
        public static readonly int[][] KingMoves = new int[64][];

        /// <summary>PawnAttacks[color][square] = squares that a pawn of that colour standing on <c>square</c> attacks.</summary>
        public static readonly int[][][] PawnAttacks = new int[2][][];

        static PrecomputedMoveData()
        {
            PawnAttacks[0] = new int[64][];
            PawnAttacks[1] = new int[64][];

            for (int square = 0; square < 64; square++)
            {
                int file = Squares.FileOf(square);
                int rank = Squares.RankOf(square);

                int north = 7 - rank;
                int south = rank;
                int west = file;
                int east = 7 - file;

                NumSquaresToEdge[square] = new int[8];
                NumSquaresToEdge[square][DirNorth] = north;
                NumSquaresToEdge[square][DirSouth] = south;
                NumSquaresToEdge[square][DirWest] = west;
                NumSquaresToEdge[square][DirEast] = east;
                NumSquaresToEdge[square][DirNorthWest] = north < west ? north : west;
                NumSquaresToEdge[square][DirSouthEast] = south < east ? south : east;
                NumSquaresToEdge[square][DirNorthEast] = north < east ? north : east;
                NumSquaresToEdge[square][DirSouthWest] = south < west ? south : west;

                KnightMoves[square] = BuildOffsetMoves(file, rank, KnightDeltas);
                KingMoves[square] = BuildOffsetMoves(file, rank, KingDeltas);

                PawnAttacks[(int)PieceColor.White][square] = BuildOffsetMoves(file, rank, WhitePawnAttackDeltas);
                PawnAttacks[(int)PieceColor.Black][square] = BuildOffsetMoves(file, rank, BlackPawnAttackDeltas);
            }
        }

        private static readonly int[,] KnightDeltas =
        {
            { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 },
            { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 }
        };

        private static readonly int[,] KingDeltas =
        {
            { 0, 1 }, { 1, 1 }, { 1, 0 }, { 1, -1 },
            { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }
        };

        private static readonly int[,] WhitePawnAttackDeltas = { { -1, 1 }, { 1, 1 } };
        private static readonly int[,] BlackPawnAttackDeltas = { { -1, -1 }, { 1, -1 } };

        private static int[] BuildOffsetMoves(int file, int rank, int[,] deltas)
        {
            var result = new List<int>(8);
            for (int i = 0; i < deltas.GetLength(0); i++)
            {
                int f = file + deltas[i, 0];
                int r = rank + deltas[i, 1];
                if (f >= 0 && f <= 7 && r >= 0 && r <= 7)
                    result.Add(Squares.At(f, r));
            }
            return result.ToArray();
        }
    }
}
