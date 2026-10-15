using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Editor
{
    /// <summary>Adds the rear-light effect only to the gameplay player, never to the menu preview.</summary>
    public static class SupercarLightTrailAuthoring
    {
        public const string MaterialPath = "Assets/Material/Supercar/Red Light Trail.mat";
        [MenuItem("Tools/Cube Dash/Add Supercar Rear Light Trails")]
        public static void ApplyFromCommandLine()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("CubeDash/Supercar Light Trail"));
                material.SetColor("_Tint", new Color(3, 0.015f, 0.025f, 0.8f));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            const string path = "Assets/Prefab/CubeDash/PlayerCube.prefab";
            GameObject player = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform car = player.GetComponent<PlayerSupercar>().Car;
                Transform effect = car.Find("Rear Red Light Trails");
                if (effect == null)
                {
                    effect = new GameObject("Rear Red Light Trails", typeof(SupercarLightTrail)).transform;
                    effect.SetParent(car, false);
                }
                var renderer = effect.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.enabled = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                var settings = new SerializedObject(effect.GetComponent<SupercarLightTrail>());
                settings.FindProperty("car").objectReferenceValue = car;
                settings.FindProperty("width").floatValue = 0.46f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
        }
    }
}
