using System.Collections.Generic;

namespace Chess.Core
{
    public enum GameResult
    {
        InProgress = 0,
        WhiteCheckmated = 1,
        BlackCheckmated = 2,
        Stalemate = 3,
        FiftyMoveRule = 4,
        ThreefoldRepetition = 5,
        InsufficientMaterial = 6
    }

    /// <summary>Decides when a game is over and why.</summary>
    public static class Arbiter
    {
        public static bool IsGameOver(GameResult result) => result != GameResult.InProgress;

        public static bool IsDraw(GameResult result)
        {
            return result == GameResult.Stalemate
                || result == GameResult.FiftyMoveRule
                || result == GameResult.ThreefoldRepetition
                || result == GameResult.InsufficientMaterial;
        }

        /// <summary>The winning side, or null for a draw or an unfinished game.</summary>
        public static PieceColor? Winner(GameResult result)
        {
            if (result == GameResult.WhiteCheckmated) return PieceColor.Black;
            if (result == GameResult.BlackCheckmated) return PieceColor.White;
            return null;
        }

        /// <summary>
        /// Checkmate and stalemate are decided first: a mate on the 100th halfmove is still a mate.
        /// </summary>
        public static GameResult GetResult(Board board, IList<Move> legalMoves)
        {
            if (legalMoves.Count == 0)
            {
                if (board.IsInCheck(board.SideToMove))
                    return board.SideToMove == PieceColor.White ? GameResult.WhiteCheckmated : GameResult.BlackCheckmated;

                return GameResult.Stalemate;
            }

            if (board.HalfmoveClock >= 100) return GameResult.FiftyMoveRule;
            if (board.RepetitionCount() >= 3) return GameResult.ThreefoldRepetition;
            if (HasInsufficientMaterial(board)) return GameResult.InsufficientMaterial;

            return GameResult.InProgress;
        }

        /// <summary>
        /// Dead positions where no sequence of legal moves can deliver mate:
        /// bare kings, king plus a single minor, or king and bishop each on same-coloured bishops.
        /// </summary>
        public static bool HasInsufficientMaterial(Board board)
        {
            int whiteKnights = 0, whiteBishops = 0;
            int blackKnights = 0, blackBishops = 0;
            int whiteBishopSquareColor = -1, blackBishopSquareColor = -1;

            for (int square = 0; square < 64; square++)
            {
                byte piece = board.Squares[square];
                if (piece == Piece.None) continue;

                PieceType type = Piece.TypeOf(piece);
                if (type == PieceType.King) continue;

                // Any pawn, rook or queen means mate is still constructible.
                if (type == PieceType.Pawn || type == PieceType.Rook || type == PieceType.Queen)
                    return false;

                bool white = Piece.IsColor(piece, PieceColor.White);

                if (type == PieceType.Knight)
                {
                    if (white) whiteKnights++; else blackKnights++;
                }
                else if (type == PieceType.Bishop)
                {
                    int squareColor = Squares.IsLightSquare(square) ? 1 : 0;
                    if (white)
                    {
                        whiteBishops++;
                        whiteBishopSquareColor = squareColor;
                    }
                    else
                    {
                        blackBishops++;
                        blackBishopSquareColor = squareColor;
                    }
                }
            }

            int whiteMinors = whiteKnights + whiteBishops;
            int blackMinors = blackKnights + blackBishops;
            int totalMinors = whiteMinors + blackMinors;

            if (totalMinors == 0) return true;                       // K vs K
            if (totalMinors == 1) return true;                       // K+minor vs K

            if (totalMinors == 2 && whiteBishops == 1 && blackBishops == 1)
                return whiteBishopSquareColor == blackBishopSquareColor;  // same-coloured bishops

            return false;
        }

        public static string Describe(GameResult result)
        {
            switch (result)
            {
                case GameResult.WhiteCheckmated: return "Checkmate - Black wins";
                case GameResult.BlackCheckmated: return "Checkmate - White wins";
                case GameResult.Stalemate: return "Draw - stalemate";
                case GameResult.FiftyMoveRule: return "Draw - fifty-move rule";
                case GameResult.ThreefoldRepetition: return "Draw - threefold repetition";
                case GameResult.InsufficientMaterial: return "Draw - insufficient material";
                default: return "In progress";
            }
        }
    }
}
