using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
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
    public sealed class MainMenuMapSelectionTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void SavedPanelHasSixThemedIllustratedMapsHonestAvailabilityAndPreservedReapplication()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Assert.That(menu.MapPanel, Is.Not.Null); Assert.That(menu.MapPanel.activeSelf, Is.False);
                var card = menu.MapPanel.transform.Find("Card");
                Assert.That(card.GetComponent<MenuPanelGraphic>().color, Is.EqualTo(LevelMenuTheme.Paper));
                Assert.That(menu.MapPanel.GetComponent<MenuPanelMotion>(), Is.Not.Null);
                Assert.That(menu.MapPanel.GetComponent<MenuMapLayout>(), Is.Not.Null);
                Assert.That(menu.MapPanel.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(7));
                for (int i = 0; i < MainMenuMapSelection.MapNames.Length; i++)
                {
                    Transform tile = card.Find(MainMenuMapSelection.MapNames[i]); var button = tile.GetComponent<Button>();
                    bool ready = true;
                    Assert.That(button.interactable, Is.EqualTo(ready));
                    Assert.That(tile.Find("Map illustration").GetComponent<MapPreviewGraphic>().Kind, Is.EqualTo((MapPreviewGraphic.Map)i));
                    var artwork = tile.Find("Map illustration").GetComponent<MapPreviewGraphic>().Illustration;
                    Assert.That(artwork, Is.Not.Null);
                    Assert.That(AssetDatabase.GetAssetPath(artwork), Is.EqualTo(MainMenuMapIllustrations.ImagePath(MainMenuMapSelection.MapNames[i])));
                    Assert.That(artwork.width, Is.EqualTo(MainMenuMapIllustrations.Width));
                    Assert.That(artwork.height, Is.EqualTo(MainMenuMapIllustrations.Height));
                    Assert.That(tile.Find("Map illustration").GetComponent<CanvasRenderer>(), Is.Not.Null);
                    Assert.That(tile.Find("Availability badge/Availability").GetComponent<Text>().text, Is.EqualTo(ready ? "PLAY" : "COMING SOON"));
                    Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(menu));
                    Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(MainMenuController.StartMap)));
                    var data = new SerializedObject(button);
                    string target = data.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0).FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
                    Assert.That(target, Is.EqualTo(MainMenuMapSelection.SceneNameFor(MainMenuMapSelection.MapNames[i])));
                    Assert.That(tile.Find("Map name").GetComponent<Text>().text, Is.EqualTo(MainMenuMapSelection.MapNames[i].ToUpperInvariant()));
                    Assert.That(button.GetComponent<ArcadeButton>().LivelyFeedback, Is.True);
                }
                Text title = card.Find("Heading").GetComponent<Text>(); title.text = "MY MAPS";
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                MainMenuMapSelection.ApplyToScene(scene); MainMenuUiAnimations.ApplyToScene(scene);
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(title.text, Is.EqualTo("MY MAPS"));
                Assert.That(card.Find("Mountain"), Is.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void DynamicUsesLevelAndStandaloneMountainCannotBeBuiltOrRecreated()
        {
            Assert.That(MainMenuMapSelection.MapNames, Does.Contain("Dynamic").And.Not.Contain("Mountain"));
            Assert.That(MainMenuMapSelection.SceneNameFor("Dynamic"), Is.EqualTo("Level"));
            Assert.That(StandaloneMapScenes.MapNames, Does.Not.Contain("Mountain").And.Not.Contain("Dynamic"));
            Assert.That(StandaloneMapScenes.MapBiomes, Is.EqualTo(new[] { EnvironmentBiome.City, EnvironmentBiome.Jungle, EnvironmentBiome.Desert, EnvironmentBiome.Beach }));
            Assert.That(File.Exists("Assets/Scenes/Mountain.unity"), Is.False);
            Assert.That(File.Exists("Assets/Scenes/Mountain.unity.meta"), Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(entry => entry.path == "Assets/Scenes/Mountain.unity"), Is.False);
            Assert.That(EditorBuildSettings.scenes.Count(entry => entry.enabled && entry.path == StandaloneMapScenes.SourcePath), Is.EqualTo(1));
            var scene = EditorSceneManager.OpenPreviewScene(StandaloneMapScenes.SourcePath);
            try
            {
                var environment = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnvironmentDirector>(true)).Single();
                Assert.That(environment.HasFixedBiome, Is.False);
                var track = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndlessTrack>(true)).Single();
                Assert.That(track.Segments.All(segment => segment.Environment.Biomes.All(biome => biome != null)), Is.True,
                    "Dynamic retains all five biomes, including mountains.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ReapplyingMigratesTheOldMountainCardInPlaceAndKeepsCustomListeners()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Transform card = menu.MapPanel.transform.Find("Card"), tile = card.Find("Dynamic");
                var button = tile.GetComponent<Button>(); var rect = tile.GetComponent<RectTransform>();
                Vector2 position = rect.anchoredPosition, size = rect.sizeDelta;
                tile.name = "Mountain"; tile.Find("Map name").GetComponent<Text>().text = "MOUNTAIN";
                UnityEventTools.AddPersistentListener(button.onClick, menu.ClosePanels);
                int objects = menu.GetComponentsInChildren<Transform>(true).Length, listeners = button.onClick.GetPersistentEventCount();
                MainMenuMapSelection.ApplyToScene(scene);
                Assert.That(card.Find("Mountain"), Is.Null); Assert.That(card.Find("Dynamic"), Is.SameAs(tile));
                Assert.That(tile.Find("Map name").GetComponent<Text>().text, Is.EqualTo("DYNAMIC"));
                Assert.That(rect.anchoredPosition, Is.EqualTo(position)); Assert.That(rect.sizeDelta, Is.EqualTo(size));
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(listeners));
                Assert.That(button.onClick.GetPersistentMethodName(listeners - 1), Is.EqualTo(nameof(MainMenuController.ClosePanels)));
                var data = new SerializedObject(button);
                Assert.That(data.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("m_Arguments.m_StringArgument").stringValue, Is.EqualTo("Level"));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator StartOpensSelectionWithoutStoppingMusicAndBackEscapeRestoreStartFocus()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var start = menu.GetComponentsInChildren<Button>().Single(button => button.name == "START");
            ExecuteEvents.Execute(start.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(menu.CurrentPanel, Is.SameAs(menu.MapPanel)); Assert.That(menu.Loading, Is.False);
            Assert.That(menu.Music.isPlaying, Is.True); Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("City"));
            menu.StartMap("");
            Assert.That(menu.Loading, Is.False, "An empty map target must not load a missing scene.");
            Assert.That(menu.Music.isPlaying, Is.True);
            menu.MapPanel.transform.Find("Card/BACK").GetComponent<Button>().onClick.Invoke();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(start.gameObject));
            yield return MainMenuSectionTests.WaitForPanelClosed(menu.MapPanel);
            menu.StartGame();
            var oldBackground = InputSystem.settings.backgroundBehavior; var oldFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>("MapSelectionKeyboard");
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(menu.CurrentPanel, Is.Null); Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(start.gameObject));
                yield return MainMenuSectionTests.WaitForPanelClosed(menu.MapPanel);
            }
            finally { InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldFocus; }
        }

        [Test]
        public void NavigationFollowsDesktopAndPortraitGridAndReachesAllSixMapsAndBack()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                var layout = menu.MapPanel.GetComponent<MenuMapLayout>();
                var buttons = MainMenuMapSelection.MapNames.Select(name => menu.MapPanel.transform.Find("Card/" + name).GetComponent<Button>()).ToArray();
                Button back = menu.MapPanel.transform.Find("Card/BACK").GetComponent<Button>();
                foreach (int columns in new[] { 3, 2 })
                {
                    layout.ConfigureNavigation(columns);
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        Navigation navigation = buttons[i].navigation;
                        Assert.That(navigation.selectOnDown, Is.SameAs(i + columns < buttons.Length ? buttons[i + columns] : back));
                        Assert.That(navigation.selectOnUp, Is.SameAs(i - columns >= 0 ? buttons[i - columns] : back));
                        Assert.That(navigation.selectOnRight, Is.SameAs(i + 1 < buttons.Length ? buttons[i + 1] : back));
                    }
                    Assert.That(back.navigation.selectOnDown, Is.SameAs(buttons[0]));
                    Assert.That(back.navigation.selectOnUp, Is.SameAs(buttons[5]));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator GamepadNavigatesOnlyPlayableCardsAndBackRestoresStart()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.StartGame();
            var oldBackground = InputSystem.settings.backgroundBehavior; var oldFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pad = InputSystem.AddDevice<Gamepad>("MapSelectionPad");
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
                string belowCity = menu.MapPanel.transform.Find("Card/City").GetComponent<Button>().navigation.selectOnDown.name;
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(belowCity));
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                for (int i = 0; i < 3 && EventSystem.current.currentSelectedGameObject.name != "BACK"; i++)
                {
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
                    InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                }
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("BACK"));
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
                Assert.That(menu.CurrentPanel, Is.Null); Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("START"));
                yield return MainMenuSectionTests.WaitForPanelClosed(menu.MapPanel);
            }
            finally { InputSystem.RemoveDevice(pad); InputSystem.settings.backgroundBehavior = oldBackground; InputSystem.settings.editorInputBehaviorInPlayMode = oldFocus; }
        }

        [UnityTest]
        public IEnumerator PlayableMapCardsLoadCountrySideAndCityAndReturningToMenuStillWorks()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.StartGame();
            Button country = menu.MapPanel.transform.Find("Card/Country Side").GetComponent<Button>();
            Assert.That(menu.MapPanel.GetComponentsInChildren<Button>(true).All(button => button.interactable), Is.True);
            EventSystem.current.SetSelectedGameObject(country.gameObject);
            ExecuteEvents.Execute(country.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(menu.Loading, Is.True); Assert.That(menu.Music.isPlaying, Is.False);
            float deadline = Time.realtimeSinceStartup + 25;
            while ((SceneManager.GetActiveScene().name != "Country Side" || SceneLoader.IsLoading) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Country Side"));
            var game = Object.FindAnyObjectByType<CubeDashGame>(); Assert.That(game, Is.Not.Null);
            game.StartRun(); game.TogglePause(); Object.FindAnyObjectByType<RunnerHud>().QuitGame();
            deadline = Time.realtimeSinceStartup + 25;
            while ((SceneManager.GetActiveScene().name != "Main Menu" || SceneLoader.IsLoading) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            menu = Object.FindAnyObjectByType<MainMenuController>(); Assert.That(menu, Is.Not.Null);
            menu.StartGame(); menu.MapPanel.transform.Find("Card/City").GetComponent<Button>().onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 25;
            while ((SceneManager.GetActiveScene().name != "City" || SceneLoader.IsLoading) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("City"));
            Assert.That(Object.FindAnyObjectByType<CubeDashGame>().CountdownActive, Is.True);
        }

        [UnityTest]
        public IEnumerator MapPanelSwitchingAndResponsivePreviewsKeepEveryCardInsideTheThemedPanel()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.OpenShop(); menu.StartGame();
            Assert.That(menu.ShopPanel.GetComponent<CanvasGroup>().interactable, Is.False);
            Assert.That(menu.CurrentPanel, Is.SameAs(menu.MapPanel));
            typeof(MenuPanelMotion).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu.MapPanel.GetComponent<MenuPanelMotion>(), new object[] { 1f });
            Capture(menu, "maps-landscape", 1672, 940); Capture(menu, "maps-16-10", 1920, 1200); Capture(menu, "maps-portrait", 600, 1000);
            var card = menu.MapPanel.transform.Find("Card").GetComponent<RectTransform>();
            foreach (string name in MainMenuMapSelection.MapNames)
            {
                var tile = card.Find(name).GetComponent<RectTransform>();
                Assert.That(card.rect.Contains(tile.anchoredPosition), Is.True, name);
                var preview = tile.Find("Map illustration").GetComponent<MapPreviewGraphic>();
                using (var mesh = new VertexHelper())
                {
                    typeof(MapPreviewGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(preview, new object[] { mesh });
                    Assert.That(mesh.currentVertCount, Is.EqualTo(4), name + " displays its actual scene texture, not generic vector artwork.");
                }
                foreach (Text text in tile.GetComponentsInChildren<Text>())
                    using (var mesh = new VertexHelper())
                    {
                        typeof(MenuLabelText).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(text, new object[] { mesh });
                        Assert.That(mesh.currentVertCount, Is.GreaterThan(0), text.text);
                    }
            }
            menu.ClosePanels(); Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("START"));
        }

        private static void Capture(MainMenuController menu, string name, int width, int height)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_MAP_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            Camera camera = Camera.main; Canvas canvas = menu.GetComponentInChildren<Canvas>();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance; bool oldAsync = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false; Canvas.ForceUpdateCanvases();
                menu.MapPanel.GetComponent<MenuMapLayout>().Refresh(); Canvas.ForceUpdateCanvases();
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane; ShaderUtil.allowAsyncCompilation = oldAsync;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image); Canvas.ForceUpdateCanvases();
                menu.MapPanel.GetComponent<MenuMapLayout>().Refresh(); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
