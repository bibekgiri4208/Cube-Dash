using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash
{
    /// <summary>Run-owned shared visuals. Never edits scene assets or creates GameObjects while recycling.</summary>
    public sealed class HighwayPresentation : IDisposable
    {
        private readonly Shader shader = Resources.Load<Shader>("CubeDashCurvedLit");
        private readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        private readonly Dictionary<(Mesh, Vector3), Mesh> meshes = new Dictionary<(Mesh, Vector3), Mesh>();
        private readonly Dictionary<(HighwayHazard, Vector3, float), Mesh> hazards = new Dictionary<(HighwayHazard, Vector3, float), Mesh>();
        private readonly List<(Renderer renderer, Material[] materials, Bounds bounds)> originals = new List<(Renderer, Material[], Bounds)>();
        private readonly List<(MeshFilter filter, Mesh mesh)> originalMeshes = new List<(MeshFilter, Mesh)>();
        private readonly Material dark, reflective;

        public static Bounds CurveBounds(Bounds bounds, Transform transform)
        {
            Vector3 padding = transform.InverseTransformVector(Vector3.right * 200);
            bounds.Expand(new Vector3(Mathf.Abs(padding.x), Mathf.Abs(padding.y), Mathf.Abs(padding.z)) * 2);
            return bounds;
        }

        public void Prewarm(RunnerCube cube)
        {
            for (int i = 1; i <= 3; i++) HazardMesh(cube, (HighwayHazard)i);
        }

        public HighwayPresentation()
        {
            if (shader == null) throw new InvalidOperationException("Missing Resources/CubeDashCurvedLit shader.");
            dark = new Material(shader) { name = "Highway tires and windows", color = new Color(0.025f, 0.04f, 0.055f) };
            reflective = new Material(shader) { name = "Highway reflective trim", color = new Color(1, 0.78f, 0.28f) };
            reflective.SetColor("_EmissionColor", new Color(0.2f, 0.12f, 0.025f));
        }

        public Material MaterialFor(Material source)
        {
            if (source == null || source.shader.name != "Universal Render Pipeline/Lit") return source;
            if (!materials.TryGetValue(source, out Material result))
            {
                // Copy into the destination shader explicitly, preserving authored colors,
                // texture transforms and surface properties without editing source assets.
                result = new Material(shader) { name = source.name + " (curved runtime)", enableInstancing = true };
                result.CopyPropertiesFromMaterial(source);
                result.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                result.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
                result.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                result.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
                if (source.GetTexture("_BaseMap") != null) result.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                result.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                result.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                result.shaderKeywords = Array.Empty<string>();
                materials.Add(source, result);
            }
            return result;
        }

        public void Prepare(Transform root)
        {
            if (root == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = renderer.sharedMaterials;
                originals.Add((renderer, source, renderer.localBounds));
                var curved = new Material[source.Length];
                for (int i = 0; i < source.Length; i++) curved[i] = MaterialFor(source[i]);
                renderer.sharedMaterials = curved;
                // Shader deformation must not disappear when the original straight mesh is offscreen.
                Bounds bounds = CurveBounds(renderer.localBounds, renderer.transform);
                if (!renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                {
                    renderer.localBounds = bounds;
                    continue;
                }
                Mesh sourceMesh = filter.sharedMesh;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                Vector3 axis = new Vector3(matrix.m20, matrix.m21, matrix.m22);
                var key = (sourceMesh, axis);
                if (!meshes.TryGetValue(key, out Mesh mesh))
                {
                    mesh = RoadMeshSubdivision.Create(sourceMesh, axis);
                    meshes.Add(key, mesh);
                }
                originalMeshes.Add((filter, sourceMesh));
                filter.sharedMesh = mesh;
                renderer.localBounds = bounds;
            }
        }

        public static Vector3 HazardSize(HighwayHazard type)
            => type == HighwayHazard.Barrier ? new Vector3(1.8f, 1.15f, 0.8f)
                : type == HighwayHazard.Van ? new Vector3(1.6f, 1.85f, 3.4f)
                : type == HighwayHazard.Truck ? new Vector3(1.7f, 2.25f, 4.6f) : Vector3.one * 1.15f;

        public void ConfigureHazard(RunnerCube cube, HighwayHazard type)
        {
            if (type == HighwayHazard.Cube) return;
            cube.SetHazard(type, HazardMesh(cube, type), new[] { cube.GetComponent<Renderer>().sharedMaterial, dark, reflective }, HazardSize(type));
        }

        private Mesh HazardMesh(RunnerCube cube, HighwayHazard type)
        {
            Vector3 scale = cube.transform.lossyScale;
            float height = cube.transform.localPosition.y;
            var key = (type, scale, height);
            if (!hazards.TryGetValue(key, out Mesh mesh))
            {
                mesh = BuildHazard(type, scale, height);
                hazards.Add(key, mesh);
            }
            return mesh;
        }

        private static Mesh BuildHazard(HighwayHazard type, Vector3 scale, float height)
        {
            Mesh box = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var groups = new List<CombineInstance>[] { new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>() };
            Vector3 inverse = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
            void Part(int group, Vector3 center, Vector3 size)
            {
                groups[group].Add(new CombineInstance { mesh = box, transform = Matrix4x4.TRS(
                    Vector3.Scale(center - Vector3.up * height, inverse), Quaternion.identity, Vector3.Scale(size, inverse)) });
            }
            Vector3 dimensions = HazardSize(type);
            float w = dimensions.x, h = dimensions.y, d = dimensions.z;
            if (type == HighwayHazard.Barrier)
            {
                Part(0, new Vector3(0, 0.62f, 0), new Vector3(w, 0.78f, 0.35f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(1, new Vector3(side * 0.64f, 0.16f, 0), new Vector3(0.18f, 0.32f, d));
                    Part(2, new Vector3(side * 0.64f, 1.07f, 0), new Vector3(0.17f, 0.16f, 0.17f));
                }
                for (int stripe = -2; stripe <= 2; stripe++)
                    Part(2, new Vector3(stripe * 0.33f, 0.63f, -0.185f), new Vector3(0.16f, 0.59f, 0.025f));
            }
            else
            {
                // Traffic faces the runner: cab and headlights at negative Z, box body behind.
                float cabDepth = type == HighwayHazard.Truck ? 1.1f : 1.2f;
                Part(0, new Vector3(0, h * 0.5f + 0.12f, cabDepth * 0.5f), new Vector3(w - 0.12f, h - 0.24f, d - cabDepth));
                Part(0, new Vector3(0, h * 0.38f, -(d - cabDepth) * 0.5f), new Vector3(w - 0.12f, h * 0.57f, cabDepth));
                Part(1, new Vector3(0, h * 0.5f, -d * 0.5f - 0.005f), new Vector3(w * 0.73f, h * 0.22f, 0.025f));
                Part(1, new Vector3(0, 0.34f, 0), new Vector3(w * 0.9f, 0.18f, d));
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int axle = -1; axle <= 1; axle += 2)
                        Part(1, new Vector3(side * (w * 0.5f - 0.13f), 0.29f, axle * (d * 0.5f - 0.65f)), new Vector3(0.26f, 0.58f, 0.58f));
                    Part(2, new Vector3(side * w * 0.32f, 0.52f, -d * 0.5f - 0.025f), new Vector3(0.25f, 0.14f, 0.045f));
                }
                Part(2, new Vector3(0, 0.25f, -d * 0.5f - 0.02f), new Vector3(w * 0.9f, 0.09f, 0.05f));
            }
            var intermediate = new Mesh[3];
            var combined = new CombineInstance[3];
            for (int i = 0; i < 3; i++)
            {
                intermediate[i] = new Mesh();
                intermediate[i].CombineMeshes(groups[i].ToArray(), true, true);
                combined[i] = new CombineInstance { mesh = intermediate[i], transform = Matrix4x4.identity };
            }
            var mesh = new Mesh { name = "Highway " + type };
            mesh.CombineMeshes(combined, false, true);
            foreach (Mesh part in intermediate) Destroy(part);
            return mesh;
        }

        public void Dispose()
        {
            foreach (var original in originals)
                if (original.renderer != null) { original.renderer.sharedMaterials = original.materials; original.renderer.localBounds = original.bounds; }
            foreach (var original in originalMeshes)
                if (original.filter != null) original.filter.sharedMesh = original.mesh;
            foreach (Material material in materials.Values) Destroy(material);
            foreach (var entry in meshes) if (entry.Value != entry.Key.Item1) Destroy(entry.Value);
            foreach (Mesh mesh in hazards.Values) Destroy(mesh);
            Destroy(dark); Destroy(reflective);
        }
        private static void Destroy(Object obj)
        {
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
