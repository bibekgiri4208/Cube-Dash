using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Editor
{
    /// <summary>Adds one shared, pre-authored aura to pooled pickups without rebuilding maps.</summary>
    public static class PowerUpAuraAuthoring
    {
        public const string PrefabPath = "Assets/Prefab/CubeDash/PowerUp.prefab";
        public const string MaterialPath = "Assets/Material/PowerUpModels/Pickup Aura.mat";
        public const string MeshPath = "Assets/3D Models/CubeDashPickupAura.asset";

        [MenuItem("Tools/Cube Dash/Add Power-Up Auras")]
        public static void ApplyFromCommandLine()
        {
            Shader shader = Shader.Find("CubeDash/Power Up Aura");
            if (shader == null) throw new InvalidOperationException("Power Up Aura shader is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Pickup Aura" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Pickup Aura Quad" };
                mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                // The vertex shader billboards and bends the quad; keep conservative CPU culling bounds.
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8);
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform aura = root.transform.Find("Pickup Aura");
                if (aura == null)
                {
                    aura = new GameObject("Pickup Aura", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    aura.SetParent(root.transform, false);
                }
                aura.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = aura.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                var pickup = root.GetComponent<PowerUpPickup>();
                var settings = new SerializedObject(pickup);
                settings.FindProperty("auraRenderer").objectReferenceValue = renderer;
                settings.ApplyModifiedPropertiesWithoutUndo();
                pickup.Tick(0);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Added pooled, color-matched power-up auras through the shared pickup prefab; map scenes were not rewritten.");
        }
    }
}
