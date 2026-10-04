using Chess.Core;
using UnityEngine;

namespace Chess.Game
{
    /// <summary>
    /// Frames the whole board on any screen shape and turns the view around when the human
    /// plays Black. The distance is solved by bisection against the projected board corners, so
    /// it stays correct for tall phone screens as well as wide desktop windows.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        private const float BoardHalfExtent = BoardView.SquareSize * 4f + BoardView.FrameWidth;

        [SerializeField] private Camera _camera;

        private float _currentYaw;
        private float _targetYaw;
        private float _lastAspect;
        private float _distance = 12f;
        private float _pitch = 55f;

        /// <summary>Fraction of the screen kept clear on each side for HUD elements.</summary>
        public float HorizontalMargin = 0.025f;
        public float TopMargin = 0.12f;
        public float BottomMargin = 0.16f;

        public Camera Camera => _camera;

        public void Configure(Camera camera)
        {
            _camera = camera;
            _lastAspect = 0f;
        }

        public void SetSide(PieceColor humanColor, bool instant)
        {
            _targetYaw = humanColor == PieceColor.White ? 0f : 180f;
            if (instant) _currentYaw = _targetYaw;
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            if (!Mathf.Approximately(_lastAspect, _camera.aspect))
            {
                _lastAspect = _camera.aspect;
                Reframe();
            }

            _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, _targetYaw, Time.deltaTime * 420f);
            Apply();
        }

        private void Apply()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _currentYaw, 0f);
            _camera.transform.SetPositionAndRotation(
                rotation * new Vector3(0f, 0f, -_distance),
                rotation);
        }

        /// <summary>
        /// A tall screen wants a steeper, more top-down angle; a wide one looks better from
        /// nearer the side.
        /// </summary>
        private void Reframe()
        {
            float aspect = _camera.aspect;
            // Wide screens are limited by height, so a shallower angle shortens the board's
            // projection and lets it sit closer; tall screens are limited by width, where a
            // steeper, more top-down angle reads better.
            _pitch = aspect < 1f ? Mathf.Lerp(68f, 56f, Mathf.InverseLerp(0.45f, 1f, aspect))
                                 : Mathf.Lerp(56f, 41f, Mathf.InverseLerp(1f, 2f, aspect));

            _distance = SolveDistance();
        }

        /// <summary>
        /// Bisects for the closest distance at which every board corner still projects inside
        /// the safe area. Being exact here matters because a chess board that spills off the
        /// edge of a phone screen is unplayable.
        /// </summary>
        private float SolveDistance()
        {
            const float minDistance = 6f;
            const float maxDistance = 60f;

            float low = minDistance;
            float high = maxDistance;

            if (!Fits(high)) return high;

            for (int i = 0; i < 24; i++)
            {
                float mid = (low + high) * 0.5f;
                if (Fits(mid)) high = mid;
                else low = mid;
            }

            return high;
        }

        private bool Fits(float distance)
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _currentYaw, 0f);
            Vector3 position = rotation * new Vector3(0f, 0f, -distance);

            Matrix4x4 view = Matrix4x4.TRS(position, rotation, new Vector3(1f, 1f, -1f)).inverse;
            Matrix4x4 projection = Matrix4x4.Perspective(_camera.fieldOfView, _camera.aspect, _camera.nearClipPlane, _camera.farClipPlane);
            Matrix4x4 viewProjection = projection * view;

            // The tallest piece is the king, so include its height in the volume being framed.
            const float top = 1.1f;

            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? -BoardHalfExtent : BoardHalfExtent,
                    (corner & 2) == 0 ? 0f : top,
                    (corner & 4) == 0 ? -BoardHalfExtent : BoardHalfExtent);

                Vector4 clip = viewProjection * new Vector4(point.x, point.y, point.z, 1f);
                if (clip.w <= 0.0001f) return false;

                float x = (clip.x / clip.w) * 0.5f + 0.5f;
                float y = (clip.y / clip.w) * 0.5f + 0.5f;

                if (x < HorizontalMargin || x > 1f - HorizontalMargin) return false;
                if (y < BottomMargin || y > 1f - TopMargin) return false;
            }

            return true;
        }

        /// <summary>Re-solves the framing, e.g. after the safe-area margins change.</summary>
        public void Refresh()
        {
            _lastAspect = 0f;
        }
    }
}
