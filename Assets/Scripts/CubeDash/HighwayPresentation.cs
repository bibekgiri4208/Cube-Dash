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
        private readonly Dictionary<(Mesh, Vector3, float), Mesh> hazards = new Dictionary<(Mesh, Vector3, float), Mesh>();
        private readonly List<(Renderer renderer, Material[] materials, Bounds bounds)> originals = new List<(Renderer, Material[], Bounds)>();
        private readonly List<(MeshFilter filter, Mesh mesh)> originalMeshes = new List<(MeshFilter, Mesh)>();
        private readonly HighwayHazardVisuals visualAssets;
        private readonly Dictionary<Color, Material[]> hazardPalettes = new Dictionary<Color, Material[]>();

        public static Bounds CurveBounds(Bounds bounds, Transform transform)
        {
            Vector3 padding = transform.InverseTransformVector(Vector3.right * 200);
            bounds.Expand(new Vector3(Mathf.Abs(padding.x), Mathf.Abs(padding.y), Mathf.Abs(padding.z)) * 2);
            return bounds;
        }

        public void Prewarm(RunnerCube cube)
        {
            for (int i = 0; i <= 3; i++)
                for (int variant = 0; variant < visualAssets.VariantCount((HighwayHazard)i); variant++) HazardMesh(cube, (HighwayHazard)i, variant);
        }

        public HighwayPresentation()
        {
            if (shader == null) throw new InvalidOperationException("Missing Resources/CubeDashCurvedLit shader.");
            visualAssets = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            if (visualAssets == null || visualAssets.MeshFor(HighwayHazard.Cube) == null || visualAssets.MeshFor(HighwayHazard.Barrier) == null || visualAssets.MeshFor(HighwayHazard.Van) == null || visualAssets.MeshFor(HighwayHazard.Truck) == null
                || visualAssets.Materials.Length != 9)
                throw new InvalidOperationException("Missing or incomplete baked Resources/HighwayHazards art.");
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

        public void ConfigureHazard(RunnerCube cube, HighwayHazard type, int variant = 0)
        {
            Material paint = cube.GetComponent<Renderer>().sharedMaterial;
            Color color = paint.GetColor("_BaseColor");
            if (!hazardPalettes.TryGetValue(color, out Material[] palette))
            {
                palette = (Material[])visualAssets.Materials.Clone();
                palette[0] = new Material(palette[0]) { name = "Traffic paint " + color, enableInstancing = true };
                palette[0].SetColor("_BaseColor", color);
                hazardPalettes.Add(color, palette);
            }
            cube.SetHazard(type, HazardMesh(cube, type, variant), palette, HazardSize(type));
        }

        private Mesh HazardMesh(RunnerCube cube, HighwayHazard type, int variant)
        {
            Vector3 scale = cube.transform.lossyScale;
            float height = cube.transform.localPosition.y;
            Mesh authored = visualAssets.MeshFor(type, variant);
            var key = (authored, scale, height);
            if (!hazards.TryGetValue(key, out Mesh mesh))
            {
                mesh = BuildHazard(authored, scale, height);
                hazards.Add(key, mesh);
            }
            return mesh;
        }

        private static Mesh BuildHazard(Mesh authored, Vector3 scale, float height)
        {
            Mesh result = Object.Instantiate(authored); result.name = authored.name + " (pooled road space)";
            Vector3[] points = result.vertices, normals = result.normals;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector3(points[i].x / scale.x, (points[i].y - height) / scale.y, points[i].z / scale.z);
                normals[i] = Vector3.Scale(normals[i], scale).normalized;
            }
            result.vertices = points; result.normals = normals;
            result.RecalculateTangents(); result.RecalculateBounds(); return result;
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
            foreach (Material[] palette in hazardPalettes.Values) Destroy(palette[0]);
        }
        private static void Destroy(Object obj)
        {
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
