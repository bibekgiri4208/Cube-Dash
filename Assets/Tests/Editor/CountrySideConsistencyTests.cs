using System.Collections;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class CountrySideConsistencyTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void SavedCountrySideMatchesLevelRoadsCameraPlayerGameplayAndCompleteThemedUi()
        {
            Scene source = EditorSceneManager.OpenPreviewScene(StandaloneMapScenes.SourcePath);
            Scene country = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var original = Find<CubeDashGame>(source); var game = Find<CubeDashGame>(country);
                Assert.That(Canonical(StandaloneMapSceneTests.Snapshot(game)), Is.EqualTo(StandaloneMapSceneTests.Snapshot(original)));
                Assert.That(Canonical(StandaloneMapSceneTests.HierarchySnapshot(game.Player)), Is.EqualTo(StandaloneMapSceneTests.HierarchySnapshot(original.Player)));
                Assert.That(StandaloneMapSceneTests.Snapshot(game.GameCamera), Is.EqualTo(StandaloneMapSceneTests.Snapshot(original.GameCamera)));
                Assert.That(StandaloneMapSceneTests.Snapshot(game.GameCamera.transform), Is.EqualTo(StandaloneMapSceneTests.Snapshot(original.GameCamera.transform)));
                Assert.That(Canonical(StandaloneMapSceneTests.HierarchySnapshot(Find<Canvas>(country).transform)),
                    Is.EqualTo(StandaloneMapSceneTests.HierarchySnapshot(Find<Canvas>(source).transform)));
                var hud = Find<RunnerHud>(country);
                Assert.That(hud.RibbonTheme && game.UsesStartCountdown, Is.True); Assert.That(hud.PowerUpBars, Is.Not.Null);
                Assert.That(hud.CountdownNumber.text, Is.EqualTo("3"));
                for (int i = 0; i < game.Track.Segments.Length; i++)
                    foreach (string name in new[] { "Road", "Plane", "Highway Shoulders", "Highway Lane Markings", "Highway Guardrails" })
                    {
                        Transform road = game.Track.Segments[i].transform.Find(name), old = original.Track.Segments[i].transform.Find(name);
                        if (old == null) Assert.That(road, Is.Null);
                        else Assert.That(Canonical(StandaloneMapSceneTests.HierarchySnapshot(road)), Is.EqualTo(StandaloneMapSceneTests.HierarchySnapshot(old)), name);
                    }
                var settings = new SerializedObject(game.Track);
                Assert.That(settings.FindProperty("roadCurveStrength").floatValue, Is.EqualTo(new SerializedObject(original.Track).FindProperty("roadCurveStrength").floatValue));
                Assert.That(settings.FindProperty("patternedEncounters").boolValue, Is.True);
                Assert.That(game.Track.Environment.HasFixedBiome, Is.True);
                Assert.That(game.Track.Segments.All(segment => segment.Environment == null), Is.True, "Only Japanese scenery, never Level's mixed-biome roots.");
                var volume = country.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true)).Single(component => component.enabled);
                Assert.That(volume.sharedProfile, Is.SameAs(Find<Volume>(source).sharedProfile));
                Material sky = AssetDatabase.LoadAssetAtPath<Material>(CountrySideConsistentBuilder.SkyPath);
                Assert.That(sky.shader.name, Is.EqualTo("CubeDash/Horizon Skybox"));
                Assert.That(ShaderUtil.ShaderHasError(sky.shader), Is.False);
                Assert.That(AssetDatabase.AssetPathToGUID(CountrySideSceneBuilder.ScenePath), Is.EqualTo("7c5df91fe690b844fbe03b51fa4a5fa7"));
            }
            finally { EditorSceneManager.ClosePreviewScene(country); EditorSceneManager.ClosePreviewScene(source); }
        }

        [Test]
        public void SavedLinkedJapaneseArtHasNativeMaterialsWindLodsAndOneHighwayWithSafeReapplication()
        {
            Scene country = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = Find<CountrySideRunnerScenery>(country);
                Assert.That(scenery.FollowsRoadCurves, Is.True); Assert.That(scenery.CurvePlants.Length, Is.GreaterThan(1000));
                Assert.That(scenery.CurvedSurfaces.Length, Is.GreaterThan(30));
                Assert.That(scenery.CurvedSurfaces.All(surface => !scenery.CurvePlants.Any(plant => surface.transform.IsChildOf(plant))), Is.True);
                foreach (Transform plant in scenery.CurvePlants)
                {
                    Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(plant.gameObject), Does.StartWith("Assets/Idyllic Fantasy Nature/Prefabs/"));
                    foreach (Material material in plant.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials))
                        Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/Idyllic Fantasy Nature/"));
                    Assert.That(plant.GetComponentsInChildren<Collider>(true), Is.Empty);
                }
                Assert.That(scenery.CurvePlants.Any(plant => plant.GetComponent<LODGroup>() != null), Is.True);
                foreach (Transform tile in scenery.Tiles)
                {
                    Assert.That(tile.Find("Runner road - clear 8.4 metre corridor").gameObject.activeSelf, Is.False, "Do not render two overlapping roads.");
                    Assert.That(tile.Find("Sakura avenue").childCount, Is.EqualTo(10));
                    Assert.That(tile.Find("Flower fields - linked Idyllic meadows").childCount, Is.EqualTo(6));
                    Assert.That(tile.Find("Village - original Japanese minka and kura").childCount, Is.EqualTo(6));
                    Assert.That(tile.Find("Rice paddies - flooded seedling terraces").childCount, Is.EqualTo(4));
                    Assert.That(tile.Find("Roadside shrine and stone lanterns"), Is.Not.Null);
                }
                Transform house = scenery.Tiles[0].Find("Village - original Japanese minka and kura").GetChild(0);
                house.localPosition += Vector3.right; Vector3 position = house.localPosition;
                int count = country.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
                CountrySideConsistentBuilder.ApplyToScene(country);
                Assert.That(house.localPosition, Is.EqualTo(position));
                Assert.That(country.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(count));
                var other = EditorSceneManager.OpenPreviewScene(StandaloneMapScenes.SourcePath);
                try { Assert.Throws<System.InvalidOperationException>(() => CountrySideConsistentBuilder.ApplyToScene(other)); }
                finally { EditorSceneManager.ClosePreviewScene(other); }
            }
            finally { EditorSceneManager.ClosePreviewScene(country); }
        }

        [UnityTest]
        public IEnumerator JapaneseSceneryFollowsCurvesWithoutReplacingNativeMeshesOrMaterialsAndFreezesResetsAndReturnsToMenu()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CountrySideSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var scenery = Object.FindAnyObjectByType<CountrySideRunnerScenery>();
            var hud = Object.FindAnyObjectByType<RunnerHud>();
            Assert.That(game.CountdownActive, Is.True); Assert.That(hud.CountdownNumber.text, Is.EqualTo("3"));
            Assert.That(game.Track, Is.Not.Null); Assert.That(game.GameCamera, Is.Not.Null);
            Assert.That(scenery.Track, Is.SameAs(game.Track));
            var native = scenery.CurvePlants.SelectMany(plant => plant.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            Material[][] materials = native.Select(renderer => renderer.sharedMaterials).ToArray();
            var filters = scenery.CurvePlants.SelectMany(plant => plant.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            Mesh[] meshes = filters.Select(filter => filter.sharedMesh).ToArray();
            Assert.That(scenery.CurvePlants.All(plant => plant != null), Is.True);
            Vector3[] homes = new Vector3[scenery.CurvePlants.Length];
            for (int i = 0; i < homes.Length; i++) homes[i] = scenery.CurvePlants[i].localPosition;
            int count = SceneObjectCount(game);
            game.StartRun(); game.TogglePause(); game.Track.Reset(42, game.PlayerCubeColor); scenery.SyncDistance();
            Capture(game, "start");
            for (int step = 0; step < 90; step++)
            {
                game.Track.Advance(42, 0, 0, LevelProgression.Difficulty((step + 1) * 42), Vector2.one * 0.45f, game.Player.position.z,
                    false, false, true, false, out _, out _);
                scenery.SyncDistance();
                if (step == 12) Capture(game, "curved");
            }
            Assert.That(Shader.GetGlobalVector("_CubeDashRoadCurve").z, Is.EqualTo(1));
            bool shifted = false;
            for (int i = 0; i < homes.Length; i++) shifted |= Mathf.Abs(scenery.CurvePlants[i].localPosition.x - homes[i].x) > 1;
            Assert.That(shifted, Is.True);
            Assert.That(Mathf.Abs(scenery.Tiles[0].localPosition.z - scenery.Tiles[1].localPosition.z), Is.EqualTo(284).Within(0.01f));
            for (int i = 0; i < native.Length; i++)
            {
                Assert.That(native[i].sharedMaterials, Is.EqualTo(materials[i]), "Native wind/alpha/shadow shaders are preserved.");
            }
            for (int i = 0; i < filters.Length; i++)
                Assert.That(filters[i].sharedMesh, Is.SameAs(meshes[i]), "No lost wind vertex data or rebuilt nature geometry.");
            Assert.That(SceneObjectCount(game), Is.EqualTo(count));
            Vector3 frozen = scenery.CurvePlants[0].position; float distance = game.Track.Travelled; yield return null;
            Assert.That(scenery.CurvePlants[0].position, Is.EqualTo(frozen)); Assert.That(game.Track.Travelled, Is.EqualTo(distance));
            game.RequestRun(); scenery.SyncDistance(); Assert.That(game.CountdownActive, Is.True); Assert.That(game.Track.Travelled, Is.Zero);
            Capture(game, "countdown");
            game.StartRun(); game.TogglePause();
            yield return new WaitForSecondsRealtime(0.35f);
            Capture(game, "pause");
            typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { PowerUpType.Magnet });
            typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { PowerUpType.DoublePoints });
            hud.UpdateStats(); Capture(game, "powerups");
            typeof(CubeDashGame).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            yield return new WaitForSecondsRealtime(0.35f);
            Capture(game, "gameover");
            hud.QuitGame();
            float deadline = Time.realtimeSinceStartup + 30;
            while (SceneManager.GetActiveScene().name != "Main Menu" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main Menu"));
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.StartGame();
            Assert.That(menu.MapPanel.transform.Find("Card/Country Side").GetComponent<Button>().interactable, Is.True);
            Assert.That(menu.Music.isPlaying, Is.True);
        }
        private static string Canonical(string value) => value.Replace("m_Father=Country Side Gameplay:Transform", "m_Father=null").Replace("Country Side Gameplay/", "");
        private static int SceneObjectCount(CubeDashGame game)
        {
            var roots = game.gameObject.scene.GetRootGameObjects();
            Assert.That(roots.All(root => root != null), Is.True, "Every scene root must remain alive.");
            return roots.Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
        }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static void Capture(CubeDashGame game, string suffix)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_CONSISTENT_COUNTRY_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            var camera = game.GameCamera; var canvas = Object.FindAnyObjectByType<Canvas>();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance; bool oldAsync = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1672, 940, 24); var image = new Texture2D(1672, 940, TextureFormat.RGB24, false);
            GameObject menu = canvas.transform.Find("Safe Area/Menu Overlay").gameObject;
            bool oldMenu = menu.activeSelf;
            try
            {
                if (suffix == "start" || suffix == "curved" || suffix == "powerups") menu.SetActive(false);
                // Capture the authored overlay through the camera for batch previews.
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false; Canvas.ForceUpdateCanvases();
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane; ShaderUtil.allowAsyncCompilation = oldAsync;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                menu.SetActive(oldMenu);
            }
        }
    }
}
