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
            Assert.That(car.localScale, Is.EqualTo(Vector3.one * 0.576f));
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

        private static void Toggle(CubeDashGame game, double time) => EconomyTestScope.Equip(game, !GameEconomy.SupercarEquipped);

        private static void Apply(CubeDashGame game, PowerUpType type)
            => typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { type });
        private static void TickTimer(CubeDashGame game, string method, float time)
            => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { time });

        [UnityTest]
        public IEnumerator PurchasedSkinKeepsNormalRulesAndColliderAndHasNoTimerOrSpeedBoost()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            Assert.That(game.SupercarEnabled, Is.False, "The car must be owned and equipped first.");
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
            Toggle(game, 16); game.StartRun(); Assert.That(game.SupercarEnabled, Is.True);
            Assert.That(game.SupercarPresentation.Car.gameObject.activeSelf, Is.True);
            Assert.That(game.Player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }

        [UnityTest]
        public IEnumerator PauseFreezesWheelsAndEquippedSkinSurvivesRestart()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); Clear(game);
            Toggle(game, 1); game.ChangeLane(1); yield return null; yield return null;
            Quaternion wheel = game.SupercarPresentation.Wheels[0].localRotation;
            Assert.That(Quaternion.Angle(wheel, Quaternion.identity), Is.GreaterThan(0.1f));
            game.TogglePause();
            Vector3 position = game.SupercarPresentation.Car.localPosition;
            yield return null; yield return null;
            Assert.That(game.SupercarPresentation.Wheels[0].localRotation, Is.EqualTo(wheel));
            Assert.That(game.SupercarPresentation.Car.localPosition, Is.EqualTo(position));
            game.TogglePause(); Assert.That(game.SupercarEnabled, Is.True);
            game.StartRun(); Clear(game);
            Assert.That(game.SupercarEnabled, Is.True);
            Assert.That(GameEconomy.SupercarEquipped, Is.True);
        }

        [UnityTest]
        public IEnumerator TruckAndPlaneTakePriorityThenRestoreTheEquippedCarAndCrashKeepsTheSkin()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
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
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver)); Assert.That(game.SupercarEnabled, Is.True);
            Assert.That(game.SupercarPresentation.Car.gameObject.activeSelf, Is.True);
            game.StartRun(); Assert.That(game.SupercarEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator EngineFollowsCarSelectionSpeedPausePowerUpPriorityAndCleanup()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); Clear(game);
            RunnerAudio audio = game.AudioPresentation;
            int sources = game.GetComponentsInChildren<AudioSource>(true).Length;
            Assert.That(audio.SupercarActive, Is.False);
            Toggle(game, 1); yield return null;
            Assert.That(audio.SupercarActive, Is.True);
            Assert.That(audio.SupercarSource.volume, Is.GreaterThan(0));
            Assert.That(audio.SupercarSource.clip, Is.Not.SameAs(audio.TruckSource.clip));
            audio.Tick(1, game.State, false, false, 12, 0, true);
            float lowPitch = audio.SupercarSource.pitch;
            audio.Tick(1, game.State, false, false, 30, 18, true);
            Assert.That(audio.SupercarSource.pitch, Is.GreaterThan(lowPitch));
            Assert.That(audio.SupercarSource.pitch, Is.InRange(0.85f, 1.41f));
            game.TogglePause();
            float pitch = audio.SupercarSource.pitch, volume = audio.SupercarSource.volume;
            audio.Tick(1, game.State, false, false, 12, 0, true); yield return null;
            Assert.That(audio.SupercarSource.pitch, Is.EqualTo(pitch));
            Assert.That(audio.SupercarSource.volume, Is.EqualTo(volume));
            game.TogglePause(); yield return null;
            Assert.That(audio.SupercarActive, Is.True);
            Apply(game, PowerUpType.Truck); yield return null;
            Assert.That(audio.TruckActive, Is.True); Assert.That(audio.SupercarActive, Is.False);
            Assert.That(audio.SupercarSource.volume, Is.Zero);
            TickTimer(game, "UpdateTruckTimers", game.TruckRemaining + 0.1f); yield return null;
            Assert.That(audio.SupercarActive, Is.True); Assert.That(audio.TruckActive, Is.False);
            Apply(game, PowerUpType.FighterPlane); yield return null;
            Assert.That(audio.FlightActive, Is.True); Assert.That(audio.SupercarActive, Is.False);
            TickTimer(game, "UpdateFlightTimers", game.FlightRemaining + 0.1f); yield return null;
            Assert.That(audio.SupercarActive, Is.True); Assert.That(audio.FlightActive, Is.False);
            Toggle(game, 2); yield return null;
            Assert.That(audio.SupercarActive, Is.False); Assert.That(audio.SupercarSource.volume, Is.Zero);
            Toggle(game, 3); yield return null; game.StartRun(); Clear(game);
            Assert.That(audio.SupercarActive, Is.False);
            Assert.That(audio.SupercarSource.pitch, Is.EqualTo(1));
            yield return null;
            typeof(CubeDashGame).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(audio.SupercarActive, Is.False); Assert.That(audio.SupercarSource.volume, Is.Zero);
            game.StartRun(); Clear(game); yield return null;
            game.enabled = false;
            Assert.That(audio.SupercarActive, Is.False);
            Assert.That(game.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(sources));
        }

        [UnityTest]
        public IEnumerator EquippedCarEnginePlaysAudiblyOnEveryMapAndRecoversAfterInterruptedPlayback()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            GameEconomy.AddCoins(100); GameEconomy.TryBuySupercar(); GameEconomy.EquipSupercar(true);
            foreach (string map in CubeDash.Editor.PauseSoundAuthoring.Maps)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + map + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false; game.StartRun(); Clear(game);
                var audio = game.AudioPresentation; var source = audio.SupercarSource;
                Assert.That(source, Is.Not.Null, map);
                Assert.That(source.clip, Is.Not.Null, map); Assert.That(source.clip.name, Is.EqualTo("SupercarEngineLoop"));
                Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo("SFX"));
                Assert.That(source.mute, Is.False); Assert.That(source.loop, Is.True); Assert.That(source.spatialBlend, Is.Zero);
                audio.Tick(1, game.State, false, false, 24, 0, game.SupercarDriving);
                yield return null;
                Assert.That(audio.SupercarActive, Is.True, map); Assert.That(source.isPlaying, Is.True, map);
                Assert.That(source.volume, Is.GreaterThan(0.4f), "The car engine must not be buried under music: " + map);
                var samples = new float[4096]; Assert.That(source.clip.GetData(samples, 0), Is.True);
                double energy = 0; foreach (float sample in samples) energy += sample * sample;
                Assert.That(System.Math.Sqrt(energy / samples.Length), Is.GreaterThan(0.08), "The saved engine clip contains audible exhaust, not silence.");
                source.Stop(); Assert.That(source.isPlaying, Is.False);
                audio.Tick(1, game.State, false, false, 24, 0, true);
                Assert.That(source.isPlaying, Is.True, "Interrupted engines recover while the skin remains equipped.");
                audio.Tick(1, CubeDashGame.RunState.Paused, false, false, 24, 0, true);
                Assert.That(source.isPlaying, Is.False);
                audio.Tick(1, CubeDashGame.RunState.Running, false, false, 24, 0, true);
                Assert.That(source.isPlaying, Is.True);
                audio.Tick(1, game.State, false, true, 24, 0, true); Assert.That(source.isPlaying, Is.False);
                audio.Tick(1, game.State, true, false, 24, 0, true); Assert.That(source.isPlaying, Is.False);
                audio.Tick(1, game.State, false, false, 24, 0, false); Assert.That(source.isPlaying, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator EnterAndNumpadEnterNeverChangeSkinsOrSwallowSameFrameSteering()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
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
                Assert.That(game.SupercarEnabled, Is.False, "Enter cannot grant an unowned skin.");
                Assert.That(typeof(CubeDashGame).GetField("targetLane", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game), Is.EqualTo(2));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.NumpadEnter)); InputSystem.Update(); read.Invoke(game, null);
                Assert.That(game.SupercarEnabled, Is.False, "Holding Enter cannot toggle a skin.");
                EconomyTestScope.Equip(game);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); InputSystem.Update(); read.Invoke(game, null);
                Assert.That(game.SupercarEnabled, Is.True, "Enter cannot unequip the purchased skin either.");
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
