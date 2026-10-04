using System.Collections.Generic;
using Chess.Core;
using UnityEngine;

namespace Chess.Visual
{
    /// <summary>
    /// Builds one mesh per piece type on demand and keeps them cached. Every piece is a turned
    /// body - the profile a lathe would cut - with distinguishing parts welded on: a ball for the
    /// pawn, battlements for the rook, a blocky head for the knight, a crown for the queen and a
    /// cross for the king. All dimensions assume a board square one unit across.
    /// </summary>
    public static class PieceMeshLibrary
    {
        private static readonly Dictionary<PieceType, Mesh> Cache = new Dictionary<PieceType, Mesh>();

        public static void Clear()
        {
            foreach (Mesh mesh in Cache.Values)
                if (mesh != null) Object.DestroyImmediate(mesh);

            Cache.Clear();
        }

        public static Mesh Get(PieceType type)
        {
            if (Cache.TryGetValue(type, out Mesh cached) && cached != null) return cached;

            Mesh mesh = Build(type);
            Cache[type] = mesh;
            return mesh;
        }

        /// <summary>Approximate model height, used to place captured pieces and size highlights.</summary>
        public static float HeightOf(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return 0.66f;
                case PieceType.Rook: return 0.70f;
                case PieceType.Knight: return 0.78f;
                case PieceType.Bishop: return 0.80f;
                case PieceType.Queen: return 0.90f;
                case PieceType.King: return 1.01f;
                default: return 0.7f;
            }
        }

        private static Mesh Build(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return BuildPawn();
                case PieceType.Rook: return BuildRook();
                case PieceType.Knight: return BuildKnight();
                case PieceType.Bishop: return BuildBishop();
                case PieceType.Queen: return BuildQueen();
                case PieceType.King: return BuildKing();
                default: return ProceduralMesh.Cylinder(0.2f, 0.5f);
            }
        }

        private static Mesh BuildPawn()
        {
            var body = ProceduralMesh.Lathe(new[]
            {
                new Vector2(0.00f, 0.000f),
                new Vector2(0.30f, 0.000f),
                new Vector2(0.32f, 0.022f),
                new Vector2(0.30f, 0.058f),
                new Vector2(0.22f, 0.078f),
                new Vector2(0.14f, 0.115f),
                new Vector2(0.11f, 0.235f),
                new Vector2(0.14f, 0.272f),
                new Vector2(0.20f, 0.300f),
                new Vector2(0.19f, 0.332f),
                new Vector2(0.13f, 0.358f),
                new Vector2(0.10f, 0.400f)
            });

            var parts = new List<ProceduralMesh.Part>
            {
                new ProceduralMesh.Part(body, Vector3.zero),
                new ProceduralMesh.Part(ProceduralMesh.Sphere(0.160f), new Vector3(0f, 0.500f, 0f))
            };

            return ProceduralMesh.Combine("Pawn", parts);
        }

        private static Mesh BuildRook()
        {
            // The profile turns back inward at the top to hollow out the tower.
            var body = ProceduralMesh.Lathe(new[]
            {
                new Vector2(0.00f, 0.000f),
                new Vector2(0.34f, 0.000f),
                new Vector2(0.36f, 0.026f),
                new Vector2(0.34f, 0.062f),
                new Vector2(0.25f, 0.086f),
                new Vector2(0.22f, 0.120f),
                new Vector2(0.21f, 0.400f),
                new Vector2(0.24f, 0.436f),
                new Vector2(0.30f, 0.474f),
                new Vector2(0.32f, 0.520f),
                new Vector2(0.32f, 0.620f),
                new Vector2(0.27f, 0.620f),
                new Vector2(0.27f, 0.545f)
            });

            var parts = new List<ProceduralMesh.Part> { new ProceduralMesh.Part(body, Vector3.zero) };

            Mesh battlement = ProceduralMesh.Box(new Vector3(0.115f, 0.085f, 0.075f));
            const int count = 6;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                var position = new Vector3(Mathf.Cos(angle) * 0.295f, 0.660f, Mathf.Sin(angle) * 0.295f);
                Quaternion rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                parts.Add(new ProceduralMesh.Part(battlement, position, rotation, Vector3.one));
            }

            return ProceduralMesh.Combine("Rook", parts);
        }

        private static Mesh BuildKnight()
        {
            var body = ProceduralMesh.Lathe(new[]
            {
                new Vector2(0.00f, 0.000f),
                new Vector2(0.33f, 0.000f),
                new Vector2(0.35f, 0.026f),
                new Vector2(0.33f, 0.062f),
                new Vector2(0.24f, 0.086f),
                new Vector2(0.20f, 0.120f),
                new Vector2(0.19f, 0.210f),
                new Vector2(0.17f, 0.275f)
            });

            var parts = new List<ProceduralMesh.Part>
            {
                new ProceduralMesh.Part(body, Vector3.zero),

                // Neck, raked forward like a knight's arching mane.
                new ProceduralMesh.Part(
                    ProceduralMesh.Box(new Vector3(0.200f, 0.330f, 0.210f)),
                    new Vector3(0f, 0.420f, -0.040f),
                    Quaternion.Euler(-18f, 0f, 0f), Vector3.one),

                // Skull.
                new ProceduralMesh.Part(
                    ProceduralMesh.Box(new Vector3(0.185f, 0.175f, 0.300f)),
                    new Vector3(0f, 0.610f, 0.055f),
                    Quaternion.Euler(8f, 0f, 0f), Vector3.one),

                // Muzzle.
                new ProceduralMesh.Part(
                    ProceduralMesh.Box(new Vector3(0.150f, 0.125f, 0.150f)),
                    new Vector3(0f, 0.560f, 0.215f),
                    Quaternion.Euler(14f, 0f, 0f), Vector3.one),

                // Ears.
                new ProceduralMesh.Part(
                    ProceduralMesh.Box(new Vector3(0.050f, 0.110f, 0.050f)),
                    new Vector3(-0.055f, 0.725f, -0.050f),
                    Quaternion.Euler(-10f, 0f, 0f), Vector3.one),
                new ProceduralMesh.Part(
                    ProceduralMesh.Box(new Vector3(0.050f, 0.110f, 0.050f)),
                    new Vector3(0.055f, 0.725f, -0.050f),
                    Quaternion.Euler(-10f, 0f, 0f), Vector3.one)
            };

            return ProceduralMesh.Combine("Knight", parts);
        }

        private static Mesh BuildBishop()
        {
            var body = ProceduralMesh.Lathe(new[]
            {
                new Vector2(0.00f, 0.000f),
                new Vector2(0.33f, 0.000f),
                new Vector2(0.35f, 0.026f),
                new Vector2(0.33f, 0.062f),
                new Vector2(0.24f, 0.086f),
                new Vector2(0.20f, 0.118f),
                new Vector2(0.16f, 0.205f),
                new Vector2(0.14f, 0.300f),
                new Vector2(0.19f, 0.338f),
                new Vector2(0.22f, 0.366f),
                new Vector2(0.16f, 0.398f),
                new Vector2(0.13f, 0.424f),
                new Vector2(0.18f, 0.472f),
                new Vector2(0.19f, 0.532f),
                new Vector2(0.16f, 0.602f),
                new Vector2(0.10f, 0.662f),
                new Vector2(0.04f, 0.706f),
                new Vector2(0.00f, 0.724f)
            });

            var parts = new List<ProceduralMesh.Part>
            {
                new ProceduralMesh.Part(body, Vector3.zero),
                new ProceduralMesh.Part(ProceduralMesh.Sphere(0.058f), new Vector3(0f, 0.756f, 0f))
            };

            return ProceduralMesh.Combine("Bishop", parts);
        }

        private static Mesh BuildQueen()
        {
            var body = ProceduralMesh.Lathe(new[]
            {
                new Vector2(0.00f, 0.000f),
                new Vector2(0.36f, 0.000f),
                new Vector2(0.38f, 0.028f),
                new Vector2(0.36f, 0.066f),
                new Vector2(0.26f, 0.096f),
                new Vector2(0.22f, 0.132f),
                new Vector2(0.17f, 0.255f),
                new Vector2(0.15f, 0.360f),
                new Vector2(0.21f, 0.400f),
                new Vector2(0.24f, 0.432f),
                new Vector2(0.18f, 0.466f),
                new Vector2(0.16f, 0.502f),
                new Vector2(0.22f, 0.622f),
                new Vector2(0.26f, 0.700f),
                new Vector2(0.24f, 0.732f),
                new Vector2(0.20f, 0.748f)
            });

            var parts = new List<ProceduralMesh.Part> { new ProceduralMesh.Part(body, Vector3.zero) };

            Mesh point = ProceduralMesh.Sphere(0.052f, 12, 8);
            const int points = 8;
            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2f / points;
                parts.Add(new ProceduralMesh.Part(
                    point,
                    new Vector3(Mathf.Cos(angle) * 0.215f, 0.778f, Mathf.Sin(angle) * 0.215f)));
            }

            parts.Add(new ProceduralMesh.Part(ProceduralMesh.Sphere(0.072f), new Vector3(0f, 0.832f, 0f)));

            return ProceduralMesh.Combine("Queen", parts);
        }

        private static Mesh BuildKing()
        {
            var body = ProceduralMesh.Lathe(new[]
            {
                new Vector2(0.00f, 0.000f),
                new Vector2(0.37f, 0.000f),
                new Vector2(0.39f, 0.028f),
                new Vector2(0.37f, 0.066f),
                new Vector2(0.27f, 0.096f),
                new Vector2(0.23f, 0.132f),
                new Vector2(0.18f, 0.272f),
                new Vector2(0.16f, 0.400f),
                new Vector2(0.22f, 0.442f),
                new Vector2(0.25f, 0.472f),
                new Vector2(0.19f, 0.506f),
                new Vector2(0.17f, 0.546f),
                new Vector2(0.23f, 0.670f),
                new Vector2(0.26f, 0.750f),
                new Vector2(0.24f, 0.784f),
                new Vector2(0.20f, 0.800f)
            });

            var parts = new List<ProceduralMesh.Part>
            {
                new ProceduralMesh.Part(body, Vector3.zero),
                new ProceduralMesh.Part(ProceduralMesh.Box(new Vector3(0.068f, 0.230f, 0.068f)), new Vector3(0f, 0.905f, 0f)),
                new ProceduralMesh.Part(ProceduralMesh.Box(new Vector3(0.190f, 0.068f, 0.068f)), new Vector3(0f, 0.928f, 0f))
            };

            return ProceduralMesh.Combine("King", parts);
        }
    }
}
