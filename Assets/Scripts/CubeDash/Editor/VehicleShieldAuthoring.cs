using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Editor
{
    /// <summary>Bakes a separate chamfered armor envelope for each gameplay vehicle; source prefabs/maps stay untouched.</summary>
    public static class VehicleShieldAuthoring
    {
        public const string MeshFolder = "Assets/3D Models/Vehicle Shields";
        public const string MaterialFolder = "Assets/Material/Vehicle Shield";
        public const string MaterialPath = MaterialFolder + "/Cyan Armor Field.mat";

        [MenuItem("Tools/Cube Dash/Add Vehicle Armor Shields")]
        public static void ApplyFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            ApplyToPlayer();
            AssetDatabase.SaveAssets();
        }

        public static void ApplyToPlayer()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/3D Models", "Vehicle Shields");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Material", "Vehicle Shield");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("CubeDash/Vehicle Armor Shield");
                if (shader == null) throw new InvalidOperationException("The vehicle armor shader is required.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath);
            }
            string path = MainMenuSkinsRevamp.PlayerPath, original = File.ReadAllText(path);
            var player = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var presentation = player.GetComponent<PlayerSupercar>();
                Apply(presentation.Car, PlayerSkin.Supercar, material);
                foreach (var model in presentation.AdditionalSkins) Apply(model.root, model.skin, material);
                PrefabUtility.SaveAsPrefabAsset(player, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            MainMenuSkinsRevamp.PreserveUnchangedDocuments(path, original);
        }

        private static void Apply(Transform model, PlayerSkin skin, Material material)
        {
            if (model == null) throw new InvalidOperationException("Missing vehicle: " + skin);
            Bounds bounds = MainMenuSkinsRevamp.LocalBounds(model, model);
            Mesh mesh = BuildEnvelope(bounds, Mathf.Abs(model.lossyScale.x), skin.ToString());
            string path = MeshFolder + "/" + skin + " Armor.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            Transform effect = model.Find("Vehicle Armor Shield");
            if (effect == null)
            {
                effect = new GameObject("Vehicle Armor Shield", typeof(VehicleShield)).transform;
                effect.SetParent(model, false);
            }
            if (effect.GetComponent<VehicleShield>() == null) throw new InvalidOperationException("Unrecognized shield object; refusing to replace it.");
            effect.GetComponent<MeshFilter>().sharedMesh = saved;
            var renderer = effect.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.enabled = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Mesh BuildEnvelope(Bounds bounds, float scale, string name)
        {
            float unit = 1 / Mathf.Max(0.01f, scale);
            float hx = bounds.extents.x + 0.10f * unit, hz = bounds.extents.z + 0.12f * unit;
            float bottom = bounds.min.y + 0.05f * unit, top = bounds.max.y + 0.10f * unit;
            float bevel = Mathf.Min(0.10f * unit, (top - bottom) * 0.2f);
            var rings = new Vector3[4][];
            var heights = new[] { bottom, bottom + bevel, top - bevel, top };
            for (int row = 0; row < 4; row++)
            {
                float inset = row == 0 ? 0.05f * unit : row == 3 ? 0.10f * unit : 0;
                float x = hx - inset, z = hz - inset, cut = Mathf.Min(x * 0.28f, z * 0.18f);
                var plan = new[] { new Vector2(-x + cut, -z), new Vector2(x - cut, -z), new Vector2(x, -z + cut), new Vector2(x, z - cut),
                    new Vector2(x - cut, z), new Vector2(-x + cut, z), new Vector2(-x, z - cut), new Vector2(-x, -z + cut) };
                rings[row] = new Vector3[8];
                for (int i = 0; i < 8; i++) rings[row][i] = new Vector3(bounds.center.x + plan[i].x, heights[row], bounds.center.z + plan[i].y);
            }
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var edges = new List<Vector2>(); var triangles = new List<int>();
            Vector3 center = new Vector3(bounds.center.x, (bottom + top) * 0.5f, bounds.center.z);
            for (int row = 0; row < 3; row++)
                for (int side = 0; side < 8; side++)
                {
                    int next = (side + 1) % 8, start = vertices.Count;
                    var a = rings[row][side]; var b = rings[row][next]; var c = rings[row + 1][next]; var d = rings[row + 1][side];
                    vertices.AddRange(new[] { a, b, c, d });
                    float width = Vector3.Distance(a, b) * scale;
                    uv.AddRange(new[] { new Vector2(0, heights[row] * scale), new Vector2(width, heights[row] * scale),
                        new Vector2(width, heights[row + 1] * scale), new Vector2(0, heights[row + 1] * scale) });
                    edges.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                    bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c + d) * 0.25f - center) > 0;
                    if (outward) triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    else triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
            // The roof is one flat holographic panel, not a dome. No underside is needed below the road.
            int roof = vertices.Count;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = rings[3][i]; vertices.Add(p);
                uv.Add(new Vector2(p.x * scale, p.z * scale));
                edges.Add(new Vector2((p.x - bounds.center.x) / (2 * hx) + 0.5f, (p.z - bounds.center.z) / (2 * hz) + 0.5f));
            }
            for (int i = 1; i < 7; i++) triangles.AddRange(new[] { roof, roof + i + 1, roof + i });
            var mesh = new Mesh { name = name + " fitted chamfered armor field" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, edges); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
