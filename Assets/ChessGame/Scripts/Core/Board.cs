using System.Collections.Generic;
using Sq = Chess.Core.Squares;

namespace Chess.Core
{
    /// <summary>
    /// Full chess position with incremental make/unmake. Keeps everything the rules need:
    /// castling rights, en passant target, the halfmove clock and a Zobrist history for repetition.
    /// </summary>
    public sealed class Board
    {
        public const int CastleWhiteKing = 1;
        public const int CastleWhiteQueen = 2;
        public const int CastleBlackKing = 4;
        public const int CastleBlackQueen = 8;
        public const int CastleAll = 15;

        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public readonly byte[] Squares = new byte[64];
        public PieceColor SideToMove = PieceColor.White;
        public int CastlingRights = CastleAll;
        public int EnPassantSquare = Sq.None;
        public int HalfmoveClock;
        public int FullmoveNumber = 1;
        public ulong ZobristKey;

        /// <summary>Indexed by <see cref="PieceColor"/>.</summary>
        public readonly int[] KingSquares = { Sq.None, Sq.None };

        private readonly List<UndoInfo> _undoStack = new List<UndoInfo>(512);
        private readonly List<ulong> _positionHistory = new List<ulong>(512);

        /// <summary>Squares whose occupancy change can revoke castling rights (king and rook home squares).</summary>
        private static readonly int[] CastleRightsMask = BuildCastleRightsMask();

        private struct UndoInfo
        {
            public Move Move;
            public byte MovedPiece;
            public byte CapturedPiece;
            public int CapturedSquare;
            public int CastlingRights;
            public int EnPassantSquare;
            public int HalfmoveClock;
            public ulong ZobristKey;
        }

        private static int[] BuildCastleRightsMask()
        {
            var mask = new int[64];
            for (int i = 0; i < 64; i++) mask[i] = CastleAll;

            mask[Sq.FromAlgebraic("a1")] &= ~CastleWhiteQueen;
            mask[Sq.FromAlgebraic("h1")] &= ~CastleWhiteKing;
            mask[Sq.FromAlgebraic("e1")] &= ~(CastleWhiteKing | CastleWhiteQueen);
            mask[Sq.FromAlgebraic("a8")] &= ~CastleBlackQueen;
            mask[Sq.FromAlgebraic("h8")] &= ~CastleBlackKing;
            mask[Sq.FromAlgebraic("e8")] &= ~(CastleBlackKing | CastleBlackQueen);

            return mask;
        }

        public int PlyCount => _undoStack.Count;

        public Move GetHistoryMove(int index) => _undoStack[index].Move;

        public Move LastMove => _undoStack.Count > 0 ? _undoStack[_undoStack.Count - 1].Move : Move.Null;

        public static Board CreateStartPosition()
        {
            var board = new Board();
            Fen.Load(board, StartFen);
            return board;
        }

        public void Clear()
        {
            for (int i = 0; i < 64; i++) Squares[i] = Piece.None;
            SideToMove = PieceColor.White;
            CastlingRights = 0;
            EnPassantSquare = Sq.None;
            HalfmoveClock = 0;
            FullmoveNumber = 1;
            KingSquares[0] = Sq.None;
            KingSquares[1] = Sq.None;
            _undoStack.Clear();
            _positionHistory.Clear();
            ZobristKey = 0UL;
        }

        /// <summary>Called by the FEN loader once every field of the position has been set.</summary>
        public void FinishSetup()
        {
            KingSquares[0] = Sq.None;
            KingSquares[1] = Sq.None;
            for (int square = 0; square < 64; square++)
            {
                byte piece = Squares[square];
                if (piece != Piece.None && Piece.TypeOf(piece) == PieceType.King)
                    KingSquares[(int)Piece.ColorOf(piece)] = square;
            }

            ZobristKey = Zobrist.Compute(this);
            _undoStack.Clear();
            _positionHistory.Clear();
            _positionHistory.Add(ZobristKey);
        }

        public Board Clone()
        {
            var copy = new Board();
            System.Array.Copy(Squares, copy.Squares, 64);
            copy.SideToMove = SideToMove;
            copy.CastlingRights = CastlingRights;
            copy.EnPassantSquare = EnPassantSquare;
            copy.HalfmoveClock = HalfmoveClock;
            copy.FullmoveNumber = FullmoveNumber;
            copy.ZobristKey = ZobristKey;
            copy.KingSquares[0] = KingSquares[0];
            copy.KingSquares[1] = KingSquares[1];
            copy._positionHistory.AddRange(_positionHistory);
            return copy;
        }

        public void MakeMove(Move move)
        {
            PieceColor us = SideToMove;
            byte moving = Squares[move.From];

            int captureSquare = move.To;
            if (move.IsEnPassant)
                captureSquare = us == PieceColor.White ? move.To - 8 : move.To + 8;

            byte captured = Squares[captureSquare];

            _undoStack.Add(new UndoInfo
            {
                Move = move,
                MovedPiece = moving,
                CapturedPiece = captured,
                CapturedSquare = captureSquare,
                CastlingRights = CastlingRights,
                EnPassantSquare = EnPassantSquare,
                HalfmoveClock = HalfmoveClock,
                ZobristKey = ZobristKey
            });

            if (captured != Piece.None)
            {
                Squares[captureSquare] = Piece.None;
                ZobristKey ^= Zobrist.PieceKeys[captured, captureSquare];
            }

            Squares[move.From] = Piece.None;
            ZobristKey ^= Zobrist.PieceKeys[moving, move.From];

            byte placed = move.IsPromotion ? Piece.Make(move.Promotion, us) : moving;
            Squares[move.To] = placed;
            ZobristKey ^= Zobrist.PieceKeys[placed, move.To];

            if (move.IsKingSideCastle)
                MoveRook(us == PieceColor.White ? 7 : 63, us == PieceColor.White ? 5 : 61);
            else if (move.IsQueenSideCastle)
                MoveRook(us == PieceColor.White ? 0 : 56, us == PieceColor.White ? 3 : 59);

            if (Piece.TypeOf(moving) == PieceType.King)
                KingSquares[(int)us] = move.To;

            ZobristKey ^= Zobrist.CastlingKeys[CastlingRights];
            CastlingRights &= CastleRightsMask[move.From];
            CastlingRights &= CastleRightsMask[move.To];
            ZobristKey ^= Zobrist.CastlingKeys[CastlingRights];

            ZobristKey ^= EnPassantKey(EnPassantSquare);
            EnPassantSquare = move.IsDoublePawnPush
                ? (us == PieceColor.White ? move.From + 8 : move.From - 8)
                : Sq.None;
            ZobristKey ^= EnPassantKey(EnPassantSquare);

            if (Piece.TypeOf(moving) == PieceType.Pawn || captured != Piece.None)
                HalfmoveClock = 0;
            else
                HalfmoveClock++;

            if (us == PieceColor.Black) FullmoveNumber++;

            SideToMove = Piece.Opposite(us);
            ZobristKey ^= Zobrist.SideToMoveKey;

            _positionHistory.Add(ZobristKey);
        }

        public void UnmakeMove()
        {
            if (_undoStack.Count == 0) return;

            UndoInfo undo = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _positionHistory.RemoveAt(_positionHistory.Count - 1);

            Move move = undo.Move;
            PieceColor us = Piece.Opposite(SideToMove);
            SideToMove = us;

            Squares[move.To] = Piece.None;
            Squares[move.From] = undo.MovedPiece;

            if (move.IsKingSideCastle)
                MoveRookRaw(us == PieceColor.White ? 5 : 61, us == PieceColor.White ? 7 : 63);
            else if (move.IsQueenSideCastle)
                MoveRookRaw(us == PieceColor.White ? 3 : 59, us == PieceColor.White ? 0 : 56);

            if (undo.CapturedPiece != Piece.None)
                Squares[undo.CapturedSquare] = undo.CapturedPiece;

            if (Piece.TypeOf(undo.MovedPiece) == PieceType.King)
                KingSquares[(int)us] = move.From;

            CastlingRights = undo.CastlingRights;
            EnPassantSquare = undo.EnPassantSquare;
            HalfmoveClock = undo.HalfmoveClock;
            ZobristKey = undo.ZobristKey;

            if (us == PieceColor.Black) FullmoveNumber--;
        }

        private static ulong EnPassantKey(int square)
        {
            return Zobrist.EnPassantFileKeys[square == Sq.None ? 8 : Sq.FileOf(square)];
        }

        private void MoveRook(int from, int to)
        {
            byte rook = Squares[from];
            Squares[from] = Piece.None;
            Squares[to] = rook;
            ZobristKey ^= Zobrist.PieceKeys[rook, from];
            ZobristKey ^= Zobrist.PieceKeys[rook, to];
        }

        private void MoveRookRaw(int from, int to)
        {
            Squares[to] = Squares[from];
            Squares[from] = Piece.None;
        }

        public bool IsInCheck(PieceColor color)
        {
            int king = KingSquares[(int)color];
            if (king == Sq.None) return false;
            return IsSquareAttacked(king, Piece.Opposite(color));
        }

        /// <summary>True when <paramref name="byColor"/> attacks <paramref name="square"/>, ignoring pins.</summary>
        public bool IsSquareAttacked(int square, PieceColor byColor)
        {
            // A pawn of byColor attacks this square from exactly the squares an enemy pawn
            // standing here would attack, so the opposite colour's table doubles as an origin list.
            int[] pawnOrigins = PrecomputedMoveData.PawnAttacks[(int)Piece.Opposite(byColor)][square];
            byte pawn = Piece.Make(PieceType.Pawn, byColor);
            for (int i = 0; i < pawnOrigins.Length; i++)
                if (Squares[pawnOrigins[i]] == pawn) return true;

            byte knight = Piece.Make(PieceType.Knight, byColor);
            int[] knightOrigins = PrecomputedMoveData.KnightMoves[square];
            for (int i = 0; i < knightOrigins.Length; i++)
                if (Squares[knightOrigins[i]] == knight) return true;

            byte king = Piece.Make(PieceType.King, byColor);
            int[] kingOrigins = PrecomputedMoveData.KingMoves[square];
            for (int i = 0; i < kingOrigins.Length; i++)
                if (Squares[kingOrigins[i]] == king) return true;

            byte queen = Piece.Make(PieceType.Queen, byColor);
            byte rook = Piece.Make(PieceType.Rook, byColor);
            byte bishop = Piece.Make(PieceType.Bishop, byColor);

            for (int dir = 0; dir < 8; dir++)
            {
                bool orthogonal = dir < 4;
                int distance = PrecomputedMoveData.NumSquaresToEdge[square][dir];
                int offset = PrecomputedMoveData.DirectionOffsets[dir];

                for (int step = 1; step <= distance; step++)
                {
                    byte piece = Squares[square + offset * step];
                    if (piece == Piece.None) continue;
                    if (piece == queen) return true;
                    if (orthogonal ? piece == rook : piece == bishop) return true;
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// How many times the current position has occurred, counting the present one.
        /// Only positions since the last irreversible move can repeat, so the scan stops there.
        /// </summary>
        public int RepetitionCount()
        {
            ulong key = ZobristKey;
            int count = 0;
            int oldest = _positionHistory.Count - 1 - HalfmoveClock;
            if (oldest < 0) oldest = 0;

            for (int i = _positionHistory.Count - 1; i >= oldest; i -= 2)
                if (_positionHistory[i] == key) count++;

            return count;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int rank = 7; rank >= 0; rank--)
            {
                sb.Append(rank + 1).Append(' ');
                for (int file = 0; file < 8; file++)
                    sb.Append(Piece.ToChar(Squares[Sq.At(file, rank)])).Append(' ');
                sb.AppendLine();
            }
            sb.AppendLine("  a b c d e f g h");
            sb.Append(Fen.Save(this));
            return sb.ToString();
        }
    }
}
