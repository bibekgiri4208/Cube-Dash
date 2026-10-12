using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class MainMenuTests
    {
        private const string MenuScene = "Assets/Scenes/Main Menu.unity";

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void MainMenuIsTheLandingSceneAndLevelRemainsEnabled()
        {
            var enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Assert.That(enabled[0].path, Is.EqualTo(MenuScene));
            Assert.That(enabled.Any(scene => scene.path == "Assets/Scenes/Level.unity"), Is.True);
        }

        [Test]
        public void MenuHasAuthoredEnvironmentCameraAndFourWiredRibbons()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MenuScene);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                var controller = roots.SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Assert.That(controller.GameplayScene, Is.EqualTo("Level"));
                Assert.That(controller.Hero.sharedMaterial.GetColor("_BaseColor").r, Is.GreaterThan(0.8f));
                Assert.That(controller.Hero.transform.parent.childCount, Is.EqualTo(3), "Body and two black eyes.");
                Assert.That(controller.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThan(200));
                var ribbons = controller.GetComponentsInChildren<MenuRibbonGraphic>(true);
                Assert.That(ribbons.Length, Is.EqualTo(4));
                CollectionAssert.AreEquivalent(new[] { "START", "SHOP", "OPTIONS", "QUIT" }, ribbons.Select(ribbon => ribbon.name));
                foreach (var ribbon in ribbons)
                {
                    var button = ribbon.GetComponent<Button>();
                    Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
                    Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(controller));
                    Assert.That(ribbon.GetComponentInChildren<Text>().font, Is.Not.Null);
                }
                Assert.That(controller.ShopPanel.activeSelf, Is.False);
                Assert.That(controller.OptionsPanel.activeSelf, Is.False);
                Assert.That(ShaderUtil.ShaderHasError(Shader.Find("CubeDash/Menu Surface")), Is.False);
                var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
                Assert.That(camera.fieldOfView, Is.EqualTo(50));
                Assert.That(camera.transform.position.y, Is.LessThan(4));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator ButtonsOpenPanelsChangePreviewAndLoadLevel()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MenuScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Shader.GetGlobalVector("_CubeDashRoadCurve"), Is.EqualTo(Vector4.zero));
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("START"));
            RenderPreviewIfRequested(menu);
            var ribbons = menu.GetComponentsInChildren<MenuRibbonGraphic>();
            ribbons.Single(ribbon => ribbon.name == "SHOP").GetComponent<Button>().onClick.Invoke();
            Assert.That(menu.ShopPanel.activeSelf, Is.True);
            Assert.That(menu.OptionsPanel.activeSelf, Is.False);
            menu.PreviewBlue();
            var appearance = new MaterialPropertyBlock(); menu.Hero.GetPropertyBlock(appearance);
            Assert.That(appearance.GetColor("_BaseColor").b, Is.GreaterThan(0.8f));
            Assert.That(menu.Hero.sharedMaterial.GetColor("_BaseColor").r, Is.GreaterThan(0.8f), "Preview must not modify the shared red material.");
            menu.PreviewRed();
            ribbons.Single(ribbon => ribbon.name == "OPTIONS").GetComponent<Button>().onClick.Invoke();
            Assert.That(menu.ShopPanel.activeSelf, Is.False);
            Assert.That(menu.OptionsPanel.activeSelf, Is.True);
            bool hadVolume = PlayerPrefs.HasKey(MainMenuController.VolumePreference);
            float savedVolume = PlayerPrefs.GetFloat(MainMenuController.VolumePreference, 1), oldVolume = AudioListener.volume;
            try
            {
                menu.SetVolume(0.37f);
                Assert.That(AudioListener.volume, Is.EqualTo(0.37f).Within(0.001f));
                Assert.That(PlayerPrefs.GetFloat(MainMenuController.VolumePreference), Is.EqualTo(0.37f).Within(0.001f));
            }
            finally
            {
                AudioListener.volume = oldVolume;
                if (hadVolume) PlayerPrefs.SetFloat(MainMenuController.VolumePreference, savedVolume);
                else PlayerPrefs.DeleteKey(MainMenuController.VolumePreference);
            }
            menu.ClosePanels();
            Assert.That(menu.OptionsPanel.activeSelf, Is.False);
            ribbons.Single(ribbon => ribbon.name == "START").GetComponent<Button>().onClick.Invoke();
            Assert.That(menu.Loading, Is.True);
            float deadline = Time.realtimeSinceStartup + 30;
            while (SceneManager.GetActiveScene().name != "Level" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level"));
            Assert.That(Object.FindAnyObjectByType<CubeDashGame>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<MainMenuController>(), Is.Null);
        }

        private static void RenderPreviewIfRequested(MainMenuController menu)
        {
            string path = System.Environment.GetEnvironmentVariable("CUBE_DASH_MENU_PLAY_PREVIEW");
            if (string.IsNullOrEmpty(path)) return;
            var camera = Camera.main;
            var canvas = menu.GetComponentInChildren<Canvas>();
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            float oldScale = canvas.scaleFactor;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1672, 940, 24);
            var image = new Texture2D(1672, 940, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                canvas.scaleFactor = 1;
                ShaderUtil.allowAsyncCompilation = false;
                camera.targetTexture = target; Canvas.ForceUpdateCanvases();
                for (int frame = 0; frame < 3; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1672, 940), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Vector3 probe = camera.WorldToScreenPoint(menu.Hero.transform.position + Vector3.up * 0.3f);
                Color pixel = image.GetPixel(Mathf.RoundToInt(probe.x), Mathf.RoundToInt(probe.y));
                Assert.That(pixel.r, Is.GreaterThan(pixel.g * 1.5f), "The rendered hero must remain red.");
                Assert.That(image.GetPixel(250, 620).r, Is.GreaterThan(0.5f), "The Start ribbon must be visible.");
                Assert.That(image.GetPixel(720, 220).grayscale, Is.LessThan(0.6f), "Asphalt must not render as pale concrete or an asynchronous placeholder shader.");
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
                canvas.scaleFactor = oldScale; ShaderUtil.allowAsyncCompilation = asyncCompilation;
                Object.Destroy(target); Object.Destroy(image);
            }
        }
    }
}
