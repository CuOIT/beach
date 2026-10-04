using Chess.Core;
using Sq = Chess.Core.Squares;

namespace Chess.AI
{
    /// <summary>
    /// Static position evaluation in centipawns. Material and piece-square tables carry most of
    /// the weight, with pawn structure, the bishop pair and rook placement on top. Pawn and king
    /// tables are tapered between a middlegame and an endgame set based on remaining material.
    /// </summary>
    public static class Evaluation
    {
        public const int PawnValue = 100;
        public const int KnightValue = 320;
        public const int BishopValue = 330;
        public const int RookValue = 500;
        public const int QueenValue = 900;

        private const int BishopPairBonus = 30;
        private const int DoubledPawnPenalty = 12;
        private const int IsolatedPawnPenalty = 18;
        private const int RookOpenFileBonus = 18;
        private const int RookSemiOpenFileBonus = 9;
        private const int TempoBonus = 8;

        /// <summary>Bonus for a passed pawn, indexed by how many ranks it has advanced.</summary>
        private static readonly int[] PassedPawnBonus = { 0, 8, 16, 28, 48, 80, 130, 0 };

        private const int TotalPhase = 24;

        public static int PieceValue(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return PawnValue;
                case PieceType.Knight: return KnightValue;
                case PieceType.Bishop: return BishopValue;
                case PieceType.Rook: return RookValue;
                case PieceType.Queen: return QueenValue;
                default: return 0;
            }
        }

        private static int PhaseWeight(PieceType type)
        {
            switch (type)
            {
                case PieceType.Knight:
                case PieceType.Bishop: return 1;
                case PieceType.Rook: return 2;
                case PieceType.Queen: return 4;
                default: return 0;
            }
        }

        /// <summary>Score from the point of view of the side to move, as negamax expects.</summary>
        public static int Evaluate(Board board)
        {
            int middlegame = 0;
            int endgame = 0;
            int phase = 0;

            // [color][file]
            var pawnsPerFile = new int[2][];
            pawnsPerFile[0] = new int[8];
            pawnsPerFile[1] = new int[8];

            int whiteBishops = 0, blackBishops = 0;

            for (int square = 0; square < 64; square++)
            {
                byte piece = board.Squares[square];
                if (piece == Piece.None) continue;

                PieceType type = Piece.TypeOf(piece);
                if (type == PieceType.Pawn)
                {
                    PieceColor c = Piece.ColorOf(piece);
                    pawnsPerFile[(int)c][Sq.FileOf(square)]++;
                }
                else if (type == PieceType.Bishop)
                {
                    if (Piece.IsColor(piece, PieceColor.White)) whiteBishops++; else blackBishops++;
                }

                phase += PhaseWeight(type);
            }

            for (int square = 0; square < 64; square++)
            {
                byte piece = board.Squares[square];
                if (piece == Piece.None) continue;

                PieceColor color = Piece.ColorOf(piece);
                PieceType type = Piece.TypeOf(piece);
                int sign = color == PieceColor.White ? 1 : -1;

                int material = PieceValue(type);
                int mg = material;
                int eg = material;

                switch (type)
                {
                    case PieceType.Pawn:
                        mg += PieceSquareTables.Read(PieceSquareTables.PawnMiddle, square, color);
                        eg += PieceSquareTables.Read(PieceSquareTables.PawnEnd, square, color);

                        int structure = PawnStructureScore(board, square, color, pawnsPerFile);
                        mg += structure;
                        eg += structure;
                        break;

                    case PieceType.Knight:
                        mg += PieceSquareTables.Read(PieceSquareTables.Knight, square, color);
                        eg = mg;
                        break;

                    case PieceType.Bishop:
                        mg += PieceSquareTables.Read(PieceSquareTables.Bishop, square, color);
                        eg = mg;
                        break;

                    case PieceType.Rook:
                        mg += PieceSquareTables.Read(PieceSquareTables.Rook, square, color);
                        int fileBonus = RookFileScore(square, color, pawnsPerFile);
                        mg += fileBonus;
                        eg = mg;
                        break;

                    case PieceType.Queen:
                        mg += PieceSquareTables.Read(PieceSquareTables.Queen, square, color);
                        eg = mg;
                        break;

                    case PieceType.King:
                        mg = PieceSquareTables.Read(PieceSquareTables.KingMiddle, square, color);
                        eg = PieceSquareTables.Read(PieceSquareTables.KingEnd, square, color);
                        break;
                }

                middlegame += sign * mg;
                endgame += sign * eg;
            }

            if (whiteBishops >= 2)
            {
                middlegame += BishopPairBonus;
                endgame += BishopPairBonus;
            }
            if (blackBishops >= 2)
            {
                middlegame -= BishopPairBonus;
                endgame -= BishopPairBonus;
            }

            if (phase > TotalPhase) phase = TotalPhase;
            int score = (middlegame * phase + endgame * (TotalPhase - phase)) / TotalPhase;

            if (board.SideToMove == PieceColor.Black) score = -score;
            return score + TempoBonus;
        }

        private static int PawnStructureScore(Board board, int square, PieceColor color, int[][] pawnsPerFile)
        {
            int file = Sq.FileOf(square);
            int rank = Sq.RankOf(square);
            int score = 0;

            if (pawnsPerFile[(int)color][file] > 1)
                score -= DoubledPawnPenalty;

            bool hasNeighbour =
                (file > 0 && pawnsPerFile[(int)color][file - 1] > 0) ||
                (file < 7 && pawnsPerFile[(int)color][file + 1] > 0);
            if (!hasNeighbour)
                score -= IsolatedPawnPenalty;

            if (IsPassedPawn(board, square, color))
            {
                int advanced = color == PieceColor.White ? rank - 1 : 6 - rank;
                if (advanced < 0) advanced = 0;
                if (advanced > 7) advanced = 7;
                score += PassedPawnBonus[advanced];
            }

            return score;
        }

        /// <summary>No enemy pawn stands ahead of it on its own or an adjacent file.</summary>
        private static bool IsPassedPawn(Board board, int square, PieceColor color)
        {
            int file = Sq.FileOf(square);
            int rank = Sq.RankOf(square);
            byte enemyPawn = Piece.Make(PieceType.Pawn, Piece.Opposite(color));
            int step = color == PieceColor.White ? 1 : -1;

            for (int f = file - 1; f <= file + 1; f++)
            {
                if (f < 0 || f > 7) continue;

                for (int r = rank + step; r >= 0 && r <= 7; r += step)
                {
                    if (board.Squares[Sq.At(f, r)] == enemyPawn) return false;
                }
            }

            return true;
        }

        private static int RookFileScore(int square, PieceColor color, int[][] pawnsPerFile)
        {
            int file = Sq.FileOf(square);
            int own = pawnsPerFile[(int)color][file];
            int enemy = pawnsPerFile[(int)Piece.Opposite(color)][file];

            if (own == 0 && enemy == 0) return RookOpenFileBonus;
            if (own == 0) return RookSemiOpenFileBonus;
            return 0;
        }
    }
}
