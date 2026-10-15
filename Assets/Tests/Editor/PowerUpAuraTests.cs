using System.Collections;
using System.IO;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class PowerUpAuraTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [TestCase("Level")]
        [TestCase("Country Side")]
        [TestCase("City")]
        [TestCase("Jungle")]
        [TestCase("Desert")]
        [TestCase("Beach")]
        public void EveryMapInheritsAuthoredAurasWithoutExtraColliders(string map)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + map + ".unity");
            try
            {
                var pickups = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PowerUpPickup>(true)).ToArray();
                Assert.That(pickups.Length, Is.GreaterThan(0));
                foreach (var pickup in pickups)
                {
                    var aura = pickup.AuraRenderer;
                    Assert.That(aura, Is.Not.Null, pickup.name);
                    Assert.That(aura.transform.parent, Is.EqualTo(pickup.transform));
                    Assert.That(aura.sharedMaterial.shader.name, Is.EqualTo("CubeDash/Power Up Aura"));
                    Assert.That(aura.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                    Assert.That(aura.GetComponentsInChildren<Collider>().Length, Is.Zero);
                    Assert.That(pickup.GetComponentsInChildren<Collider>(true).Count(c => c == pickup.Collider), Is.EqualTo(1));
                    Assert.That(aura.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(4));
                }
                Assert.That(ShaderUtil.ShaderHasError(pickups[0].AuraRenderer.sharedMaterial.shader), Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void AuraTracksEveryModelPulsesFreezesAndReusesItsObjectsAndMaterial()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PowerUpAuraAuthoring.PrefabPath));
            try
            {
                var pickup = root.GetComponent<PowerUpPickup>();
                var aura = pickup.AuraRenderer;
                var shared = aura.sharedMaterial;
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                string[] models = { "Shield Icon", "2x Icon", "Fighter Icon", "Truck Icon", "Magnet Icon" };
                var colors = new System.Collections.Generic.HashSet<Color>();
                var properties = new MaterialPropertyBlock();
                for (int i = 0; i < models.Length; i++)
                {
                    pickup.Configure((PowerUpType)i, null);
                    pickup.Tick(0.25f);
                    var model = root.transform.Find(models[i]);
                    var renderers = model.GetComponentsInChildren<MeshRenderer>();
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Assert.That(Vector3.Distance(aura.transform.position, bounds.center), Is.LessThan(0.15f), "The aura follows the actual model center, not its ground-level pivot.");
                    Vector3 size = aura.transform.localScale;
                    aura.GetPropertyBlock(properties);
                    colors.Add(properties.GetColor("_Tint"));
                    float phase = properties.GetFloat("_Phase");
                    pickup.Tick(0);
                    Assert.That(aura.transform.localScale, Is.EqualTo(size), "Zero delta/paused gameplay must freeze the pulse.");
                    aura.GetPropertyBlock(properties);
                    Assert.That(properties.GetFloat("_Phase"), Is.EqualTo(phase));
                    pickup.Tick(0.4f);
                    Assert.That(aura.transform.localScale, Is.Not.EqualTo(size));
                    root.SetActive(false);
                    Assert.That(aura.gameObject.activeInHierarchy, Is.False);
                    root.SetActive(true);
                    Assert.That(aura.sharedMaterial, Is.SameAs(shared));
                    Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                }
                Assert.That(colors.Count, Is.EqualTo(5), "Every power-up has a distinct matching aura tint.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator AllFiveAurasRenderWithoutShaderErrors()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU rendering required.");
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var stage = new GameObject("Aura Preview Stage");
            var target = new RenderTexture(1600, 600, 24);
            var image = new Texture2D(1600, 600, TextureFormat.RGB24, false);
            var before = new Texture2D(1600, 600, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            Vector4 oldCurve = Shader.GetGlobalVector("_CubeDashRoadCurve");
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Shader.SetGlobalVector("_CubeDashRoadCurve", Vector4.zero);
                var camera = new GameObject("Aura Preview Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(stage.transform);
                camera.transform.position = new Vector3(0, 0.8f, -10);
                camera.transform.rotation = Quaternion.Euler(4.5f, 0, 0);
                camera.orthographic = true; camera.orthographicSize = 1.7f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.045f, 0.065f, 0.1f);
                camera.cullingMask = 1 << 31; camera.targetTexture = target;
                var light = new GameObject("Preview Light", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(stage.transform); light.type = LightType.Directional;
                light.intensity = 1.3f; light.transform.rotation = Quaternion.Euler(40, -30, 0); light.cullingMask = 1 << 31;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PowerUpAuraAuthoring.PrefabPath);
                var pickups = new PowerUpPickup[5];
                for (int i = 0; i < pickups.Length; i++)
                {
                    pickups[i] = Object.Instantiate(prefab, stage.transform).GetComponent<PowerUpPickup>();
                    pickups[i].transform.position = new Vector3((i - 2) * 1.75f, 0, 0);
                    foreach (Transform child in pickups[i].GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    pickups[i].Configure((PowerUpType)i, null); pickups[i].Tick(0.3f);
                    pickups[i].AuraRenderer.enabled = false;
                }
                for (int i = 0; i < 3; i++) camera.Render();
                RenderTexture.active = target; before.ReadPixels(new Rect(0, 0, 1600, 600), 0, 0); before.Apply();
                foreach (var pickup in pickups) pickup.AuraRenderer.enabled = true;
                for (int i = 0; i < 3; i++) camera.Render();
                image.ReadPixels(new Rect(0, 0, 1600, 600), 0, 0); image.Apply();
                Color32[] original = before.GetPixels32(); Color32[] glowing = image.GetPixels32();
                int changed = 0;
                for (int i = 0; i < original.Length; i++)
                    if (Mathf.Abs(original[i].r - glowing[i].r) + Mathf.Abs(original[i].g - glowing[i].g) + Mathf.Abs(original[i].b - glowing[i].b) > 18) changed++;
                Assert.That(changed, Is.GreaterThan(10000), "Auras must visibly render, not just exist in the hierarchy.");
                Assert.That(ShaderUtil.ShaderHasError(pickups[0].AuraRenderer.sharedMaterial.shader), Is.False);
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_AURA_PREVIEW");
                if (!string.IsNullOrEmpty(prefix))
                {
                    File.WriteAllBytes(prefix + ".png", image.EncodeToPNG());
                    File.WriteAllBytes(prefix + "-before.png", before.EncodeToPNG());
                }
            }
            finally
            {
                Shader.SetGlobalVector("_CubeDashRoadCurve", oldCurve);
                ShaderUtil.allowAsyncCompilation = oldAsync; RenderTexture.active = oldActive;
                Object.DestroyImmediate(stage); Object.DestroyImmediate(target); Object.DestroyImmediate(image); Object.DestroyImmediate(before);
            }
        }
    }
}
