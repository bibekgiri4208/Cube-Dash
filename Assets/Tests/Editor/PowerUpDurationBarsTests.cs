using System.Collections;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class PowerUpDurationBarsTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void SavedCardsMatchTheMenuThemeAreLeftAlignedAndCannotReceiveInput()
        {
            var scene = EditorSceneManager.OpenPreviewScene(LevelMenuTheme.ScenePath);
            try
            {
                var hud = Find<RunnerHud>(scene); var bars = hud.PowerUpBars;
                Assert.That(bars, Is.Not.Null); Assert.That(bars.Rows.Length, Is.EqualTo(7));
                Assert.That(bars.Stack.anchorMin, Is.EqualTo(new Vector2(0, 1)));
                Assert.That(bars.Stack.gameObject.activeSelf, Is.False);
                Assert.That(bars.Stack.anchoredPosition.x, Is.EqualTo(24));
                Assert.That(bars.Stack.sizeDelta.x, Is.EqualTo(216));
                Assert.That(bars.Rows.Select(row => row.Kind), Is.EquivalentTo(System.Enum.GetValues(typeof(PowerUpDurationRow.Effect))));
                foreach (PowerUpDurationRow row in bars.Rows)
                {
                    var panel = row.GetComponent<MenuPanelGraphic>();
                    Assert.That(panel.color, Is.EqualTo(LevelMenuTheme.Paper)); Assert.That(panel.BorderColor, Is.EqualTo(LevelMenuTheme.Ink));
                    Assert.That(row.gameObject.activeSelf, Is.False); Assert.That(row.Rect.sizeDelta, Is.EqualTo(new Vector2(216, 56)));
                    Assert.That(row.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                    Assert.That(row.RemainingLabel, Is.TypeOf<MenuLabelText>());
                    Assert.That(row.DurationMeter.interactable, Is.False); Assert.That(row.DurationMeter.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                    Assert.That(row.DurationMeter.handleRect, Is.Null); Assert.That(row.DurationMeter.onValueChanged.GetPersistentEventCount(), Is.Zero);
                    Assert.That(row.DurationMeter.fillRect.GetComponent<MenuPanelGraphic>().color, Is.EqualTo(LevelMenuTheme.Accent));
                }
                int count = hud.GetComponentsInChildren<Transform>(true).Length;
                bars.Rows[0].GetComponent<MenuPanelGraphic>().color = Color.blue;
                LevelPowerUpBars.ApplyToScene(scene); LevelMenuTheme.ApplyToScene(scene);
                Assert.That(hud.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(bars.Rows[0].GetComponent<MenuPanelGraphic>().color, Is.EqualTo(Color.blue));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator EachConcurrentEffectHasItsOwnMeterWhichDrainsFromItsActualConfiguredDuration()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(LevelMenuTheme.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var hud = Object.FindAnyObjectByType<RunnerHud>();
            game.enabled = false; game.StartRun();
            var settings = new SerializedObject(game); settings.FindProperty("doublePointsDuration").floatValue = 20; settings.ApplyModifiedPropertiesWithoutUndo();
            Apply(game, PowerUpType.Shield); Apply(game, PowerUpType.DoublePoints); Apply(game, PowerUpType.Magnet); Apply(game, PowerUpType.FighterPlane);
            hud.UpdateStats(); var bars = hud.PowerUpBars;
            Assert.That(bars.Rows.Count(row => row.gameObject.activeSelf), Is.EqualTo(4));
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Shield).RemainingLabel.text, Is.EqualTo("1 HIT"));
            Assert.That(Row(bars, PowerUpDurationRow.Effect.DoublePoints).RemainingLabel.text, Is.EqualTo("20s"));
            Set(game, "doublePointsTimer", 10f); Set(game, "magnetTimer", 2.5f); Set(game, "flightTimer", 7.5f); bars.Refresh();
            Assert.That(Row(bars, PowerUpDurationRow.Effect.DoublePoints).DurationMeter.value, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Magnet).DurationMeter.value, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Flight).DurationMeter.value, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Magnet).RemainingLabel.text, Is.EqualTo("3s"));
            var active = bars.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            for (int i = 0; i < active.Length; i++) Assert.That(active[i].Rect.anchoredPosition.y, Is.EqualTo(-i * 64));
            var fractions = active.Select(row => row.DurationMeter.value).ToArray();
            game.TogglePause();
            for (int i = 0; i < 8; i++) { yield return null; bars.Refresh(); }
            Assert.That(active.Select(row => row.DurationMeter.value).ToArray(), Is.EqualTo(fractions));
            Assert.That(bars.Stack.gameObject.activeSelf, Is.True);
            game.TogglePause(); Apply(game, PowerUpType.Magnet); bars.Refresh();
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Magnet).DurationMeter.value, Is.EqualTo(1));
            Set(game, "magnetTimer", 0f); Set(game, "shields", 0); bars.Refresh();
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Shield).gameObject.activeSelf, Is.False);
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Magnet).gameObject.activeSelf, Is.False);
            Assert.That(Row(bars, PowerUpDurationRow.Effect.DoublePoints).Rect.anchoredPosition.y, Is.Zero);
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Flight).Rect.anchoredPosition.y, Is.EqualTo(-64));
            Assert.That(hud.transform.Find("Safe Area/Power-Up Status").gameObject.activeSelf, Is.False);
            Assert.That(hud.transform.Find("Safe Area/Power-Up Ribbon").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator GameplayTicksDrainMetersAndPauseReallyFreezesTheirTimers()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(LevelMenuTheme.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var bars = Object.FindAnyObjectByType<RunnerHud>().PowerUpBars;
            game.StartRun(); Apply(game, PowerUpType.Magnet); Apply(game, PowerUpType.DoublePoints);
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
            float before = game.MagnetRemaining;
            float deadline = Time.realtimeSinceStartup + 10;
            while (before - game.MagnetRemaining < 0.25f && Time.realtimeSinceStartup < deadline) yield return null;
            bars.Refresh();
            Assert.That(game.MagnetRemaining, Is.LessThan(before));
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Magnet).DurationMeter.value,
                Is.EqualTo(game.MagnetRemaining / game.MagnetDuration).Within(0.001f));
            Assert.That(Row(bars, PowerUpDurationRow.Effect.DoublePoints).DurationMeter.value,
                Is.EqualTo(game.DoublePointsRemaining / game.DoublePointsDuration).Within(0.001f));
            game.TogglePause(); float paused = game.MagnetRemaining, doublePaused = game.DoublePointsRemaining;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(game.MagnetRemaining, Is.EqualTo(paused)); Assert.That(game.DoublePointsRemaining, Is.EqualTo(doublePaused));
            game.TogglePause();
            deadline = Time.realtimeSinceStartup + 10;
            while (paused - game.MagnetRemaining < 0.1f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.MagnetRemaining, Is.LessThan(paused));
        }

        [UnityTest]
        public IEnumerator FlightTruckTimedProtectionAndSupercarSwapCardsAndResetWithoutGrowingUi()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(LevelMenuTheme.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var hud = Object.FindAnyObjectByType<RunnerHud>();
            game.enabled = false; game.StartRun(); var bars = hud.PowerUpBars;
            int objects = hud.GetComponentsInChildren<Transform>(true).Length;
            Apply(game, PowerUpType.FighterPlane); bars.Refresh();
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Flight).gameObject.activeSelf, Is.True);
            Apply(game, PowerUpType.Truck); bars.Refresh();
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Flight).gameObject.activeSelf, Is.False);
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Truck).gameObject.activeSelf, Is.True);
            var settings = new SerializedObject(game); settings.FindProperty("truckShieldDuration").floatValue = 6; settings.ApplyModifiedPropertiesWithoutUndo();
            Invoke(game, "UpdateTruckTimers", 11f); bars.Refresh();
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Truck).gameObject.activeSelf, Is.False);
            Assert.That(Row(bars, PowerUpDurationRow.Effect.TimedShield).RemainingLabel.text, Is.EqualTo("6s"));
            Assert.That(Row(bars, PowerUpDurationRow.Effect.TimedShield).DurationMeter.value, Is.EqualTo(1));
            Invoke(game, "UpdateFlightTimers", 3f); bars.Refresh();
            Assert.That(Row(bars, PowerUpDurationRow.Effect.TimedShield).DurationMeter.value, Is.EqualTo(0.5f).Within(0.001f));
            EconomyTestScope.Equip(game); bars.Refresh();
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Supercar).RemainingLabel.text, Is.EqualTo("ON"));
            Assert.That(Row(bars, PowerUpDurationRow.Effect.Supercar).DurationMeter.value, Is.EqualTo(1));
            Invoke(game, "Crash"); bars.Refresh(); Assert.That(bars.Stack.gameObject.activeSelf, Is.False);
            game.RequestRun(); bars.Refresh(); Assert.That(bars.Stack.gameObject.activeSelf, Is.False);
            for (int i = 0; i < 20; i++) { game.StartRun(); Apply(game, PowerUpType.Magnet); bars.Refresh(); }
            Assert.That(hud.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
            EconomyTestScope.Equip(game, false); game.StartRun(); bars.Refresh(); Assert.That(bars.Stack.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator LeftCardsRenderLikeTheSketchInLandscapeAndPortrait()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(LevelMenuTheme.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var hud = Object.FindAnyObjectByType<RunnerHud>();
            game.enabled = false; game.StartRun();
            Apply(game, PowerUpType.Magnet); Apply(game, PowerUpType.DoublePoints); Apply(game, PowerUpType.FighterPlane);
            Set(game, "magnetTimer", 3f); Set(game, "doublePointsTimer", 5f); Set(game, "flightTimer", 7f); hud.UpdateStats();
            Capture(hud, "three-left-bars"); Capture(hud, "three-left-bars-portrait", 600, 1000);
            foreach (PowerUpDurationRow row in hud.PowerUpBars.Rows.Where(row => row.gameObject.activeSelf))
            {
                foreach (Text label in row.GetComponentsInChildren<Text>())
                    using (var mesh = new VertexHelper())
                    {
                        typeof(MenuLabelText).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(label, new object[] { mesh });
                        Assert.That(mesh.currentVertCount, Is.GreaterThan(0), label.text + " must remain legible.");
                    }
            }
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static PowerUpDurationRow Row(RunnerPowerUpBars bars, PowerUpDurationRow.Effect effect) => bars.Rows.Single(row => row.Kind == effect);
        private static void Apply(CubeDashGame game, PowerUpType type) => Invoke(game, "ApplyPowerUp", type);
        private static void Set(CubeDashGame game, string field, object value) => typeof(CubeDashGame).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, value);
        private static void Invoke(CubeDashGame game, string method, params object[] args) => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);
        private static void Capture(RunnerHud hud, string name, int width = 1672, int height = 940)
        {
            typeof(LevelMenuThemeTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { hud, name, width, height });
        }
    }
}
