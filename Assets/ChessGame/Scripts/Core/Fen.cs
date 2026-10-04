using System.Text;
using Sq = Chess.Core.Squares;

namespace Chess.Core
{
    /// <summary>Forsyth-Edwards Notation reader/writer, used for setup, tests and save games.</summary>
    public static class Fen
    {
        public static bool Load(Board board, string fen)
        {
            if (string.IsNullOrWhiteSpace(fen)) return false;

            string[] parts = fen.Trim().Split(' ');
            if (parts.Length < 1) return false;

            board.Clear();

            int file = 0;
            int rank = 7;
            foreach (char c in parts[0])
            {
                if (c == '/')
                {
                    file = 0;
                    rank--;
                    continue;
                }

                if (char.IsDigit(c))
                {
                    file += c - '0';
                    continue;
                }

                if (rank < 0 || rank > 7 || file < 0 || file > 7) return false;

                byte piece = Piece.FromChar(c);
                if (piece == Piece.None) return false;

                board.Squares[Sq.At(file, rank)] = piece;
                file++;
            }

            board.SideToMove = parts.Length > 1 && parts[1] == "b" ? PieceColor.Black : PieceColor.White;

            board.CastlingRights = 0;
            if (parts.Length > 2 && parts[2] != "-")
            {
                foreach (char c in parts[2])
                {
                    switch (c)
                    {
                        case 'K': board.CastlingRights |= Board.CastleWhiteKing; break;
                        case 'Q': board.CastlingRights |= Board.CastleWhiteQueen; break;
                        case 'k': board.CastlingRights |= Board.CastleBlackKing; break;
                        case 'q': board.CastlingRights |= Board.CastleBlackQueen; break;
                    }
                }
            }

            board.EnPassantSquare = parts.Length > 3 && parts[3] != "-"
                ? Sq.FromAlgebraic(parts[3])
                : Sq.None;

            board.HalfmoveClock = parts.Length > 4 && int.TryParse(parts[4], out int half) ? half : 0;
            board.FullmoveNumber = parts.Length > 5 && int.TryParse(parts[5], out int full) ? full : 1;

            board.FinishSetup();
            return true;
        }

        public static string Save(Board board)
        {
            var sb = new StringBuilder();

            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    byte piece = board.Squares[Sq.At(file, rank)];
                    if (piece == Piece.None)
                    {
                        empty++;
                        continue;
                    }

                    if (empty > 0)
                    {
                        sb.Append(empty);
                        empty = 0;
                    }
                    sb.Append(Piece.ToChar(piece));
                }

                if (empty > 0) sb.Append(empty);
                if (rank > 0) sb.Append('/');
            }

            sb.Append(' ').Append(board.SideToMove == PieceColor.White ? 'w' : 'b');

            sb.Append(' ');
            if (board.CastlingRights == 0)
            {
                sb.Append('-');
            }
            else
            {
                if ((board.CastlingRights & Board.CastleWhiteKing) != 0) sb.Append('K');
                if ((board.CastlingRights & Board.CastleWhiteQueen) != 0) sb.Append('Q');
                if ((board.CastlingRights & Board.CastleBlackKing) != 0) sb.Append('k');
                if ((board.CastlingRights & Board.CastleBlackQueen) != 0) sb.Append('q');
            }

            sb.Append(' ').Append(board.EnPassantSquare == Sq.None ? "-" : Sq.ToAlgebraic(board.EnPassantSquare));
            sb.Append(' ').Append(board.HalfmoveClock);
            sb.Append(' ').Append(board.FullmoveNumber);

            return sb.ToString();
        }
    }
}
