using System.Collections.Generic;
using Chess.Core;
using Chess.Visual;
using UnityEngine;

namespace Chess.Game
{
    /// <summary>
    /// Keeps the visible pieces in step with the logical board. Ordinary moves are animated
    /// incrementally; anything harder to reason about incrementally (a new game, an undo) goes
    /// through a full rebuild, which is cheap at 32 objects and cannot drift out of sync.
    /// </summary>
    public sealed class PiecesView : MonoBehaviour
    {
        private readonly PieceView[] _bySquare = new PieceView[64];
        private readonly List<PieceView> _all = new List<PieceView>(32);

        private Material _whiteMaterial;
        private Material _blackMaterial;
        private Transform _root;

        public bool IsAnimating
        {
            get
            {
                for (int i = 0; i < _all.Count; i++)
                {
                    PieceView piece = _all[i];
                    if (piece != null && piece.IsAnimating) return true;
                }
                return false;
            }
        }

        public void ApplyTheme(BoardTheme theme)
        {
            _whiteMaterial = MaterialLibrary.CreateOpaque("WhitePiece", theme.WhitePiece, 0.45f, 0.05f);
            _blackMaterial = MaterialLibrary.CreateOpaque("BlackPiece", theme.BlackPiece, 0.62f, 0.12f);
        }

        public PieceView PieceAt(int square)
        {
            return Squares.IsValid(square) ? _bySquare[square] : null;
        }

        public void Rebuild(Board board)
        {
            if (_root != null) Destroy(_root.gameObject);

            _root = new GameObject("Pieces").transform;
            _root.SetParent(transform, false);

            _all.Clear();
            for (int i = 0; i < 64; i++) _bySquare[i] = null;

            for (int square = 0; square < 64; square++)
            {
                byte piece = board.Squares[square];
                if (piece == Piece.None) continue;

                Spawn(Piece.TypeOf(piece), Piece.ColorOf(piece), square);
            }
        }

        private PieceView Spawn(PieceType type, PieceColor color, int square)
        {
            var holder = new GameObject(color + " " + type);
            holder.transform.SetParent(_root, false);

            var view = holder.AddComponent<PieceView>();
            view.Initialise(
                type,
                color,
                square,
                PieceMeshLibrary.Get(type),
                color == PieceColor.White ? _whiteMaterial : _blackMaterial);

            _bySquare[square] = view;
            _all.Add(view);
            return view;
        }

        /// <summary>
        /// Animates one played move. <paramref name="capturedSquare"/> is the square the taken
        /// piece actually stood on, which differs from the destination for en passant.
        /// </summary>
        public void ApplyMove(Move move, PieceType movedType, int capturedSquare, float duration)
        {
            if (capturedSquare >= 0)
            {
                PieceView victim = _bySquare[capturedSquare];
                if (victim != null)
                {
                    _bySquare[capturedSquare] = null;
                    _all.Remove(victim);
                    victim.AnimateCapture(duration * 0.75f);
                }
            }

            PieceView mover = _bySquare[move.From];
            if (mover == null) return;

            _bySquare[move.From] = null;
            _bySquare[move.To] = mover;

            // Knights arc high enough to read as leaping over whatever they pass.
            float arc = movedType == PieceType.Knight ? 0.75f : 0.14f;
            mover.AnimateTo(move.To, duration, arc);

            if (move.IsPromotion)
                mover.ChangeType(move.Promotion, PieceMeshLibrary.Get(move.Promotion));

            if (move.IsCastle)
            {
                bool white = mover.Color == PieceColor.White;
                int rookFrom, rookTo;

                if (move.IsKingSideCastle)
                {
                    rookFrom = white ? 7 : 63;
                    rookTo = white ? 5 : 61;
                }
                else
                {
                    rookFrom = white ? 0 : 56;
                    rookTo = white ? 3 : 59;
                }

                PieceView rook = _bySquare[rookFrom];
                if (rook != null)
                {
                    _bySquare[rookFrom] = null;
                    _bySquare[rookTo] = rook;
                    rook.AnimateTo(rookTo, duration, 0.1f);
                }
            }
        }

        /// <summary>Snaps everything to its final position, used when the player skips an animation.</summary>
        public void FinishAnimations()
        {
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                PieceView piece = _all[i];
                if (piece != null) piece.SnapToSquare();
            }
        }
    }
}
