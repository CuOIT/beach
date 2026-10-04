using System.Collections.Generic;
using Sq = Chess.Core.Squares;

namespace Chess.Core
{
    /// <summary>
    /// Generates pseudo-legal moves and then filters them by actually playing each one and
    /// testing whether the mover's king is left attacked. Not thread safe: give every
    /// search thread its own instance.
    /// </summary>
    public sealed class MoveGenerator
    {
        private static readonly PieceType[] PromotionPieces =
        {
            PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight
        };

        private readonly List<Move> _pseudoLegal = new List<Move>(256);

        public List<Move> GenerateLegalMoves(Board board, bool capturesOnly = false)
        {
            var moves = new List<Move>(64);
            GenerateLegalMoves(board, moves, capturesOnly);
            return moves;
        }

        public void GenerateLegalMoves(Board board, List<Move> moves, bool capturesOnly = false)
        {
            moves.Clear();
            _pseudoLegal.Clear();
            GeneratePseudoLegal(board, _pseudoLegal, capturesOnly);

            PieceColor us = board.SideToMove;

            // _pseudoLegal is only read in this loop and every nested generator call finishes
            // before we resume, so reusing one buffer per instance is safe.
            for (int i = 0; i < _pseudoLegal.Count; i++)
            {
                Move move = _pseudoLegal[i];
                board.MakeMove(move);
                bool legal = !board.IsInCheck(us);
                board.UnmakeMove();
                if (legal) moves.Add(move);
            }
        }

        public bool HasAnyLegalMove(Board board)
        {
            _pseudoLegal.Clear();
            GeneratePseudoLegal(board, _pseudoLegal, false);

            PieceColor us = board.SideToMove;
            for (int i = 0; i < _pseudoLegal.Count; i++)
            {
                board.MakeMove(_pseudoLegal[i]);
                bool legal = !board.IsInCheck(us);
                board.UnmakeMove();
                if (legal) return true;
            }

            return false;
        }

        private static void GeneratePseudoLegal(Board board, List<Move> moves, bool capturesOnly)
        {
            PieceColor us = board.SideToMove;

            for (int square = 0; square < 64; square++)
            {
                byte piece = board.Squares[square];
                if (piece == Piece.None || !Piece.IsColor(piece, us)) continue;

                switch (Piece.TypeOf(piece))
                {
                    case PieceType.Pawn:
                        GeneratePawnMoves(board, square, us, moves, capturesOnly);
                        break;

                    case PieceType.Knight:
                        GenerateJumpMoves(board, square, us, PrecomputedMoveData.KnightMoves[square], moves, capturesOnly);
                        break;

                    case PieceType.King:
                        GenerateJumpMoves(board, square, us, PrecomputedMoveData.KingMoves[square], moves, capturesOnly);
                        if (!capturesOnly) GenerateCastlingMoves(board, square, us, moves);
                        break;

                    case PieceType.Bishop:
                        GenerateSlidingMoves(board, square, us, 4, 8, moves, capturesOnly);
                        break;

                    case PieceType.Rook:
                        GenerateSlidingMoves(board, square, us, 0, 4, moves, capturesOnly);
                        break;

                    case PieceType.Queen:
                        GenerateSlidingMoves(board, square, us, 0, 8, moves, capturesOnly);
                        break;
                }
            }
        }

        private static void GenerateJumpMoves(Board board, int from, PieceColor us, int[] targets, List<Move> moves, bool capturesOnly)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                int to = targets[i];
                byte occupant = board.Squares[to];

                if (occupant == Piece.None)
                {
                    if (!capturesOnly) moves.Add(new Move(from, to));
                }
                else if (!Piece.IsColor(occupant, us))
                {
                    moves.Add(new Move(from, to, MoveFlags.Capture));
                }
            }
        }

        private static void GenerateSlidingMoves(Board board, int from, PieceColor us, int startDir, int endDir, List<Move> moves, bool capturesOnly)
        {
            for (int dir = startDir; dir < endDir; dir++)
            {
                int distance = PrecomputedMoveData.NumSquaresToEdge[from][dir];
                int offset = PrecomputedMoveData.DirectionOffsets[dir];

                for (int step = 1; step <= distance; step++)
                {
                    int to = from + offset * step;
                    byte occupant = board.Squares[to];

                    if (occupant == Piece.None)
                    {
                        if (!capturesOnly) moves.Add(new Move(from, to));
                        continue;
                    }

                    if (!Piece.IsColor(occupant, us))
                        moves.Add(new Move(from, to, MoveFlags.Capture));

                    break;
                }
            }
        }

        private static void GeneratePawnMoves(Board board, int from, PieceColor us, List<Move> moves, bool capturesOnly)
        {
            bool white = us == PieceColor.White;
            int forward = white ? 8 : -8;
            int startRank = white ? 1 : 6;
            int promotionRank = white ? 7 : 0;

            int oneStep = from + forward;
            if (Sq.IsValid(oneStep) && board.Squares[oneStep] == Piece.None)
            {
                if (Sq.RankOf(oneStep) == promotionRank)
                {
                    // Promotions change material, so quiescence search wants them even when
                    // it only asked for captures.
                    AddPromotions(moves, from, oneStep, MoveFlags.Promotion);
                }
                else if (!capturesOnly)
                {
                    moves.Add(new Move(from, oneStep));

                    if (Sq.RankOf(from) == startRank)
                    {
                        int twoStep = from + forward * 2;
                        if (board.Squares[twoStep] == Piece.None)
                            moves.Add(new Move(from, twoStep, MoveFlags.DoublePawnPush));
                    }
                }
            }

            int[] attacks = PrecomputedMoveData.PawnAttacks[(int)us][from];
            for (int i = 0; i < attacks.Length; i++)
            {
                int to = attacks[i];
                byte occupant = board.Squares[to];

                if (occupant != Piece.None)
                {
                    if (Piece.IsColor(occupant, us)) continue;

                    if (Sq.RankOf(to) == promotionRank)
                        AddPromotions(moves, from, to, MoveFlags.Promotion | MoveFlags.Capture);
                    else
                        moves.Add(new Move(from, to, MoveFlags.Capture));
                }
                else if (to == board.EnPassantSquare)
                {
                    moves.Add(new Move(from, to, MoveFlags.Capture | MoveFlags.EnPassant));
                }
            }
        }

        private static void AddPromotions(List<Move> moves, int from, int to, MoveFlags flags)
        {
            for (int i = 0; i < PromotionPieces.Length; i++)
                moves.Add(new Move(from, to, flags, PromotionPieces[i]));
        }

        /// <summary>
        /// Castling needs the right, an empty path, the rook still home, and the king neither
        /// starting in check nor crossing an attacked square. The destination square is covered
        /// by the ordinary legality filter.
        /// </summary>
        private static void GenerateCastlingMoves(Board board, int kingSquare, PieceColor us, List<Move> moves)
        {
            bool white = us == PieceColor.White;
            int home = white ? 4 : 60;
            if (kingSquare != home) return;

            PieceColor them = Piece.Opposite(us);
            if (board.IsSquareAttacked(kingSquare, them)) return;

            byte rook = Piece.Make(PieceType.Rook, us);

            int kingSideRight = white ? Board.CastleWhiteKing : Board.CastleBlackKing;
            if ((board.CastlingRights & kingSideRight) != 0)
            {
                int rookSquare = white ? 7 : 63;
                int fFile = home + 1;
                int gFile = home + 2;

                if (board.Squares[rookSquare] == rook &&
                    board.Squares[fFile] == Piece.None &&
                    board.Squares[gFile] == Piece.None &&
                    !board.IsSquareAttacked(fFile, them))
                {
                    moves.Add(new Move(kingSquare, gFile, MoveFlags.KingSideCastle));
                }
            }

            int queenSideRight = white ? Board.CastleWhiteQueen : Board.CastleBlackQueen;
            if ((board.CastlingRights & queenSideRight) != 0)
            {
                int rookSquare = white ? 0 : 56;
                int dFile = home - 1;
                int cFile = home - 2;
                int bFile = home - 3;

                if (board.Squares[rookSquare] == rook &&
                    board.Squares[dFile] == Piece.None &&
                    board.Squares[cFile] == Piece.None &&
                    board.Squares[bFile] == Piece.None &&
                    !board.IsSquareAttacked(dFile, them))
                {
                    moves.Add(new Move(kingSquare, cFile, MoveFlags.QueenSideCastle));
                }
            }
        }
    }
}
