using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class SupercarTests
    {
        [Test]
        public void DoublePressPairsQuickEdgesButNotSlowPressesOrOverlappingTriples()
        {
            var input = new DoublePressInput();
            Assert.That(input.Press(1), Is.False); Assert.That(input.Press(1.2), Is.True);
            Assert.That(input.Press(1.3), Is.False); Assert.That(input.Press(1.5), Is.True);
            Assert.That(input.Press(2), Is.False); Assert.That(input.Press(3), Is.False);
            input.Reset(); Assert.That(input.Press(3.1), Is.False);
            Assert.That(input.Press(2.1), Is.False, "Clock reversal cannot complete a stale pair.");
            Assert.That(input.Press(2.2), Is.True);
        }

        [Test]
        public void SavedCarHasReferenceDetailsAndValidFacetedGeometryWithoutExtraColliders()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Supercar.prefab");
            Assert.That(prefab, Is.Not.Null); Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Mesh body = prefab.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(body.vertexCount, Is.InRange(1500, 20000)); Assert.That(body.subMeshCount, Is.EqualTo(10));
            Assert.That(body.bounds.size.z, Is.InRange(4.7f, 5.1f)); Assert.That(body.bounds.size.x, Is.InRange(2.1f, 2.6f));
            Assert.That(body.bounds.max.y, Is.InRange(1.25f, 1.35f));
            foreach (int surface in new[] { 1, 3, 5, 6, 7, 8 }) Assert.That(body.GetIndexCount(surface), Is.GreaterThan(10), "Paint, glazing, trim, exhaust and lights are modeled separately.");
            Material[] materials = prefab.GetComponent<Renderer>().sharedMaterials;
            Assert.That(materials.Length, Is.EqualTo(10));
            Assert.That(materials[3].shader.name, Is.EqualTo("CubeDash/Supercar Glass"));
            Assert.That(materials[3].FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
            Assert.That(materials[1].GetColor("_BaseColor").b, Is.GreaterThan(0.8f));
            Assert.That(materials[7].IsKeywordEnabled("_EMISSION"), Is.True); Assert.That(materials[8].IsKeywordEnabled("_EMISSION"), Is.True);
            foreach (int index in new[] { 5, 7, 8 })
                Assert.That(materials[index].globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack,
                    Is.EqualTo((MaterialGlobalIlluminationFlags)0), "URP refresh must not turn off the blue rims and LEDs.");
            Assert.That(prefab.transform.childCount, Is.EqualTo(4));
            Mesh wheel = prefab.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
            Assert.That(wheel.bounds.size.y, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(wheel.GetIndexCount(5), Is.GreaterThan(100), "Electric-blue rim rings.");
            foreach (Mesh mesh in new[] { body, wheel })
            {
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Assert.That(normals.Length, Is.EqualTo(vertices.Length)); int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-12f));
                    Assert.That(normals[triangles[i]].sqrMagnitude, Is.InRange(0.9f, 1.1f));
                }
            }
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/PlayerCube.prefab");
            Assert.That(player.GetComponent<PlayerSupercar>(), Is.Not.Null);
            Transform car = player.transform.Find("Supercar Visual"); Assert.That(car.gameObject.activeSelf, Is.False);
            Assert.That(car.localScale.x, Is.EqualTo(0.72f));
            Assert.That(car.localPosition.y, Is.EqualTo(-0.575f));
            Assert.That((wheel.bounds.size.x + 2.1f) * car.localScale.x, Is.LessThan(2.6f));
        }

        private static void Clear(CubeDashGame game)
        {
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
        }

        private static void Toggle(CubeDashGame game, double time)
        {
            Assert.That(game.PressSupercarEnter(time), Is.False);
            Assert.That(game.PressSupercarEnter(time + 0.15), Is.True);
        }

        private static void Apply(CubeDashGame game, PowerUpType type)
            => typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { type });
        private static void TickTimer(CubeDashGame game, string method, float time)
            => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { time });

        [UnityTest]
        public IEnumerator ManualToggleKeepsNormalRulesAndColliderAndHasNoTimerOrSpeedBoost()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            Assert.That(game.PressSupercarEnter(0), Is.False, "Ready/menu presses do not arm the toggle.");
            game.StartRun(); Clear(game); game.enabled = false;
            BoxCollider collider = game.Player.GetComponent<BoxCollider>(); Vector3 size = collider.size, center = collider.center;
            float speed = game.Speed; int count = game.Player.GetComponentsInChildren<Transform>(true).Length;
            Toggle(game, 1); Assert.That(game.SupercarEnabled && game.SupercarDriving, Is.True);
            Assert.That(game.SupercarPresentation.Car.gameObject.activeSelf, Is.True);
            Assert.That(game.Player.Find("Cube Visual").GetComponent<Renderer>().enabled, Is.False);
            game.SupercarPresentation.Tick(12, speed, 8);
            Assert.That(game.SupercarEnabled, Is.True); Assert.That(game.Speed, Is.EqualTo(speed));
            Assert.That(game.Trucking || game.Flying, Is.False);
            Assert.That(collider.enabled, Is.True); Assert.That(collider.size, Is.EqualTo(size)); Assert.That(collider.center, Is.EqualTo(center));
            RunnerCube match = game.Track.Segments[1].Cubes[0]; match.Configure(CubeColor.Red, game.Track.ColorMaterial(CubeColor.Red));
            match.transform.position = new Vector3(0, 0.575f, 2); match.gameObject.SetActive(true);
            Assert.That(game.Track.Advance(3, 0, 0, 0, Vector2.one * 0.575f, 0, out int collected), Is.False);
            Assert.That(collected, Is.EqualTo(1)); Clear(game);
            RunnerCube wrong = game.Track.Segments[1].Cubes[1]; wrong.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
            wrong.transform.position = new Vector3(0, 0.575f, 2); wrong.gameObject.SetActive(true);
            Assert.That(game.Track.Advance(3, 0, 0, 0, Vector2.one * 0.575f, 0, out collected), Is.True, "Supercar does not provide Truck protection.");
            Toggle(game, 15); Assert.That(game.SupercarEnabled, Is.False);
            Assert.That(game.Player.Find("Cube Visual").GetComponent<Renderer>().enabled, Is.True);
            Toggle(game, 16); game.StartRun(); Assert.That(game.SupercarEnabled, Is.False);
            Assert.That(game.SupercarPresentation.Car.gameObject.activeSelf, Is.False);
            Assert.That(game.Player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }

        [UnityTest]
        public IEnumerator PauseFreezesWheelsAndPressPairsCannotCrossMenuOrRestartBoundaries()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); Clear(game);
            Toggle(game, 1); game.ChangeLane(1); yield return null; yield return null;
            Quaternion wheel = game.SupercarPresentation.Wheels[0].localRotation;
            Assert.That(Quaternion.Angle(wheel, Quaternion.identity), Is.GreaterThan(0.1f));
            game.PressSupercarEnter(3); game.TogglePause();
            Vector3 position = game.SupercarPresentation.Car.localPosition;
            Assert.That(game.PressSupercarEnter(3.1), Is.False);
            yield return null; yield return null;
            Assert.That(game.SupercarPresentation.Wheels[0].localRotation, Is.EqualTo(wheel));
            Assert.That(game.SupercarPresentation.Car.localPosition, Is.EqualTo(position));
            game.TogglePause(); Assert.That(game.PressSupercarEnter(3.2), Is.False);
            Assert.That(game.SupercarEnabled, Is.True); Assert.That(game.PressSupercarEnter(3.3), Is.True);
            Assert.That(game.SupercarEnabled, Is.False);
            game.PressSupercarEnter(4); game.StartRun(); Clear(game);
            Assert.That(game.PressSupercarEnter(4.1), Is.False); Assert.That(game.SupercarEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator TruckAndPlaneTakePriorityThenRestoreTheSelectedCarAndCrashClearsIt()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); Clear(game); game.enabled = false;
            Toggle(game, 1); Apply(game, PowerUpType.Magnet); Apply(game, PowerUpType.DoublePoints);
            Assert.That(game.SupercarDriving && game.MagnetActive && game.DoublePointsActive, Is.True);
            Apply(game, PowerUpType.Truck); Assert.That(game.SupercarEnabled, Is.True); Assert.That(game.SupercarDriving, Is.False);
            Assert.That(game.SupercarPresentation.Car.gameObject.activeSelf, Is.False);
            Assert.That(game.TruckPresentation.Truck.gameObject.activeSelf, Is.True);
            Apply(game, PowerUpType.FighterPlane); Assert.That(game.Flying, Is.False, "Truck still blocks Plane.");
            TickTimer(game, "UpdateTruckTimers", game.TruckRemaining + 0.1f);
            Assert.That(game.SupercarPresentation.Car.gameObject.activeSelf, Is.True);
            Apply(game, PowerUpType.FighterPlane); Assert.That(game.SupercarDriving, Is.False);
            Assert.That(game.Player.GetComponent<BoxCollider>().enabled, Is.False);
            TickTimer(game, "UpdateFlightTimers", game.FlightRemaining + 0.1f);
            Assert.That(game.SupercarPresentation.Car.gameObject.activeSelf, Is.True);
            Assert.That(game.Player.GetComponent<BoxCollider>().enabled, Is.True);
            typeof(CubeDashGame).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver)); Assert.That(game.SupercarEnabled, Is.False);
            Assert.That(game.SupercarPresentation.Car.gameObject.activeSelf, Is.False);
            Assert.That(game.PressSupercarEnter(3), Is.False); game.StartRun(); Assert.That(game.PressSupercarEnter(3.1), Is.False);
        }

        [UnityTest]
        public IEnumerator EnterAndNumpadEnterUsePressEdgesAndDoNotSwallowSameFrameSteering()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); Clear(game); game.enabled = false;
            InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("SupercarTestKeyboard"); keyboard.MakeCurrent();
            MethodInfo read = typeof(CubeDashGame).GetMethod("ReadInput", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                // Explicit input updates allow keyboard routing to be tested without a focused batch-mode Game view.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); InputSystem.Update();
                Assert.That(keyboard.enterKey.wasPressedThisFrame, Is.True); read.Invoke(game, null);
                Assert.That(game.SupercarEnabled, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.NumpadEnter, Key.D)); InputSystem.Update();
                Assert.That(keyboard.numpadEnterKey.wasPressedThisFrame, Is.True); read.Invoke(game, null);
                Assert.That(game.SupercarEnabled, Is.True);
                Assert.That(typeof(CubeDashGame).GetField("targetLane", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game), Is.EqualTo(2));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.NumpadEnter)); InputSystem.Update(); read.Invoke(game, null);
                Assert.That(game.SupercarEnabled, Is.True, "Holding Enter cannot trigger repeated toggles.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
