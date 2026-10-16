using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash
{
    /// <summary>One-time longitudinal slicing: even a two-triangle road bends smoothly. Preserves submeshes, effect UVs and rise anchors.</summary>
    public static class RoadMeshSubdivision
    {
        private struct Vertex
        {
            public Vector3 Position, Normal;
            public Vector2 Uv, EffectUv, Anchor;
            public Vector4 Tangent;
            public Color Color;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
            {
                Position = Vector3.Lerp(a.Position, b.Position, t), Normal = Vector3.Lerp(a.Normal, b.Normal, t),
                Uv = Vector2.Lerp(a.Uv, b.Uv, t), EffectUv = Vector2.Lerp(a.EffectUv, b.EffectUv, t), Anchor = Vector2.Lerp(a.Anchor, b.Anchor, t),
                Tangent = Vector4.Lerp(a.Tangent, b.Tangent, t), Color = UnityEngine.Color.Lerp(a.Color, b.Color, t)
            };
        }

        public static Mesh Create(Mesh source, Vector3 worldZAxis, float spacing = 3f)
        {
            spacing = Mathf.Max(0.5f, spacing);
            Vector3[] positions = source.vertices, normals = source.normals;
            Vector2[] uv = source.uv, effectUv = source.uv2, anchors = source.uv3;
            bool hasEffectUv = effectUv.Length == positions.Length;
            Vector4[] tangents = source.tangents;
            Color[] colors = source.colors;
            var input = new Vertex[positions.Length];
            for (int i = 0; i < input.Length; i++)
                input[i] = new Vertex { Position = positions[i], Normal = normals.Length == input.Length ? normals[i] : Vector3.up,
                    Uv = uv.Length == input.Length ? uv[i] : Vector2.zero, Anchor = anchors.Length == input.Length ? anchors[i] : Vector2.zero,
                    EffectUv = hasEffectUv ? effectUv[i] : Vector2.zero,
                    Tangent = tangents.Length == input.Length ? tangents[i] : new Vector4(1, 0, 0, 1), Color = colors.Length == input.Length ? colors[i] : Color.white };
            bool needsSlicing = false;
            for (int sub = 0; sub < source.subMeshCount && !needsSlicing; sub++)
            {
                int[] triangles = source.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    float a = Vector3.Dot(positions[triangles[i]], worldZAxis), b = Vector3.Dot(positions[triangles[i + 1]], worldZAxis), c = Vector3.Dot(positions[triangles[i + 2]], worldZAxis);
                    if (Mathf.Max(a, Mathf.Max(b, c)) - Mathf.Min(a, Mathf.Min(b, c)) > spacing) { needsSlicing = true; break; }
                }
            }
            if (!needsSlicing) return source;
            var output = new List<Vertex>();
            var indices = new List<int>[source.subMeshCount];
            var polygon = new List<Vertex>(6);
            var scratch = new List<Vertex>(6);
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                indices[sub] = new List<int>();
                int[] triangles = source.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vertex a = input[triangles[i]], b = input[triangles[i + 1]], c = input[triangles[i + 2]];
                    float za = Vector3.Dot(a.Position, worldZAxis), zb = Vector3.Dot(b.Position, worldZAxis), zc = Vector3.Dot(c.Position, worldZAxis);
                    float min = Mathf.Min(za, Mathf.Min(zb, zc)), max = Mathf.Max(za, Mathf.Max(zb, zc));
                    int first = Mathf.FloorToInt(min / spacing), last = Mathf.FloorToInt((max - 0.0001f) / spacing);
                    last = Mathf.Max(first, last);
                    for (int slice = first; slice <= last; slice++)
                    {
                        polygon.Clear(); polygon.Add(a); polygon.Add(b); polygon.Add(c);
                        Clip(polygon, scratch, worldZAxis, slice * spacing, true);
                        Clip(scratch, polygon, worldZAxis, (slice + 1) * spacing, false);
                        for (int v = 1; v < polygon.Count - 1; v++)
                        {
                            int start = output.Count;
                            output.Add(polygon[0]); output.Add(polygon[v]); output.Add(polygon[v + 1]);
                            indices[sub].Add(start); indices[sub].Add(start + 1); indices[sub].Add(start + 2);
                        }
                    }
                }
            }
            var mesh = new Mesh { name = source.name + " (road slices)", indexFormat = output.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            var p = new List<Vector3>(output.Count); var n = new List<Vector3>(output.Count);
            var u = new List<Vector2>(output.Count); var effect = hasEffectUv ? new List<Vector2>(output.Count) : null;
            var anchor = new List<Vector2>(output.Count);
            var tangent = new List<Vector4>(output.Count); var color = new List<Color>(output.Count);
            foreach (Vertex v in output)
            { p.Add(v.Position); n.Add(v.Normal.normalized); u.Add(v.Uv); effect?.Add(v.EffectUv); anchor.Add(v.Anchor); tangent.Add(v.Tangent); color.Add(v.Color); }
            mesh.SetVertices(p); mesh.SetNormals(n); mesh.SetUVs(0, u); mesh.SetUVs(2, anchor); mesh.SetTangents(tangent); mesh.SetColors(color);
            if (hasEffectUv) mesh.SetUVs(1, effect);
            mesh.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < indices.Length; sub++) mesh.SetTriangles(indices[sub], sub);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Clip(List<Vertex> input, List<Vertex> output, Vector3 axis, float plane, bool above)
        {
            output.Clear();
            if (input.Count == 0) return;
            Vertex previous = input[input.Count - 1];
            float previousZ = Vector3.Dot(previous.Position, axis);
            bool previousInside = above ? previousZ >= plane : previousZ <= plane;
            foreach (Vertex current in input)
            {
                float z = Vector3.Dot(current.Position, axis);
                bool inside = above ? z >= plane : z <= plane;
                if (inside != previousInside) output.Add(Vertex.Lerp(previous, current, (plane - previousZ) / (z - previousZ)));
                if (inside) output.Add(current);
                previous = current; previousZ = z; previousInside = inside;
            }
        }
    }
}
