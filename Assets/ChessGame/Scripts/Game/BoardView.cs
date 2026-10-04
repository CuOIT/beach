using System.Collections.Generic;
using Chess.Core;
using Chess.Visual;
using TMPro;
using UnityEngine;

namespace Chess.Game
{
    public enum HighlightKind
    {
        Selection,
        LegalMove,
        LegalCapture,
        LastMove,
        Check
    }

    /// <summary>
    /// Builds the board geometry and owns the mapping between square indices and world space.
    /// The playing surface sits at y = 0 with the board centred on the origin, so a1 is at
    /// (-3.5, 0, -3.5) and h8 at (3.5, 0, 3.5). Flipping for the black player is done by moving
    /// the camera, never the board, so every coordinate here stays in one frame of reference.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float SquareSize = 1f;
        public const float SlabThickness = 0.28f;
        public const float FrameWidth = 0.42f;

        /// <summary>Gap between the frame's top face and the playing surface at y = 0.</summary>
        private const float SquareLift = 0.006f;

        private const float HighlightHeight = 0.006f;

        private BoardTheme _theme;
        private Transform _highlightRoot;
        private Transform _labelRoot;

        private readonly List<GameObject> _highlightPool = new List<GameObject>();
        private int _activeHighlights;

        private Mesh _squareQuad;
        private Mesh _moveDisc;
        private Mesh _captureRing;

        private readonly Dictionary<HighlightKind, Material> _highlightMaterials = new Dictionary<HighlightKind, Material>();

        public BoardTheme Theme => _theme;

        public void Build(BoardTheme theme)
        {
            _theme = theme;

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _highlightPool.Clear();
            _activeHighlights = 0;

            BuildSlab();
            BuildSquares();

            _squareQuad = ProceduralMesh.Quad(SquareSize * 0.98f);
            _moveDisc = ProceduralMesh.Disc(0.155f);
            _captureRing = ProceduralMesh.Ring(0.34f, 0.44f);

            BuildHighlightMaterials();

            _highlightRoot = new GameObject("Highlights").transform;
            _highlightRoot.SetParent(transform, false);
        }

        private void BuildSlab()
        {
            float span = SquareSize * 8f + FrameWidth * 2f;

            var frame = new GameObject("Frame");
            frame.transform.SetParent(transform, false);
            // Drop the slab so its top face clears the square quads; coplanar faces z-fight.
            frame.transform.localPosition = new Vector3(0f, -SlabThickness * 0.5f - SquareLift, 0f);
            frame.AddComponent<MeshFilter>().sharedMesh = ProceduralMesh.Box(new Vector3(span, SlabThickness, span));

            var frameRenderer = frame.AddComponent<MeshRenderer>();
            frameRenderer.sharedMaterial = MaterialLibrary.CreateOpaque("Frame", _theme.Frame, 0.25f);
            frameRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var table = new GameObject("Table");
            table.transform.SetParent(transform, false);
            table.transform.localPosition = new Vector3(0f, -SlabThickness - 0.02f, 0f);
            table.AddComponent<MeshFilter>().sharedMesh = ProceduralMesh.Quad(60f);

            var tableRenderer = table.AddComponent<MeshRenderer>();
            tableRenderer.sharedMaterial = MaterialLibrary.CreateOpaque("Table", _theme.Table, 0.1f);
            tableRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// The 64 squares are merged into two meshes, one per colour, so the whole board costs
        /// two draw calls instead of sixty-four.
        /// </summary>
        private void BuildSquares()
        {
            Mesh quad = ProceduralMesh.Quad(SquareSize);

            var light = new List<ProceduralMesh.Part>(32);
            var dark = new List<ProceduralMesh.Part>(32);

            for (int square = 0; square < 64; square++)
            {
                Vector3 position = SquareToWorld(square);
                var part = new ProceduralMesh.Part(quad, position);

                if (Squares.IsLightSquare(square)) light.Add(part);
                else dark.Add(part);
            }

            CreateSquareGroup("LightSquares", light, _theme.LightSquare);
            CreateSquareGroup("DarkSquares", dark, _theme.DarkSquare);
        }

        private void CreateSquareGroup(string name, List<ProceduralMesh.Part> parts, Color color)
        {
            var group = new GameObject(name);
            group.transform.SetParent(transform, false);
            group.AddComponent<MeshFilter>().sharedMesh = ProceduralMesh.Combine(name, parts);

            var renderer = group.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialLibrary.CreateOpaque(name, color, 0.18f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void BuildHighlightMaterials()
        {
            _highlightMaterials.Clear();
            _highlightMaterials[HighlightKind.Selection] = MaterialLibrary.CreateOverlay("Selection", _theme.Selection, 0.55f);
            _highlightMaterials[HighlightKind.LegalMove] = MaterialLibrary.CreateOverlay("LegalMove", _theme.LegalMove, 0.70f);
            _highlightMaterials[HighlightKind.LegalCapture] = MaterialLibrary.CreateOverlay("LegalCapture", _theme.CaptureMove, 0.80f);
            _highlightMaterials[HighlightKind.LastMove] = MaterialLibrary.CreateOverlay("LastMove", _theme.LastMove, 0.35f);
            _highlightMaterials[HighlightKind.Check] = MaterialLibrary.CreateOverlay("Check", _theme.Check, 0.60f);
        }

        public static Vector3 SquareToWorld(int square, float y = 0f)
        {
            return new Vector3(
                (Squares.FileOf(square) - 3.5f) * SquareSize,
                y,
                (Squares.RankOf(square) - 3.5f) * SquareSize);
        }

        public static bool TryWorldToSquare(Vector3 point, out int square)
        {
            int file = Mathf.RoundToInt(point.x / SquareSize + 3.5f);
            int rank = Mathf.RoundToInt(point.z / SquareSize + 3.5f);

            if (file < 0 || file > 7 || rank < 0 || rank > 7)
            {
                square = Squares.None;
                return false;
            }

            square = Squares.At(file, rank);
            return true;
        }

        /// <summary>Projects a screen point onto the playing surface. No colliders are involved.</summary>
        public static bool TryScreenPointToSquare(Camera camera, Vector2 screenPoint, out int square)
        {
            square = Squares.None;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(screenPoint);
            var surface = new Plane(Vector3.up, Vector3.zero);

            if (!surface.Raycast(ray, out float distance)) return false;

            return TryWorldToSquare(ray.GetPoint(distance), out square);
        }

        public void ClearHighlights()
        {
            for (int i = 0; i < _activeHighlights; i++)
                _highlightPool[i].SetActive(false);

            _activeHighlights = 0;
        }

        public void AddHighlight(int square, HighlightKind kind)
        {
            if (!Squares.IsValid(square)) return;

            GameObject marker = RentHighlight();
            marker.transform.localPosition = SquareToWorld(square, HighlightHeight + LayerOffset(kind));

            Mesh mesh;
            switch (kind)
            {
                case HighlightKind.LegalMove: mesh = _moveDisc; break;
                case HighlightKind.LegalCapture: mesh = _captureRing; break;
                default: mesh = _squareQuad; break;
            }

            marker.GetComponent<MeshFilter>().sharedMesh = mesh;
            marker.GetComponent<MeshRenderer>().sharedMaterial = _highlightMaterials[kind];
            marker.SetActive(true);
        }

        /// <summary>Keeps stacked overlays from z-fighting: later layers sit a hair higher.</summary>
        private static float LayerOffset(HighlightKind kind)
        {
            switch (kind)
            {
                case HighlightKind.LastMove: return 0f;
                case HighlightKind.Check: return 0.001f;
                case HighlightKind.Selection: return 0.002f;
                default: return 0.003f;
            }
        }

        private GameObject RentHighlight()
        {
            if (_activeHighlights < _highlightPool.Count)
                return _highlightPool[_activeHighlights++];

            var marker = new GameObject("Highlight");
            marker.transform.SetParent(_highlightRoot, false);
            marker.AddComponent<MeshFilter>();

            var renderer = marker.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _highlightPool.Add(marker);
            _activeHighlights++;
            return marker;
        }

        /// <summary>Etches file letters and rank numbers into the frame around the board.</summary>
        public void BuildCoordinateLabels(TMP_FontAsset font)
        {
            if (font == null) return;

            if (_labelRoot != null) Destroy(_labelRoot.gameObject);

            _labelRoot = new GameObject("Coordinates").transform;
            _labelRoot.SetParent(transform, false);

            float edge = SquareSize * 4f + FrameWidth * 0.5f;
            Color color = _theme.LightSquare;
            color.a = 0.75f;

            for (int file = 0; file < 8; file++)
            {
                float x = (file - 3.5f) * SquareSize;
                CreateLabel(((char)('a' + file)).ToString(), new Vector3(x, 0.002f, -edge), font, color);
                CreateLabel(((char)('a' + file)).ToString(), new Vector3(x, 0.002f, edge), font, color);
            }

            for (int rank = 0; rank < 8; rank++)
            {
                float z = (rank - 3.5f) * SquareSize;
                CreateLabel((rank + 1).ToString(), new Vector3(-edge, 0.002f, z), font, color);
                CreateLabel((rank + 1).ToString(), new Vector3(edge, 0.002f, z), font, color);
            }
        }

        private void CreateLabel(string text, Vector3 position, TMP_FontAsset font, Color color)
        {
            var label = new GameObject("Label " + text);
            label.transform.SetParent(_labelRoot, false);
            label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mesh = label.AddComponent<TextMeshPro>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 1.1f;
            mesh.color = color;
            mesh.alignment = TextAlignmentOptions.Center;

            var rect = mesh.rectTransform;
            rect.sizeDelta = new Vector2(SquareSize, FrameWidth);
        }
    }
}
