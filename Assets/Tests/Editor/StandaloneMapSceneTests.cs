using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class StandaloneMapSceneTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void SavedMapRetainsExactGameplayRoadsCameraAndUiWithOnlyOneBiome(int index)
        {
            Scene source = EditorSceneManager.OpenPreviewScene(StandaloneMapScenes.SourcePath);
            Scene map = EditorSceneManager.OpenPreviewScene(StandaloneMapScenes.ScenePath(index));
            try
            {
                var original = Find<CubeDashGame>(source); var game = Find<CubeDashGame>(map);
                Assert.That(Snapshot(game), Is.EqualTo(Snapshot(original)), "All serialized gameplay tuning and references must match Level.");
                Assert.That(Snapshot(game.GameCamera), Is.EqualTo(Snapshot(original.GameCamera)));
                Assert.That(Snapshot(game.GameCamera.transform), Is.EqualTo(Snapshot(original.GameCamera.transform)));
                Assert.That(HierarchySnapshot(game.Player), Is.EqualTo(HierarchySnapshot(original.Player)), "Player, face, colliders and saved vehicle presentations must match Level.");
                var originalCanvas = Find<Canvas>(source); var canvas = Find<Canvas>(map);
                Assert.That(HierarchySnapshot(canvas.transform), Is.EqualTo(HierarchySnapshot(originalCanvas.transform)), "Saved HUD, countdown, pause, Game Over, durations, labels and callbacks must be unchanged.");
                Assert.That(game.UsesStartCountdown, Is.True); Assert.That(Find<RunnerHud>(map).RibbonTheme, Is.True);
                Assert.That(Find<RunnerHud>(map).PowerUpBars, Is.Not.Null);
                var director = game.Track.Environment;
                Assert.That(director.HasFixedBiome, Is.True); Assert.That(director.FixedBiome, Is.EqualTo((EnvironmentBiome)index));
                Assert.That(original.Track.Environment.HasFixedBiome, Is.False, "Original Level keeps its five-biome cycle.");
                Assert.That(game.Track.Segments.Length, Is.EqualTo(original.Track.Segments.Length));
                var trackData = new SerializedObject(game.Track); var originalData = new SerializedObject(original.Track);
                foreach (string field in new[] { "laneWidth", "powerUpChance", "roadCurveStrength", "patternedEncounters" })
                    Assert.That(trackData.FindProperty(field).propertyType == SerializedPropertyType.Boolean ? trackData.FindProperty(field).boolValue.ToString() : trackData.FindProperty(field).floatValue.ToString("R"),
                        Is.EqualTo(originalData.FindProperty(field).propertyType == SerializedPropertyType.Boolean ? originalData.FindProperty(field).boolValue.ToString() : originalData.FindProperty(field).floatValue.ToString("R")), field);
                for (int s = 0; s < game.Track.Segments.Length; s++)
                {
                    TrackSegment segment = game.Track.Segments[s], old = original.Track.Segments[s];
                    Assert.That(segment.Environment.Biomes.Count(root => root != null), Is.EqualTo(1));
                    Assert.That(segment.Environment.Biomes[index].activeSelf, Is.True);
                    segment.Environment.Configure(s - 1, director.SegmentsPerBiome, (EnvironmentBiome)index);
                    Assert.That(segment.Environment.CurrentBiome, Is.EqualTo((EnvironmentBiome)index));
                    Assert.That(segment.transform.Find("Road") != null || segment.transform.Find("Plane") != null, Is.True);
                    foreach (string name in new[] { "Road", "Plane", "Highway Shoulders", "Highway Guardrails", "Highway Lane Markings" })
                    {
                        Transform road = segment.transform.Find(name), oldRoad = old.transform.Find(name);
                        if (oldRoad == null) Assert.That(road, Is.Null, name);
                        else Assert.That(HierarchySnapshot(road), Is.EqualTo(HierarchySnapshot(oldRoad)), name);
                    }
                    for (int slot = 0; slot < segment.Cubes.Length; slot++)
                        Assert.That(HierarchySnapshot(segment.Cubes[slot].transform), Is.EqualTo(HierarchySnapshot(old.Cubes[slot].transform)), "Cube/coin/collider layout");
                    for (int slot = 0; slot < segment.PowerUps.Length; slot++)
                        Assert.That(HierarchySnapshot(segment.PowerUps[slot].transform), Is.EqualTo(HierarchySnapshot(old.PowerUps[slot].transform)), "Power-up layout");
                    Assert.That(segment.GetComponent<BiomeBoundaryBlend>().Entrance, Is.False);
                    Assert.That(segment.GetComponent<BiomeBoundaryBlend>().Exit, Is.False);
                }
                var ambience = Find<BiomeAmbience>(map);
                for (int biome = 0; biome < BiomeRules.Count; biome++)
                    Assert.That(ambience.Source((EnvironmentBiome)biome) != null, Is.EqualTo(biome == index && index != 0));
                Assert.That(EditorBuildSettings.scenes.Count(entry => entry.enabled && entry.path == StandaloneMapScenes.ScenePath(index)), Is.EqualTo(1));
                Assert.That(EditorBuildSettings.scenes.Single(entry => entry.path == StandaloneMapScenes.ScenePath(index)).guid.ToString(),
                    Is.EqualTo(AssetDatabase.AssetPathToGUID(StandaloneMapScenes.ScenePath(index))));
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(StandaloneMapScenes.SkyPath(index)), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(map); EditorSceneManager.ClosePreviewScene(source); }
        }

        [UnityTest]
        public IEnumerator AllFiveMenuMapsLoadCountdownAndPreserveProgressionThroughRecyclingPauseRetryAndQuit()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(StandaloneMapScenes.SourcePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var source = Object.FindAnyObjectByType<CubeDashGame>(); source.StartRun(); source.TogglePause();
            List<string> expected = Simulate(source.Track, null);
            for (int index = 0; index < BiomeRules.Count; index++)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.StartGame();
                menu.MapPanel.transform.Find("Card/" + StandaloneMapScenes.MapNames[index]).GetComponent<Button>().onClick.Invoke();
                Assert.That(menu.Loading, Is.True); Assert.That(menu.Music.isPlaying, Is.False);
                yield return WaitForScene(StandaloneMapScenes.MapNames[index]);
                var game = Object.FindAnyObjectByType<CubeDashGame>();
                Assert.That(game.CountdownActive, Is.True);
                Assert.That(Object.FindAnyObjectByType<RunnerHud>().CountdownNumber.text, Is.EqualTo("3"));
                game.StartRun(); game.TogglePause();
                Assert.That(Simulate(game.Track, (EnvironmentBiome)index), Is.EqualTo(expected), "Map must not change the seeded hazards, coins, power-ups, collisions or curve progression.");
                float frozen = game.Track.Travelled; yield return null;
                Assert.That(game.Track.Travelled, Is.EqualTo(frozen));
                game.RequestRun(); Assert.That(game.CountdownActive, Is.True); Assert.That(game.Track.Travelled, Is.Zero);
                game.StartRun(); game.TogglePause();
                Assert.That(game.Track.Environment.CurrentBiome, Is.EqualTo((EnvironmentBiome)index));
                Capture(game, StandaloneMapScenes.MapNames[index]);
                Object.FindAnyObjectByType<RunnerHud>().QuitGame(); yield return WaitForScene("Main Menu");
                Assert.That(Object.FindAnyObjectByType<MainMenuController>().Music.isPlaying, Is.True);
            }
        }

        private static List<string> Simulate(EndlessTrack track, EnvironmentBiome? biome)
        {
            track.Reset(7321, CubeColor.Red);
            int count = track.GetComponentsInChildren<Transform>(true).Length;
            var snapshots = new List<string>();
            for (int step = 0; step < 90; step++)
            {
                track.Advance(42, 0, 0, LevelProgression.Difficulty((step + 1) * 42), Vector2.one * 0.45f, 0, false, false, true, false, out int collected, out bool shieldUsed);
                var state = new StringBuilder().Append(collected).Append('/').Append(shieldUsed).Append('/').Append(Shader.GetGlobalVector("_CubeDashRoadCurve"));
                foreach (TrackSegment segment in track.Segments)
                {
                    state.Append(segment.transform.position);
                    foreach (RunnerCube cube in segment.Cubes)
                        state.Append(cube.gameObject.activeSelf).Append(cube.Color).Append(cube.Hazard).Append(cube.CoinVisible).Append(cube.transform.localPosition).Append(cube.Collider.size).Append(cube.Collider.center);
                    foreach (PowerUpPickup power in segment.PowerUps)
                    {
                        state.Append(power.gameObject.activeSelf);
                        if (power.gameObject.activeSelf) state.Append(power.Type).Append(power.transform.localPosition);
                    }
                    if (biome.HasValue)
                    {
                        Assert.That(segment.Environment.CurrentBiome, Is.EqualTo(biome.Value));
                        Assert.That(segment.Environment.Biomes[(int)biome.Value].activeSelf, Is.True);
                        Assert.That(segment.GetComponent<BiomeBoundaryBlend>().Entrance || segment.GetComponent<BiomeBoundaryBlend>().Exit, Is.False);
                    }
                    else Assert.That(segment.Environment.CurrentBiome, Is.EqualTo(BiomeRules.AtDistance(
                        segment.Environment.SectionIndex * RunnerRules.SegmentLength, track.Environment.SegmentsPerBiome)));
                }
                if (biome.HasValue)
                {
                    Assert.That(track.Environment.CurrentBiome, Is.EqualTo(biome.Value));
                    track.Environment.BlendAtDistance(track.Travelled, out var previous, out var next, out float blend);
                    Assert.That(previous, Is.EqualTo(biome.Value)); Assert.That(next, Is.EqualTo(biome.Value)); Assert.That(blend, Is.Zero);
                }
                snapshots.Add(state.ToString());
            }
            Assert.That(track.Travelled, Is.EqualTo(3780));
            Assert.That(track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            return snapshots;
        }
        private static IEnumerator WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
        }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static string Path(Transform transform) => transform.parent == null ? transform.name : Path(transform.parent) + "/" + transform.name;
        private static string Reference(Object value)
        {
            if (value == null) return "null";
            string asset = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(asset)) return asset + ":" + value.name;
            if (value is Component component) return Path(component.transform) + ":" + component.GetType().Name;
            if (value is GameObject go) return Path(go.transform);
            return value.name;
        }
        internal static string Snapshot(Component component)
        {
            var builder = new StringBuilder(); var property = new SerializedObject(component).GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.ObjectReference;
                if (property.propertyPath == "m_CorrespondingSourceObject" || property.propertyPath == "m_PrefabInstance" || property.propertyPath == "m_PrefabAsset" || property.propertyType == SerializedPropertyType.Generic) continue;
                builder.Append(property.propertyPath).Append('=');
                if (property.propertyType == SerializedPropertyType.ObjectReference) builder.Append(Reference(property.objectReferenceValue));
                else builder.Append(property.type).Append(':').Append(PropertyValue(property));
                builder.AppendLine();
            }
            return builder.ToString();
        }
        private static string PropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float: return property.floatValue.ToString("R");
                case SerializedPropertyType.Boolean: return property.boolValue.ToString();
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Color: return property.colorValue.ToString();
                case SerializedPropertyType.Vector2: return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return property.vector3Value.ToString();
                case SerializedPropertyType.Vector4: return property.vector4Value.ToString();
                case SerializedPropertyType.Quaternion: return property.quaternionValue.ToString();
                case SerializedPropertyType.Rect: return property.rectValue.ToString();
                case SerializedPropertyType.Bounds: return property.boundsValue.ToString();
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.LayerMask: return property.intValue.ToString();
                case SerializedPropertyType.AnimationCurve:
                    return string.Join(";", property.animationCurveValue.keys.Select(key => key.time + "," + key.value + "," + key.inTangent + "," + key.outTangent));
                default: return property.type;
            }
        }
        internal static string HierarchySnapshot(Transform root)
        {
            Assert.That(root, Is.Not.Null);
            return string.Join("\n", root.GetComponentsInChildren<Transform>(true).Select(transform => Path(transform) + ":" + transform.gameObject.activeSelf + "\n" + string.Join("\n", transform.GetComponents<Component>().Select(component => component.GetType().Name + ":" + Snapshot(component)))));
        }
        private static void Capture(CubeDashGame game, string name)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_STANDALONE_MAP_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            var camera = game.GameCamera; var canvas = Object.FindAnyObjectByType<Canvas>();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance; bool oldAsync = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            bool paused = game.State == CubeDashGame.RunState.Paused;
            try
            {
                if (paused) game.TogglePause();
                game.Track.Reset(7321, CubeColor.Red);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false; Canvas.ForceUpdateCanvases();
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane; ShaderUtil.allowAsyncCompilation = oldAsync;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                if (paused) game.TogglePause();
            }
        }
    }
}
