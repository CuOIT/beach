using Chess.Core;
using UnityEngine;

namespace Chess.Game
{
    /// <summary>
    /// One piece on the board. Movement is a self-driven tween rather than a coroutine so the
    /// game can ask at any moment whether animations have settled.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        public PieceType Type { get; private set; }
        public PieceColor Color { get; private set; }
        public int Square { get; private set; }

        private Transform _transform;
        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private float _duration;
        private float _arcHeight;

        private bool _moving;
        private bool _vanishing;
        private Vector3 _baseScale = Vector3.one;

        public bool IsAnimating => _moving || _vanishing;

        public void Initialise(PieceType type, PieceColor color, int square, Mesh mesh, Material material)
        {
            _transform = transform;
            Type = type;
            Color = color;

            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // Knights are not rotationally symmetric, so both armies must face each other.
            _transform.localRotation = Quaternion.Euler(0f, color == PieceColor.White ? 0f : 180f, 0f);
            _baseScale = Vector3.one;
            _transform.localScale = _baseScale;

            PlaceInstantly(square);
        }

        public void PlaceInstantly(int square)
        {
            Square = square;
            _moving = false;
            _vanishing = false;
            _transform.localScale = _baseScale;
            _transform.localPosition = BoardView.SquareToWorld(square);
        }

        /// <summary>
        /// Slides to a new square. Knights hop noticeably higher, which reads as jumping over
        /// the pieces in between.
        /// </summary>
        public void AnimateTo(int square, float duration, float arcHeight)
        {
            Square = square;
            _from = _transform.localPosition;
            _to = BoardView.SquareToWorld(square);
            _duration = Mathf.Max(0.01f, duration);
            _arcHeight = arcHeight;
            _elapsed = 0f;
            _moving = true;
        }

        public void AnimateCapture(float duration)
        {
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            _vanishing = true;
            _moving = false;
        }

        /// <summary>Swaps the model in place, which is what a promotion looks like.</summary>
        public void ChangeType(PieceType type, Mesh mesh)
        {
            Type = type;
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        /// <summary>Jumps straight to the resting position, skipping whatever tween is running.</summary>
        public void SnapToSquare()
        {
            if (_vanishing)
            {
                _vanishing = false;
                Destroy(gameObject);
                return;
            }

            _moving = false;
            _transform.localScale = _baseScale;
            _transform.localPosition = BoardView.SquareToWorld(Square);
        }

        private void Update()
        {
            if (_moving) StepMove();
            else if (_vanishing) StepVanish();
        }

        private void StepMove()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // Smoothstep keeps the piece from starting and stopping abruptly.
            float eased = t * t * (3f - 2f * t);

            Vector3 position = Vector3.Lerp(_from, _to, eased);
            position.y += Mathf.Sin(t * Mathf.PI) * _arcHeight;
            _transform.localPosition = position;

            if (t >= 1f)
            {
                _moving = false;
                _transform.localPosition = _to;
            }
        }

        private void StepVanish()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            _transform.localScale = _baseScale * (1f - t);
            _transform.localPosition += Vector3.up * (Time.deltaTime * 0.6f);

            if (t >= 1f)
            {
                _vanishing = false;
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
    }
}
