using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class MainMenuSakuraBlossomTests
    {
        private const string MenuMaterial = "Assets/MainMenu/Sakura/Menu Sakura Blossoms.mat";
        private const string SourceMaterial = "Assets/Idyllic Fantasy Nature/Materials/Trees/Tree_Blossoms.mat";

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void AllBlossomLodsIncludingManuallyPlacedTreesUseMenuOnlyHdrMaterialWhileBarkIsUnchanged()
        {
            Material blossoms = AssetDatabase.LoadAssetAtPath<Material>(MenuMaterial);
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterial);
            Assert.That(blossoms, Is.Not.Null); Assert.That(blossoms, Is.Not.SameAs(source));
            Assert.That(AssetDatabase.GetAssetPath(blossoms.shader), Is.EqualTo("Assets/MainMenu/Sakura/Menu Sakura Blossoms.shadergraph"));
            Assert.That(ShaderUtil.ShaderHasError(blossoms.shader), Is.False); Assert.That(blossoms.shader.isSupported, Is.True);
            Assert.That(blossoms.GetColor("_Top_Color").r, Is.GreaterThan(1));
            Assert.That(blossoms.GetColor("_Bottom_Color").r, Is.GreaterThanOrEqualTo(1));
            Assert.That(blossoms.GetColor("_Top_Color").r, Is.GreaterThan(blossoms.GetColor("_Top_Color").g * 2.5f), "Keep rich pink, not near-white HDR tints.");
            Assert.That(blossoms.GetFloat("_Menu_Blossom_Glow"), Is.InRange(0.7f, 1f), "Avoid overdriving the textured emission and washing out petals.");
            Assert.That(blossoms.GetTexture("_Texture"), Is.SameAs(source.GetTexture("_Texture")));
            Assert.That(blossoms.GetFloat("_Alpha_Cutoff"), Is.EqualTo(source.GetFloat("_Alpha_Cutoff")));
            Assert.That(blossoms.GetFloat("_Wind_Strength"), Is.EqualTo(source.GetFloat("_Wind_Strength")));
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                var trees = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>()).Where(tree =>
                    PrefabUtility.IsAnyPrefabInstanceRoot(tree.gameObject) &&
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree.gameObject).StartsWith("Assets/Idyllic Fantasy Nature/Prefabs/BlossomTree_")).ToArray();
                Assert.That(trees.Length, Is.GreaterThanOrEqualTo(3));
                int blossomSlots = 0;
                foreach (Transform tree in trees)
                {
                    GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(tree.gameObject);
                    Renderer[] sourceRenderers = prefab.GetComponentsInChildren<Renderer>(true);
                    Renderer[] renderers = tree.GetComponentsInChildren<Renderer>(true);
                    for (int index = 0; index < renderers.Length; index++)
                    {
                        Assert.That(renderers[index].GetComponent<MeshFilter>().sharedMesh,
                            Is.SameAs(sourceRenderers[index].GetComponent<MeshFilter>().sharedMesh));
                        Assert.That(renderers[index].shadowCastingMode, Is.EqualTo(sourceRenderers[index].shadowCastingMode));
                        Assert.That(renderers[index].receiveShadows, Is.EqualTo(sourceRenderers[index].receiveShadows));
                        for (int slot = 0; slot < sourceRenderers[index].sharedMaterials.Length; slot++)
                        {
                            Material original = sourceRenderers[index].sharedMaterials[slot];
                            Material actual = renderers[index].sharedMaterials[slot];
                            if (original == source) { Assert.That(actual, Is.SameAs(blossoms)); blossomSlots++; }
                            else Assert.That(actual, Is.SameAs(original), "Do not brighten trunks or other foliage.");
                        }
                    }
                }
                Assert.That(blossomSlots, Is.EqualTo(trees.Length * 3));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator MenuBlossomsKeepRicherPinkThanTheWashedOutTuningWithoutReturningToDarkPackageShading()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU rendering required.");
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Main Menu.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var camera = Camera.main;
            var canvas = menu.GetComponentInChildren<Canvas>();
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterial);
            Material blossoms = AssetDatabase.LoadAssetAtPath<Material>(MenuMaterial);
            var washedOut = new Material(blossoms);
            washedOut.SetColor("_Top_Color", new Color(1.3f, 0.6f, 0.85f, 1));
            washedOut.SetColor("_Bottom_Color", new Color(1.08f, 0.36f, 0.56f, 1));
            washedOut.SetFloat("_Menu_Blossom_Glow", 1.2f);
            Renderer[] renderers = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>()).ToArray();
            var target = new RenderTexture(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            bool oldCanvas = canvas.enabled, oldAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                canvas.enabled = false; ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                Swap(renderers, blossoms, source);
                Color[] before = Capture(camera, target, image, "before");
                Swap(renderers, source, washedOut);
                Color[] pale = Capture(camera, target, image, "washed-out");
                Swap(renderers, washedOut, blossoms);
                Color[] after = Capture(camera, target, image, "after");
                float oldBrightness = 0, newBrightness = 0, paleBrightness = 0, saturationGain = 0;
                float darkest = 1, lightest = 0; int petals = 0;
                for (int y = 315; y < 535; y++)
                    for (int x = 775; x < 955; x++)
                    {
                        int index = y * 960 + x;
                        Color old = before[index];
                        if (old.r > 0.25f && old.r > old.g * 1.15f && old.b > old.g * 1.15f)
                        {
                            oldBrightness += old.grayscale; newBrightness += after[index].grayscale; petals++;
                            paleBrightness += pale[index].grayscale;
                            Color.RGBToHSV(pale[index], out _, out float paleSaturation, out _);
                            Color.RGBToHSV(after[index], out _, out float saturation, out _);
                            saturationGain += saturation - paleSaturation;
                            darkest = Mathf.Min(darkest, after[index].grayscale);
                            lightest = Mathf.Max(lightest, after[index].grayscale);
                        }
                    }
                Debug.Log($"Sakura color comparison: {petals} petal pixels, brightness gain over package {(newBrightness - oldBrightness) / Mathf.Max(1, petals):F3}, saturation gain over washed-out colors {saturationGain / Mathf.Max(1, petals):F3}, reduction in washed-out brightness {(paleBrightness - newBrightness) / Mathf.Max(1, petals):F3}.");
                Assert.That(petals, Is.GreaterThan(100));
                Assert.That((newBrightness - oldBrightness) / petals, Is.GreaterThan(0.015f), "Do not return to the original dark canopy.");
                Assert.That(saturationGain / petals, Is.GreaterThan(0.05f), "Petals must render richer pink than the pale, over-bright tuning.");
                Assert.That((paleBrightness - newBrightness) / petals, Is.GreaterThan(0.025f), "Reduce the washed-out luminance rather than boosting exposure again.");
                Assert.That(lightest - darkest, Is.GreaterThan(0.15f), "Keep visible petal and canopy contrast.");
            }
            finally
            {
                Swap(renderers, source, blossoms);
                Swap(renderers, washedOut, blossoms);
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; canvas.enabled = oldCanvas;
                ShaderUtil.allowAsyncCompilation = oldAsync;
                Object.Destroy(target); Object.Destroy(image); Object.Destroy(washedOut);
            }
        }

        private static void Swap(Renderer[] renderers, Material from, Material to)
        {
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++) if (materials[slot] == from) materials[slot] = to;
                renderer.sharedMaterials = materials;
            }
        }

        private static Color[] Capture(Camera camera, RenderTexture target, Texture2D image, string name)
        {
            for (int frame = 0; frame < 3; frame++) camera.Render();
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_SAKURA_PREVIEW_PREFIX");
            if (!string.IsNullOrEmpty(prefix)) System.IO.File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            return image.GetPixels();
        }
    }
}
