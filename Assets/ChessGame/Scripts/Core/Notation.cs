using System.Collections.Generic;
using System.Text;
using Sq = Chess.Core.Squares;

namespace Chess.Core
{
    /// <summary>Standard Algebraic Notation, used for the in-game move list.</summary>
    public static class Notation
    {
        /// <summary>
        /// Builds SAN for <paramref name="move"/> in the position <paramref name="board"/> is
        /// currently in, so call it before the move is played. <paramref name="legalMoves"/> must
        /// be the legal moves of that same position: they drive the disambiguation.
        /// </summary>
        public static string ToSan(Board board, Move move, IList<Move> legalMoves, MoveGenerator generator)
        {
            var sb = new StringBuilder(8);

            if (move.IsKingSideCastle) sb.Append("O-O");
            else if (move.IsQueenSideCastle) sb.Append("O-O-O");
            else
            {
                byte piece = board.Squares[move.From];
                PieceType type = Piece.TypeOf(piece);

                if (type == PieceType.Pawn)
                {
                    if (move.IsCapture)
                        sb.Append((char)('a' + Sq.FileOf(move.From))).Append('x');
                }
                else
                {
                    sb.Append(PieceLetter(type));
                    sb.Append(Disambiguation(board, move, type, legalMoves));
                    if (move.IsCapture) sb.Append('x');
                }

                sb.Append(Sq.ToAlgebraic(move.To));

                if (move.IsPromotion)
                    sb.Append('=').Append(PieceLetter(move.Promotion));
            }

            sb.Append(CheckSuffix(board, move, generator));
            return sb.ToString();
        }

        private static char PieceLetter(PieceType type)
        {
            switch (type)
            {
                case PieceType.Knight: return 'N';
                case PieceType.Bishop: return 'B';
                case PieceType.Rook: return 'R';
                case PieceType.Queen: return 'Q';
                case PieceType.King: return 'K';
                default: return 'P';
            }
        }

        /// <summary>
        /// Adds the least amount of origin information that still identifies the piece:
        /// file if that is unique, else rank, else both.
        /// </summary>
        private static string Disambiguation(Board board, Move move, PieceType type, IList<Move> legalMoves)
        {
            bool needed = false;
            bool sameFile = false;
            bool sameRank = false;

            for (int i = 0; i < legalMoves.Count; i++)
            {
                Move other = legalMoves[i];
                if (other.From == move.From || other.To != move.To) continue;
                if (Piece.TypeOf(board.Squares[other.From]) != type) continue;

                needed = true;
                if (Sq.FileOf(other.From) == Sq.FileOf(move.From)) sameFile = true;
                if (Sq.RankOf(other.From) == Sq.RankOf(move.From)) sameRank = true;
            }

            if (!needed) return string.Empty;
            if (!sameFile) return ((char)('a' + Sq.FileOf(move.From))).ToString();
            if (!sameRank) return ((char)('1' + Sq.RankOf(move.From))).ToString();
            return Sq.ToAlgebraic(move.From);
        }

        private static string CheckSuffix(Board board, Move move, MoveGenerator generator)
        {
            board.MakeMove(move);

            string suffix = string.Empty;
            if (board.IsInCheck(board.SideToMove))
                suffix = generator.HasAnyLegalMove(board) ? "+" : "#";

            board.UnmakeMove();
            return suffix;
        }
    }
}
