namespace Chess.Core
{
    public enum PieceType : byte
    {
        None = 0, Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6
    }

    public enum PieceColor : byte { White = 0, Black = 1 }

    /// <summary>
    /// Pieces are packed into a single byte: bits 0-2 hold the <see cref="PieceType"/>,
    /// bit 3 marks white and bit 4 marks black. 0 means an empty square.
    /// </summary>
    public static class Piece
    {
        public const byte None = 0;
        public const byte Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6;
        public const byte White = 8, Black = 16;

        public const byte TypeMask = 0b00111;
        public const byte ColorMask = 0b11000;

        public static byte Make(PieceType type, PieceColor color)
        {
            return (byte)((byte)type | (color == PieceColor.White ? White : Black));
        }

        public static PieceType TypeOf(byte piece) => (PieceType)(piece & TypeMask);

        public static PieceColor ColorOf(byte piece) => (piece & White) != 0 ? PieceColor.White : PieceColor.Black;

        public static bool IsColor(byte piece, PieceColor color)
        {
            if (piece == None) return false;
            return color == PieceColor.White ? (piece & White) != 0 : (piece & Black) != 0;
        }

        public static bool IsSlidingPiece(byte piece)
        {
            PieceType t = TypeOf(piece);
            return t == PieceType.Bishop || t == PieceType.Rook || t == PieceType.Queen;
        }

        public static PieceColor Opposite(PieceColor color)
        {
            return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        }

        public static char ToChar(byte piece)
        {
            char c;
            switch (TypeOf(piece))
            {
                case PieceType.Pawn: c = 'p'; break;
                case PieceType.Knight: c = 'n'; break;
                case PieceType.Bishop: c = 'b'; break;
                case PieceType.Rook: c = 'r'; break;
                case PieceType.Queen: c = 'q'; break;
                case PieceType.King: c = 'k'; break;
                default: return '.';
            }
            return IsColor(piece, PieceColor.White) ? char.ToUpperInvariant(c) : c;
        }

        public static byte FromChar(char c)
        {
            PieceColor color = char.IsUpper(c) ? PieceColor.White : PieceColor.Black;
            PieceType type;
            switch (char.ToLowerInvariant(c))
            {
                case 'p': type = PieceType.Pawn; break;
                case 'n': type = PieceType.Knight; break;
                case 'b': type = PieceType.Bishop; break;
                case 'r': type = PieceType.Rook; break;
                case 'q': type = PieceType.Queen; break;
                case 'k': type = PieceType.King; break;
                default: return None;
            }
            return Make(type, color);
        }
    }
}
