using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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

        [Test]
        public void ShopAndOptionsHideHelperCopyButKeepFunctionalLabelsAndEmptyStatus()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                AssertHelperCopyHidden(menu);
                Assert.That(menu.ShopPanel.transform.Find("Card/Heading").gameObject.activeSelf, Is.True);
                Assert.That(menu.OptionsPanel.transform.Find("Card/Heading").gameObject.activeSelf, Is.True);
                foreach (string rarity in new[] { "Common", "Rare", "Epic" })
                {
                    Transform section = menu.ShopPanel.transform.Find("Card/" + rarity + " Skins");
                    Assert.That(section.Find("Rarity heading/Rarity label").gameObject.activeSelf, Is.True);
                    Assert.That(section.Find("Empty state/Empty title").gameObject.activeSelf, Is.True);
                }
                foreach (string path in new[] { "Audio section/Volume heading", "Audio section/Music volume heading", "Display section/Fullscreen heading" })
                    Assert.That(menu.OptionsPanel.transform.Find("Card/" + path).gameObject.activeSelf, Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void NewlyAuthoredPanelsDoNotRestoreInstructions()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Menu authoring fixture"); SceneManager.MoveGameObjectToScene(root, scene);
                var menu = root.AddComponent<MainMenuController>();
                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); canvas.transform.SetParent(root.transform, false);
                CubeDash.Editor.MainMenuSections.ApplyToScene(scene);
                AssertHelperCopyHidden(menu);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void AssertHelperCopyHidden(MainMenuController menu)
        {
            foreach (GameObject panel in new[] { menu.ShopPanel, menu.OptionsPanel })
                Assert.That(panel.transform.Find("Card/Description").gameObject.activeSelf, Is.False);
            Assert.That(menu.ShopPanel.transform.Find("Card/Availability note").gameObject.activeSelf, Is.False);
            Assert.That(menu.OptionsPanel.transform.Find("Card/Settings note").gameObject.activeSelf, Is.False);
            Assert.That(menu.OptionsPanel.transform.Find("Card/Display section/Fullscreen explanation").gameObject.activeSelf, Is.False);
            foreach (string rarity in new[] { "Common", "Rare", "Epic" })
                Assert.That(menu.ShopPanel.transform.Find("Card/" + rarity + " Skins/Empty state/Empty explanation").gameObject.activeSelf, Is.False);
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
                Assert.That(menu.ShopPanel.activeSelf, Is.False); Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("SHOP"));
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
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("OPTIONS"));
            }
            finally
            {
                menu.SetVolume(savedVolume); AudioListener.volume = oldVolume;
                if (!hadVolume) PlayerPrefs.DeleteKey(MainMenuController.VolumePreference);
                menu.ClosePanels();
            }
        }

        [Test]
        public void ShopAndOptionsUseTheMainMenuRibbonTheme()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                foreach (GameObject panel in new[] { menu.ShopPanel, menu.OptionsPanel })
                {
                    Transform card = panel.transform.Find("Card");
                    var face = card.GetComponent<MenuPanelGraphic>();
                    Assert.That(face.CornerRadius, Is.Zero);
                    Assert.That(face.CornerCut, Is.EqualTo(22));
                    Assert.That(face.BorderWidth, Is.EqualTo(4));
                    Assert.That(face.color, Is.EqualTo(new Color(1, 0.82f, 0.84f)));
                    Assert.That(card.Find("Ribbon header").GetComponent<MenuRibbonGraphic>().Primary, Is.False);
                    Assert.That(card.Find("Ribbon header").GetComponent<MenuRibbonGraphic>().raycastTarget, Is.False);
                    var back = card.GetComponentInChildren<Button>(true);
                    Assert.That(back.targetGraphic, Is.SameAs(back.GetComponent<MenuRibbonGraphic>()));
                    Assert.That(back.GetComponent<MenuRibbonGraphic>().Primary, Is.True);
                    Assert.That(back.transition, Is.EqualTo(Selectable.Transition.None));
                }
                foreach (string rarity in new[] { "Common", "Rare", "Epic" })
                    Assert.That(menu.ShopPanel.transform.Find("Card/" + rarity + " Skins/Rarity heading").GetComponent<MenuRibbonGraphic>(), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator ClosingPanelsRestoresTheirOpenerForPointerSubmitEscapeAndGamepadBack()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Main Menu.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Button shop = menu.GetComponentsInChildren<Button>(true).Single(button => button.name == "SHOP");
            Button options = menu.GetComponentsInChildren<Button>(true).Single(button => button.name == "OPTIONS");
            var background = InputSystem.settings.backgroundBehavior;
            var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>("MenuReturnKeyboard");
            var pad = InputSystem.AddDevice<Gamepad>("MenuReturnGamepad");
            try
            {
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(shop.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(shop.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(menu.ShopPanel.activeSelf, Is.True);
                menu.ShopPanel.GetComponentInChildren<Button>().onClick.Invoke();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(shop.gameObject));
                menu.ClosePanels();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(shop.gameObject), "Closing nothing must not jump to Start.");

                EventSystem.current.SetSelectedGameObject(options.gameObject);
                ExecuteEvents.Execute(options.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Assert.That(menu.OptionsPanel.activeSelf, Is.True);
                EventSystem.current.SetSelectedGameObject(menu.OptionsPanel.GetComponentInChildren<Slider>().gameObject);
                keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(menu.OptionsPanel.activeSelf, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(options.gameObject));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;

                menu.OpenShop();
                menu.OpenOptions();
                Assert.That(menu.ShopPanel.activeSelf, Is.False);
                Assert.That(menu.OptionsPanel.activeSelf, Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null;
                Assert.That(menu.OptionsPanel.activeSelf, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(options.gameObject));
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;

                menu.OpenShop();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null;
                Assert.That(menu.ShopPanel.activeSelf, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(shop.gameObject));
            }
            finally
            {
                menu.ClosePanels();
                InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(pad);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            }
        }

        [UnityTest]
        public IEnumerator SliderFillMeetsBothInnerTrackEdgesWithoutAnUnfilledLeadingGap()
        {
            // Preview scenes do not initialize Slider's cached fill-container layout; check the live UI.
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Main Menu.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            try
            {
                menu.OpenOptions(); yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (Slider slider in menu.OptionsPanel.GetComponentsInChildren<Slider>(true))
                {
                    var rail = slider.transform.Find("Track").GetComponent<MenuPanelGraphic>();
                    var fill = slider.fillRect.GetComponent<MenuPanelGraphic>();
                    Assert.That(fill.CornerRadius, Is.Zero, "Square fill caps avoid a pale sliver at the starting edge.");
                    foreach (float value in new[] { 0f, 0.01f, 0.42f, 1f })
                    {
                        slider.SetValueWithoutNotify(value);
                        var corners = new Vector3[4]; slider.fillRect.GetWorldCorners(corners);
                        float left = rail.rectTransform.InverseTransformPoint(corners[0]).x;
                        float right = rail.rectTransform.InverseTransformPoint(corners[2]).x;
                        float innerWidth = rail.rectTransform.rect.width - rail.BorderWidth * 2;
                        Assert.That(left, Is.EqualTo(rail.rectTransform.rect.xMin + rail.BorderWidth).Within(0.01f));
                        Assert.That(right - left, Is.EqualTo(innerWidth * value).Within(0.01f));
                    }
                }
            }
            finally { menu.ClosePanels(); }
        }

        [Test]
        public void ReapplyingRibbonThemeDoesNotReplaceControlsOrCollectionContents()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                var controls = menu.OptionsPanel.GetComponentsInChildren<Selectable>(true);
                Transform slots = menu.ShopPanel.transform.Find("Card/Common Skins/Skin viewport/Skin slots");
                var item = new GameObject("Authored skin content"); item.transform.SetParent(slots, false);
                var heading = menu.ShopPanel.transform.Find("Card/Heading").GetComponent<Text>(); heading.text = "MY SHOP";
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                CubeDash.Editor.MainMenuPanelTheme.ApplyToScene(scene);
                CubeDash.Editor.MainMenuPanelTheme.ApplyToScene(scene);
                CollectionAssert.AreEqual(controls, menu.OptionsPanel.GetComponentsInChildren<Selectable>(true));
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(heading.text, Is.EqualTo("MY SHOP"));
                Assert.That(slots.GetChild(0), Is.SameAs(item.transform));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
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
