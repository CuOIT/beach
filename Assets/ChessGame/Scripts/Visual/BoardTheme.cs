using UnityEngine;

namespace Chess.Visual
{
    /// <summary>Colour scheme for the board, the pieces and the overlay highlights.</summary>
    public struct BoardTheme
    {
        public string Name;

        public Color LightSquare;
        public Color DarkSquare;
        public Color Frame;
        public Color Table;

        public Color WhitePiece;
        public Color BlackPiece;

        public Color Selection;
        public Color LegalMove;
        public Color CaptureMove;
        public Color LastMove;
        public Color Check;

        public Color AmbientSky;
        public Color AmbientGround;

        public static readonly BoardTheme[] All =
        {
            Classic,
            Emerald,
            Midnight
        };

        public static BoardTheme Get(int index)
        {
            if (index < 0 || index >= All.Length) index = 0;
            return All[index];
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        public static BoardTheme Classic => new BoardTheme
        {
            Name = "Classic",
            LightSquare = Hex("#EFD9B4"),
            DarkSquare = Hex("#9C6B43"),
            Frame = Hex("#4E3524"),
            Table = Hex("#23201E"),
            WhitePiece = Hex("#F5EFE2"),
            BlackPiece = Hex("#3B3733"),
            Selection = Hex("#FFD24A"),
            LegalMove = Hex("#5FD98A"),
            CaptureMove = Hex("#FF6B5E"),
            LastMove = Hex("#7FB3FF"),
            Check = Hex("#FF4040"),
            AmbientSky = Hex("#7E8FA6"),
            AmbientGround = Hex("#3A3330")
        };

        public static BoardTheme Emerald => new BoardTheme
        {
            Name = "Emerald",
            LightSquare = Hex("#EDEED3"),
            DarkSquare = Hex("#4E7A45"),
            Frame = Hex("#2C4429"),
            Table = Hex("#1B231C"),
            WhitePiece = Hex("#F7F4E8"),
            BlackPiece = Hex("#35443B"),
            Selection = Hex("#FFD24A"),
            LegalMove = Hex("#8CE6B0"),
            CaptureMove = Hex("#FF7A6B"),
            LastMove = Hex("#9AD1FF"),
            Check = Hex("#FF4A4A"),
            AmbientSky = Hex("#7FA08C"),
            AmbientGround = Hex("#2B3330")
        };

        public static BoardTheme Midnight => new BoardTheme
        {
            Name = "Midnight",
            LightSquare = Hex("#C9D2E3"),
            DarkSquare = Hex("#4A5878"),
            Frame = Hex("#232B3D"),
            Table = Hex("#12151F"),
            WhitePiece = Hex("#E8EDF7"),
            BlackPiece = Hex("#2E3647"),
            Selection = Hex("#FFC94A"),
            LegalMove = Hex("#66E0C8"),
            CaptureMove = Hex("#FF6E8A"),
            LastMove = Hex("#8FB8FF"),
            Check = Hex("#FF5470"),
            AmbientSky = Hex("#6E7C9E"),
            AmbientGround = Hex("#20242F")
        };
    }
}
