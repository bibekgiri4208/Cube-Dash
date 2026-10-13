using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public sealed class MainMenuSectionTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void ShopHasThreeEmptyEditableRarityCollectionsAndNoInventedSkinsOrPurchaseControls()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                MainMenuController menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Assert.That(menu.ShopPanel.name, Is.EqualTo("Shop - Skin Collections"));
                Assert.That(menu.ShopPanel.activeSelf, Is.False);
                Transform card = menu.ShopPanel.transform.Find("Card"); Assert.That(card, Is.Not.Null);
                foreach (string rarity in new[] { "Common", "Rare", "Epic" })
                {
                    Transform section = card.Find(rarity + " Skins"); Assert.That(section, Is.Not.Null);
                    Assert.That(section.Find("Rarity heading/Rarity label").GetComponent<Text>().text, Is.EqualTo(rarity.ToUpperInvariant()));
                    Transform slots = section.Find("Skin viewport/Skin slots"); Assert.That(slots, Is.Not.Null);
                    Assert.That(slots.childCount, Is.Zero, "These are empty sections, not placeholder skin products.");
                    Assert.That(slots.GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(2));
                    Assert.That(section.Find("Skin viewport").GetComponent<ScrollRect>().content, Is.SameAs(slots));
                    Assert.That(section.Find("Empty state/Empty title").GetComponent<Text>().text, Is.EqualTo("NO SKINS YET"));
                }
                Button[] controls = menu.ShopPanel.GetComponentsInChildren<Button>(true);
                Assert.That(controls.Length, Is.EqualTo(1)); Assert.That(controls[0].name, Is.EqualTo("BACK"));
                Assert.That(controls[0].onClick.GetPersistentTarget(0), Is.SameAs(menu));
                Assert.That(controls[0].onClick.GetPersistentMethodName(0), Is.EqualTo("ClosePanels"));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void OptionsKeepsLiveAudioAndDisplayControlsAndOldPanelsRemainRecoverable()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                MainMenuController menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Assert.That(menu.OptionsPanel.name, Is.EqualTo("Options - Audio and Display")); Assert.That(menu.OptionsPanel.activeSelf, Is.False);
                Slider slider = menu.OptionsPanel.GetComponentInChildren<Slider>(true);
                Toggle toggle = menu.OptionsPanel.GetComponentInChildren<Toggle>(true);
                Assert.That(slider.fillRect, Is.Not.Null); Assert.That(slider.handleRect, Is.Not.Null);
                Assert.That(slider.onValueChanged.GetPersistentTarget(0), Is.SameAs(menu));
                Assert.That(slider.onValueChanged.GetPersistentMethodName(0), Is.EqualTo("SetVolume"));
                Assert.That(toggle.onValueChanged.GetPersistentTarget(0), Is.SameAs(menu));
                Assert.That(toggle.onValueChanged.GetPersistentMethodName(0), Is.EqualTo("SetFullscreen"));
                Assert.That(toggle.graphic.GetComponent<MenuGlyphGraphic>().Symbol, Is.EqualTo(MenuGlyphGraphic.Glyph.Check));
                var serialized = new SerializedObject(menu);
                Assert.That(serialized.FindProperty("volume").objectReferenceValue, Is.SameAs(slider));
                Assert.That(serialized.FindProperty("fullscreen").objectReferenceValue, Is.SameAs(toggle));
                Assert.That(serialized.FindProperty("volumeReadout").objectReferenceValue, Is.Not.Null);
                Transform canvas = menu.GetComponentInChildren<Canvas>(true).transform;
                Assert.That(canvas.Find("Previous Shop Preview (inactive backup)").gameObject.activeSelf, Is.False);
                Assert.That(canvas.Find("Previous Options (inactive backup)").gameObject.activeSelf, Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ReapplyingThePanelUpgradePreservesExistingCollectionsAndManualEdits()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                MainMenuController menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                GameObject shop = menu.ShopPanel, options = menu.OptionsPanel;
                Text title = shop.transform.Find("Card/Heading").GetComponent<Text>(); title.text = "MY SKIN SHOP";
                int objectCount = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
                CubeDash.Editor.MainMenuSections.ApplyToScene(scene);
                Assert.That(menu.ShopPanel, Is.SameAs(shop)); Assert.That(menu.OptionsPanel, Is.SameAs(options));
                Assert.That(title.text, Is.EqualTo("MY SKIN SHOP"));
                Assert.That(scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(objectCount));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator PanelsNavigateAndVolumeReadoutTracksTheSliderWithoutCreatingSkins()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Main Menu.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
            bool hadVolume = PlayerPrefs.HasKey(MainMenuController.VolumePreference);
            float savedVolume = PlayerPrefs.GetFloat(MainMenuController.VolumePreference, 1), oldVolume = AudioListener.volume;
            try
            {
                menu.OpenShop(); yield return null;
                Assert.That(menu.ShopPanel.activeSelf, Is.True); Assert.That(menu.OptionsPanel.activeSelf, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(menu.ShopPanel.transform), Is.True);
                AssertHeadingRenders(menu.ShopPanel);
                AssertPointerRoutesTo(menu.ShopPanel.GetComponentInChildren<Button>(), menu);
                CaptureIfRequested(menu, "shop");
                menu.ShopPanel.GetComponentInChildren<Button>().onClick.Invoke();
                Assert.That(menu.ShopPanel.activeSelf, Is.False); Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("START"));
                menu.OpenOptions(); yield return null;
                AssertHeadingRenders(menu.OptionsPanel);
                Slider slider = menu.OptionsPanel.GetComponentInChildren<Slider>();
                Toggle toggle = menu.OptionsPanel.GetComponentInChildren<Toggle>();
                Canvas.ForceUpdateCanvases();
                Assert.That(slider.fillRect.rect.height, Is.EqualTo(14).Within(0.01f), "Slider-driven anchors must not stretch the narrow fill into a large bar.");
                Assert.That(slider.handleRect.rect.height, Is.EqualTo(42).Within(0.01f));
                AssertPointerRoutesTo(slider, menu);
                AssertPointerRoutesTo(toggle, menu);
                Text readout = menu.OptionsPanel.GetComponentsInChildren<Text>().Single(text => text.name == "Volume value");
                slider.value = 0.42f;
                Assert.That(AudioListener.volume, Is.EqualTo(0.42f).Within(0.001f)); Assert.That(readout.text, Is.EqualTo("42%"));
                Assert.That(PlayerPrefs.GetFloat(MainMenuController.VolumePreference), Is.EqualTo(0.42f).Within(0.001f));
                menu.SetVolume(2);
                Assert.That(slider.value, Is.EqualTo(1)); Assert.That(readout.text, Is.EqualTo("100%"));
                Assert.That(toggle.isOn, Is.EqualTo(Screen.fullScreen));
                Button back = menu.OptionsPanel.GetComponentInChildren<Button>();
                Slider musicSlider = menu.OptionsPanel.GetComponentsInChildren<Slider>().Single(control => control.name == "Music volume slider");
                AssertPointerRoutesTo(musicSlider, menu);
                Assert.That(slider.navigation.selectOnDown, Is.SameAs(musicSlider));
                Assert.That(musicSlider.navigation.selectOnDown, Is.SameAs(toggle));
                Assert.That(toggle.navigation.selectOnDown, Is.SameAs(back)); Assert.That(back.navigation.selectOnDown, Is.SameAs(slider));
                CaptureIfRequested(menu, "options");
                back.onClick.Invoke(); Assert.That(menu.OptionsPanel.activeSelf, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("START"));
            }
            finally
            {
                menu.SetVolume(savedVolume); AudioListener.volume = oldVolume;
                if (!hadVolume) PlayerPrefs.DeleteKey(MainMenuController.VolumePreference);
                menu.ClosePanels();
            }
        }

        private static void AssertHeadingRenders(GameObject panel)
        {
            var heading = panel.transform.Find("Card/Heading").GetComponent<MenuLabelText>();
            using (var mesh = new VertexHelper())
            {
                typeof(MenuLabelText).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new[] { typeof(VertexHelper) }, null).Invoke(heading, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.GreaterThanOrEqualTo(heading.text.Length * 4), "The title must fit its label and generate visible glyphs.");
            }
        }

        private static void AssertPointerRoutesTo(Selectable control, MainMenuController menu)
        {
            Canvas.ForceUpdateCanvases();
            var graphic = control.targetGraphic;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center))
            };
            var hits = new List<RaycastResult>();
            menu.GetComponentInChildren<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject), Is.SameAs(control.gameObject),
                "The visible control must receive pointer input rather than a decoration or underlying menu ribbon.");
        }

        private static void CaptureIfRequested(MainMenuController menu, string name)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_MENU_SECTIONS_PLAY_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            Camera camera = Camera.main; Canvas canvas = menu.GetComponentInChildren<Canvas>();
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            RenderMode oldMode = canvas.renderMode; Camera oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance;
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1672, 940, 24); var image = new Texture2D(1672, 940, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false; Canvas.ForceUpdateCanvases();
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1672, 940), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane;
                ShaderUtil.allowAsyncCompilation = oldAsync; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
