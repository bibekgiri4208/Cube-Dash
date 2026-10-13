using System.Collections;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class CountrySideGameplayTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void SavedCountrySideUsesTheFullLevelGameplayRigWithNoForeignSceneReferences()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var objects = scene.GetRootGameObjects();
                var game = objects.SelectMany(r => r.GetComponentsInChildren<CubeDashGame>(true)).Single();
                Assert.That(game.Player.gameObject.scene, Is.EqualTo(scene));
                Assert.That(game.GameCamera.gameObject.scene, Is.EqualTo(scene));
                Assert.That(game.Track.gameObject.scene, Is.EqualTo(scene));
                Assert.That(game.Track.Segments.Length, Is.EqualTo(8));
                Assert.That(game.FlightPresentation, Is.Not.Null);
                Assert.That(game.TruckPresentation, Is.Not.Null);
                Assert.That(game.SupercarPresentation, Is.Not.Null);
                Assert.That(game.AudioPresentation, Is.Not.Null);
                Assert.That(game.Track.Debris, Is.Not.Null);
                Assert.That(game.Track.Environment, Is.Not.Null);
                Assert.That(game.Track.Environment.HasFixedBiome, Is.True);
                Assert.That(game.AmbiencePresentation, Is.Not.Null);
                Assert.That(new SerializedObject(game.Track).FindProperty("roadCurveStrength").floatValue, Is.EqualTo(1));
                Assert.That(game.UsesStartCountdown, Is.True);
                Assert.That(objects.SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Count(), Is.EqualTo(1));
                var module = objects.SelectMany(r => r.GetComponentsInChildren<InputSystemUIInputModule>(true)).Single();
                Assert.That(module.actionsAsset, Is.Not.Null);
                Assert.That(module.point, Is.Not.Null);
                Assert.That(module.leftClick, Is.Not.Null);
                Assert.That(objects.SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).Count(), Is.EqualTo(1));
                foreach (TrackSegment segment in game.Track.Segments)
                {
                    Assert.That(segment.Cubes.Length, Is.EqualTo(9));
                    Assert.That(segment.Cubes.All(c => c != null && c.Collider != null), Is.True);
                    Assert.That(segment.PowerUps.Length, Is.EqualTo(3));
                    Assert.That(segment.PowerUps.All(p => p != null && p.Collider != null), Is.True);
                    Assert.That(segment.Environment, Is.Null);
                    Assert.That(segment.GetComponent<SceneryRise>(), Is.Null);
                }
                foreach (Component component in objects.SelectMany(r => r.GetComponentsInChildren<Component>(true)))
                {
                    Assert.That(component, Is.Not.Null, "No missing gameplay or scenery scripts.");
                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var reference = property.objectReferenceValue;
                        if (reference != null && EditorUtility.IsPersistent(reference)) continue;
                        if (reference is Component c) Assert.That(c.gameObject.scene, Is.EqualTo(scene), component.name + "." + property.propertyPath);
                        if (reference is GameObject go) Assert.That(go.scene, Is.EqualTo(scene), component.name + "." + property.propertyPath);
                    }
                }
                var hud = objects.SelectMany(r => r.GetComponentsInChildren<RunnerHud>(true)).Single();
                Assert.That(hud.GameOverOverlay, Is.Not.Null);
                foreach (Button button in hud.GetComponentsInChildren<Button>(true))
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        Object target = button.onClick.GetPersistentTarget(i);
                        Assert.That(target == game || target == hud, Is.True, button.name + " retains its Level callback.");
                        Assert.That(button.onClick.GetPersistentMethodName(i), Is.Not.Empty);
                    }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void RouteTilesAreSavedAndDecorativeCollidersDoNotObstructTheRunner()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CountrySideRunnerScenery>(true)).Single();
                Assert.That(scenery.Track, Is.Not.Null);
                Assert.That(scenery.Tiles.Length, Is.EqualTo(2));
                Assert.That(scenery.TileLength, Is.EqualTo(284));
                foreach (Transform tile in scenery.Tiles)
                {
                    Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tile.gameObject), Does.EndWith("Playable Countryside Route.prefab"));
                    Assert.That(tile.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(tile.Find("Sakura avenue").childCount, Is.EqualTo(10));
                    Assert.That(tile.GetComponentsInChildren<Renderer>(true).All(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.BatchingStatic) == 0), Is.True);
                }
                Assert.That(EditorBuildSettings.scenes.Any(s => s.path == CountrySideSceneBuilder.ScenePath && s.enabled), Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TileLoopRemainsContinuousAfterManyKilometresAndRestartsAtTheAuthoredPositions()
        {
            foreach (float distance in new[] { 0, 50, 280, 281, 284, 568, 1500, 12500, 100000 })
            {
                float a = CountrySideRunnerScenery.TilePosition(distance, 0, 2, 284, 248, -32);
                float b = CountrySideRunnerScenery.TilePosition(distance, 1, 2, 284, 248, -32);
                Assert.That(Mathf.Abs(a - b), Is.EqualTo(284).Within(0.01f));
                Assert.That(Mathf.Min(a, b) + 248, Is.GreaterThanOrEqualTo(-32));
                Assert.That(Mathf.Max(a, b) + 248, Is.GreaterThanOrEqualTo(252));
            }
            Assert.That(CountrySideRunnerScenery.TilePosition(0, 0, 2, 284, 248, -32), Is.Zero);
            Assert.That(CountrySideRunnerScenery.TilePosition(0, 1, 2, 284, 248, -32), Is.EqualTo(284));
        }

        [Test]
        public void PlayableUpgradeIsIdempotentAndRefusesOtherScenes()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                int count = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Count();
                CountrySidePlayableBuilder.ApplyToScene(scene);
                Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Count(), Is.EqualTo(count));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var other = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try { Assert.Throws<System.InvalidOperationException>(() => CountrySidePlayableBuilder.ApplyToScene(other)); }
            finally { EditorSceneManager.ClosePreviewScene(other); }
        }

        [UnityTest]
        public IEnumerator CountrySideStartsSteersScrollsPausesResumesAndRestarts()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CountrySideSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>();
            var scenery = Object.FindAnyObjectByType<CountrySideRunnerScenery>();
            Assert.That(game.enabled, Is.True);
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Ready));
            var hud = Object.FindAnyObjectByType<RunnerHud>();
            Assert.That(game.CountdownActive, Is.True);
            typeof(CubeDashGame).GetMethod("TickStartCountdown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { 3.1f });
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            game.ToggleInvincibility(); game.ChangeLane(1);
            float deadline = Time.time + 0.3f;
            while (Time.time < deadline) yield return null;
            Assert.That(game.Distance, Is.GreaterThan(1));
            Assert.That(game.Player.position.x, Is.GreaterThan(1));
            Assert.That(scenery.Tiles[0].localPosition.z, Is.LessThan(-1));
            game.TogglePause();
            float pausedDistance = game.Distance; Vector3 pausedTile = scenery.Tiles[0].localPosition;
            deadline = Time.time + 0.15f;
            while (Time.time < deadline) yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
            Assert.That(game.Distance, Is.EqualTo(pausedDistance));
            Assert.That(scenery.Tiles[0].localPosition, Is.EqualTo(pausedTile));
            game.TogglePause();
            deadline = Time.time + 0.1f;
            while (Time.time < deadline) yield return null;
            Assert.That(game.Distance, Is.GreaterThan(pausedDistance));
            game.enabled = false;
            int objectCount = scenery.Tiles.Sum(tile => tile.GetComponentsInChildren<Transform>(true).Length);
            game.Track.Advance(1500, game.Player.position.x, game.Player.position.x, 1,
                new Vector2(0.575f, 0.575f), game.Player.position.z, false, true, true, out _, out _);
            scenery.SyncDistance();
            Assert.That(Mathf.Abs(scenery.Tiles[0].localPosition.z - scenery.Tiles[1].localPosition.z), Is.EqualTo(284).Within(0.01f));
            Assert.That(scenery.Tiles.Sum(tile => tile.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(objectCount));
            Assert.That(game.Track.Segments.Max(segment => segment.transform.position.z), Is.GreaterThan(200));
            game.StartRun(); scenery.SyncDistance();
            Physics.SyncTransforms();
            Assert.That(game.Score, Is.Zero); Assert.That(game.Distance, Is.Zero);
            Assert.That(game.Player.position.x, Is.Zero);
            Assert.That(scenery.Tiles[0].localPosition.z, Is.Zero);
            Assert.That(scenery.Tiles[1].localPosition.z, Is.EqualTo(284));
            Assert.That(Object.FindAnyObjectByType<RunnerHud>().RibbonTheme, Is.True);
        }

        [UnityTest]
        public IEnumerator CountrySideAwardsCoinsAppliesEveryPowerUpCrashesAndRetries()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CountrySideSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>();
            game.enabled = false; game.StartRun();
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
            RunnerCube coin = game.Track.Segments[1].Cubes[1];
            coin.Configure(game.PlayerCubeColor, game.Track.ColorMaterial(game.PlayerCubeColor), true);
            coin.transform.position = game.Player.position; coin.gameObject.SetActive(true);
            typeof(CubeDashGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(game.Score, Is.EqualTo(1)); Assert.That(coin.gameObject.activeSelf, Is.False);
            var apply = typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic);
            apply.Invoke(game, new object[] { PowerUpType.Shield }); Assert.That(game.Shields, Is.EqualTo(1));
            apply.Invoke(game, new object[] { PowerUpType.DoublePoints }); Assert.That(game.DoublePointsActive, Is.True);
            apply.Invoke(game, new object[] { PowerUpType.Magnet }); Assert.That(game.MagnetActive, Is.True);
            apply.Invoke(game, new object[] { PowerUpType.FighterPlane }); Assert.That(game.Flying, Is.True);
            apply.Invoke(game, new object[] { PowerUpType.Truck }); Assert.That(game.Trucking, Is.True); Assert.That(game.Flying, Is.False);
            game.StartRun();
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
            var hazard = game.Track.Segments[1].Cubes[1];
            hazard.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue), false);
            hazard.transform.position = game.Player.position; hazard.gameObject.SetActive(true);
            typeof(CubeDashGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver));
            Object.FindAnyObjectByType<RunnerHud>().GameOverOverlay.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Retry").onClick.Invoke();
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Ready));
            Assert.That(game.CountdownActive, Is.True);
            Assert.That(game.Distance, Is.Zero); Assert.That(game.Score, Is.Zero);
            Assert.That(game.Flying || game.Trucking || game.MagnetActive, Is.False);
        }
    }
}
