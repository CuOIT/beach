using System.Collections.Generic;
using UnityEngine;

namespace Chess.Visual
{
    /// <summary>
    /// Small mesh factory used to build the chess set at runtime, so the project needs no
    /// imported art. Pieces are surfaces of revolution with a few solid parts welded on top.
    /// </summary>
    public static class ProceduralMesh
    {
        /// <summary>
        /// Revolves a profile around the Y axis. Profile points are (radius, height), ordered
        /// bottom to top; a radius of zero closes the shape at that end.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int radialSegments = 24)
        {
            int rings = profile.Count;
            var vertices = new List<Vector3>(rings * radialSegments + 2);
            var triangles = new List<int>(rings * radialSegments * 6);

            for (int ring = 0; ring < rings; ring++)
            {
                float radius = profile[ring].x;
                float height = profile[ring].y;

                for (int segment = 0; segment < radialSegments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / radialSegments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
                }
            }

            for (int ring = 0; ring < rings - 1; ring++)
            {
                int lower = ring * radialSegments;
                int upper = (ring + 1) * radialSegments;

                for (int segment = 0; segment < radialSegments; segment++)
                {
                    int next = (segment + 1) % radialSegments;

                    triangles.Add(lower + segment);
                    triangles.Add(upper + segment);
                    triangles.Add(upper + next);

                    triangles.Add(lower + segment);
                    triangles.Add(upper + next);
                    triangles.Add(lower + next);
                }
            }

            // Close the ends whenever the profile does not already taper to the axis.
            if (profile[0].x > 0.0001f)
                AddCap(vertices, triangles, profile[0].y, 0, radialSegments, false);

            if (profile[rings - 1].x > 0.0001f)
                AddCap(vertices, triangles, profile[rings - 1].y, (rings - 1) * radialSegments, radialSegments, true);

            var mesh = new Mesh { name = "Lathe" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCap(List<Vector3> vertices, List<int> triangles, float height, int ringStart, int segments, bool facingUp)
        {
            int center = vertices.Count;
            vertices.Add(new Vector3(0f, height, 0f));

            for (int segment = 0; segment < segments; segment++)
            {
                int current = ringStart + segment;
                int next = ringStart + (segment + 1) % segments;

                if (facingUp)
                {
                    triangles.Add(center);
                    triangles.Add(current);
                    triangles.Add(next);
                }
                else
                {
                    triangles.Add(center);
                    triangles.Add(next);
                    triangles.Add(current);
                }
            }
        }

        public static Mesh Box(Vector3 size)
        {
            Vector3 h = size * 0.5f;

            var vertices = new[]
            {
                // bottom
                new Vector3(-h.x, -h.y, -h.z), new Vector3( h.x, -h.y, -h.z), new Vector3( h.x, -h.y,  h.z), new Vector3(-h.x, -h.y,  h.z),
                // top
                new Vector3(-h.x,  h.y, -h.z), new Vector3(-h.x,  h.y,  h.z), new Vector3( h.x,  h.y,  h.z), new Vector3( h.x,  h.y, -h.z),
                // front
                new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x,  h.y, -h.z), new Vector3( h.x,  h.y, -h.z), new Vector3( h.x, -h.y, -h.z),
                // back
                new Vector3( h.x, -h.y,  h.z), new Vector3( h.x,  h.y,  h.z), new Vector3(-h.x,  h.y,  h.z), new Vector3(-h.x, -h.y,  h.z),
                // left
                new Vector3(-h.x, -h.y,  h.z), new Vector3(-h.x,  h.y,  h.z), new Vector3(-h.x,  h.y, -h.z), new Vector3(-h.x, -h.y, -h.z),
                // right
                new Vector3( h.x, -h.y, -h.z), new Vector3( h.x,  h.y, -h.z), new Vector3( h.x,  h.y,  h.z), new Vector3( h.x, -h.y,  h.z)
            };

            var triangles = new int[36];
            for (int face = 0; face < 6; face++)
            {
                int v = face * 4;
                int t = face * 6;
                triangles[t + 0] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "Box" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Sphere(float radius, int longitude = 20, int latitude = 12)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int lat = 0; lat <= latitude; lat++)
            {
                float theta = lat * Mathf.PI / latitude;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= longitude; lon++)
                {
                    float phi = lon * 2f * Mathf.PI / longitude;
                    vertices.Add(new Vector3(
                        radius * sinTheta * Mathf.Cos(phi),
                        radius * cosTheta,
                        radius * sinTheta * Mathf.Sin(phi)));
                }
            }

            int stride = longitude + 1;
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    int a = lat * stride + lon;
                    int b = a + stride;

                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);

                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            var mesh = new Mesh { name = "Sphere" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Cylinder(float radius, float height, int segments = 20)
        {
            return Lathe(new[]
            {
                new Vector2(radius, 0f),
                new Vector2(radius, height)
            }, segments);
        }

        public struct Part
        {
            public Mesh Mesh;
            public Matrix4x4 Transform;

            public Part(Mesh mesh, Vector3 position)
            {
                Mesh = mesh;
                Transform = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
            }

            public Part(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Mesh = mesh;
                Transform = Matrix4x4.TRS(position, rotation, scale);
            }
        }

        /// <summary>Welds several parts into one mesh so a piece renders in a single draw call.</summary>
        public static Mesh Combine(string name, IList<Part> parts)
        {
            var combine = new CombineInstance[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                combine[i].mesh = parts[i].Mesh;
                combine[i].transform = parts[i].Transform;
            }

            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A flat filled circle facing up, used for the legal-move markers.</summary>
        public static Mesh Disc(float radius, int segments = 32)
        {
            var vertices = new List<Vector3>(segments + 1) { Vector3.zero };
            var triangles = new List<int>(segments * 3);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            for (int i = 0; i < segments; i++)
            {
                triangles.Add(0);
                triangles.Add(1 + (i + 1) % segments);
                triangles.Add(1 + i);
            }

            var mesh = new Mesh { name = "Disc" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A flat annulus facing up, drawn around a piece that can be captured.</summary>
        public static Mesh Ring(float innerRadius, float outerRadius, int segments = 32)
        {
            var vertices = new List<Vector3>(segments * 2);
            var triangles = new List<int>(segments * 6);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices.Add(new Vector3(cos * innerRadius, 0f, sin * innerRadius));
                vertices.Add(new Vector3(cos * outerRadius, 0f, sin * outerRadius));
            }

            for (int i = 0; i < segments; i++)
            {
                int inner = i * 2;
                int outer = inner + 1;
                int nextInner = ((i + 1) % segments) * 2;
                int nextOuter = nextInner + 1;

                triangles.Add(inner);
                triangles.Add(nextOuter);
                triangles.Add(outer);

                triangles.Add(inner);
                triangles.Add(nextInner);
                triangles.Add(nextOuter);
            }

            var mesh = new Mesh { name = "Ring" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A flat, upward-facing quad centred on the origin, used for board squares and highlights.</summary>
        public static Mesh Quad(float size)
        {
            float h = size * 0.5f;

            var mesh = new Mesh { name = "Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-h, 0f, -h),
                new Vector3(-h, 0f,  h),
                new Vector3( h, 0f,  h),
                new Vector3( h, 0f, -h)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
