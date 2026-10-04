using System.Collections.Generic;

namespace Chess.Core
{
    /// <summary>
    /// Move-generation counter. Comparing these node counts against published values is the
    /// standard way to prove that every rule - castling, en passant, promotion, pins - is exact.
    /// </summary>
    public static class Perft
    {
        public static long Run(Board board, int depth)
        {
            var generator = new MoveGenerator();
            return Run(board, depth, generator);
        }

        private static long Run(Board board, int depth, MoveGenerator generator)
        {
            if (depth == 0) return 1L;

            var moves = new List<Move>(64);
            generator.GenerateLegalMoves(board, moves);

            if (depth == 1) return moves.Count;

            long nodes = 0L;
            for (int i = 0; i < moves.Count; i++)
            {
                board.MakeMove(moves[i]);
                nodes += Run(board, depth - 1, generator);
                board.UnmakeMove();
            }

            return nodes;
        }

        /// <summary>Per-root-move node counts, the usual way to bisect a move generation bug.</summary>
        public static Dictionary<string, long> Divide(Board board, int depth)
        {
            var result = new Dictionary<string, long>();
            if (depth <= 0) return result;

            var generator = new MoveGenerator();
            var moves = generator.GenerateLegalMoves(board);

            foreach (Move move in moves)
            {
                board.MakeMove(move);
                result[move.ToUci()] = Run(board, depth - 1, generator);
                board.UnmakeMove();
            }

            return result;
        }
    }
}
