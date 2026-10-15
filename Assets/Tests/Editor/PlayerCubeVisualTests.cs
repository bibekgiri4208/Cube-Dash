using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class PlayerCubeVisualTests
    {
        private const string PlayerPath = "Assets/Prefab/CubeDash/PlayerCube.prefab";
        private const string PreviewPath = "Assets/Prefab/CubeDash/PlayerCubeMenuPreview.prefab";

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void MenuVariantInheritsTheActualPlayerMeshAndEyesButHasNoGameplayComponents()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(preview), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(preview)), Is.EqualTo(PlayerPath));
            PlayerCubeVisual model = player.transform.Find("Cube Visual").GetComponent<PlayerCubeVisual>();
            PlayerCubeVisual menu = preview.GetComponentInChildren<PlayerCubeVisual>();
            Assert.That(menu.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(model.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(menu.Body.sharedMaterial, Is.SameAs(model.Body.sharedMaterial));
            Color red = menu.Body.sharedMaterial.GetColor("_BaseColor");
            Assert.That(red.r, Is.GreaterThan(Mathf.Max(red.g, red.b) * 2f),
                "The inherited material should remain red-dominant while allowing the user's saved color tuning.");
            Assert.That(menu.Body.sharedMaterial.GetColor("_EmissionColor").maxColorComponent, Is.LessThan(0.15f),
                "The menu's bright lighting must not combine with excessive self-emission to wash out the red.");
            Assert.That(menu.Eyes.Length, Is.EqualTo(2));
            for (int i = 0; i < 2; i++)
            {
                Assert.That(menu.Eyes[i].transform.parent, Is.SameAs(menu.transform));
                Assert.That(menu.Eyes[i].sharedMaterial, Is.SameAs(model.Eyes[i].sharedMaterial));
                Assert.That(menu.Eyes[i].sharedMaterial.GetColor("_BaseColor").maxColorComponent, Is.LessThan(0.02f));
                Assert.That(menu.Eyes[i].sharedMaterial.GetColor("_EmissionColor").maxColorComponent, Is.Zero);
            }
            Assert.That(preview.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(preview.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(preview.GetComponentsInChildren<PlayerInput>(true), Is.Empty);
            Assert.That(preview.GetComponentsInChildren<MonoBehaviour>(true), Has.Length.EqualTo(1));
            Assert.That(preview.GetComponentsInChildren<MonoBehaviour>(true)[0], Is.SameAs(menu));
            Assert.That(preview.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
            Assert.That(preview.transform.childCount, Is.EqualTo(1));
            Assert.That(player.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerFlight>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerTruck>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerSupercar>(), Is.Not.Null);
        }

        [Test]
        public void JellyIsBoundedVolumePreservingAndKeepsTheFeetGrounded()
        {
            float minimum = float.MaxValue, maximum = float.MinValue;
            for (int i = 0; i < 300; i++)
            {
                Vector3 scale = PlayerCubeVisual.JellyScale(i * 0.05f);
                Assert.That(scale.x, Is.InRange(0.85f, 1.15f));
                Assert.That(scale.y, Is.InRange(0.85f, 1.15f));
                Assert.That(scale.z, Is.InRange(0.85f, 1.15f));
                Assert.That(scale.x * scale.y * scale.z, Is.EqualTo(1).Within(0.00001f));
                minimum = Mathf.Min(minimum, scale.y); maximum = Mathf.Max(maximum, scale.y);
                Vector3 origin = new Vector3(0.2f, 0.7f, -0.4f), rest = new Vector3(1.45f, 1.45f, 1.45f);
                Vector3 deformed = Vector3.Scale(rest, scale);
                Vector3 position = PlayerCubeVisual.GroundedPosition(origin, rest, deformed, -0.5f);
                Assert.That(position.y - deformed.y * 0.5f, Is.EqualTo(origin.y - rest.y * 0.5f).Within(0.00001f));
                Assert.That(position.x, Is.EqualTo(origin.x)); Assert.That(position.z, Is.EqualTo(origin.z));
            }
            Assert.That(maximum - minimum, Is.GreaterThan(0.15f), "An observable squash and rebound, not a barely visible scale pulse.");
            Assert.That(PlayerCubeVisual.JellyScale(0), Is.EqualTo(Vector3.one));
        }

        [Test]
        public void MenuBackflipHasAnticipationAFullBackwardTurnAndASoftLanding()
        {
            Assert.That(PlayerCubeVisual.IdleAngularSpeed, Is.InRange(3f, 3.2f), "Only a modest increase from the previous 2.6 speed.");
            float period = 2 * Mathf.PI / PlayerCubeVisual.IdleAngularSpeed;
            Assert.That(Vector3.Distance(PlayerCubeVisual.JellyScale(0.3f), PlayerCubeVisual.JellyScale(0.3f + period)), Is.LessThan(0.00001f));

            float start = PlayerCubeVisual.BackflipStart;
            PlayerCubeVisual.SampleMenuIdle(start - 0.1f, out Vector3 idle, out Quaternion rotation, out float lift);
            Assert.That(idle, Is.EqualTo(PlayerCubeVisual.JellyScale(start - 0.1f)));
            Assert.That(rotation, Is.EqualTo(Quaternion.identity)); Assert.That(lift, Is.Zero);
            PlayerCubeVisual.SampleMenuIdle(start + 0.09f, out Vector3 crouch, out rotation, out lift);
            Assert.That(crouch.y, Is.LessThan(PlayerCubeVisual.JellyScale(start + 0.09f).y));
            Assert.That(rotation, Is.EqualTo(Quaternion.identity)); Assert.That(lift, Is.Zero);
            PlayerCubeVisual.SampleMenuIdle(start + 0.385f, out _, out rotation, out lift);
            Assert.That((rotation * Vector3.back).y, Is.GreaterThan(0.7f), "The face pitches upward: a backflip, not a frontflip.");
            Assert.That(lift, Is.GreaterThan(0.6f));
            PlayerCubeVisual.SampleMenuIdle(start + 0.59f, out _, out rotation, out lift);
            Assert.That(Quaternion.Angle(Quaternion.identity, rotation), Is.EqualTo(180).Within(0.01f));
            Assert.That(lift, Is.EqualTo(0.95f).Within(0.00001f));
            PlayerCubeVisual.SampleMenuIdle(start + 1.125f, out Vector3 landing, out rotation, out lift);
            Assert.That(landing.y, Is.LessThan(PlayerCubeVisual.JellyScale(start + 1.125f).y));
            Assert.That(rotation, Is.EqualTo(Quaternion.identity)); Assert.That(lift, Is.Zero);

            foreach (float time in new[] { start, start + 0.18f, start + 1f, start + PlayerCubeVisual.BackflipDuration })
            {
                PlayerCubeVisual.SampleMenuIdle(time - 0.00001f, out Vector3 before, out Quaternion turnBefore, out float liftBefore);
                PlayerCubeVisual.SampleMenuIdle(time + 0.00001f, out Vector3 after, out Quaternion turnAfter, out float liftAfter);
                Assert.That(Vector3.Distance(before, after), Is.LessThan(0.001f), "No snapping between idle, takeoff and landing.");
                Assert.That(Quaternion.Angle(turnBefore, turnAfter), Is.LessThan(0.1f));
                Assert.That(Mathf.Abs(liftBefore - liftAfter), Is.LessThan(0.001f));
            }
            for (int i = 0; i <= 160; i++)
            {
                float time = start + i * 0.01f;
                PlayerCubeVisual.SampleMenuIdle(time, out Vector3 scale, out rotation, out lift);
                Assert.That(scale.x * scale.y * scale.z, Is.EqualTo(1).Within(0.00001f));
                Assert.That(rotation * Vector3.right, Is.EqualTo(Vector3.right), "No unintended sideways spin.");
                Vector3 position = PlayerCubeVisual.GroundedPosition(Vector3.zero, Vector3.one, scale, -0.5f) + Vector3.up * lift;
                Vector3 rotatedUp = rotation * Vector3.up, rotatedForward = rotation * Vector3.forward;
                float extent = (Mathf.Abs(rotatedUp.y) * scale.y + Mathf.Abs(rotatedForward.y) * scale.z) * 0.5f;
                Assert.That(position.y - extent, Is.GreaterThanOrEqualTo(-0.5001f), "The airborne mesh must not rotate through the floor.");
                PlayerCubeVisual.SampleMenuIdle(time + PlayerCubeVisual.BackflipInterval, out _, out Quaternion repeated, out float repeatedLift);
                Assert.That(Quaternion.Angle(rotation, repeated), Is.LessThan(0.1f));
                Assert.That(repeatedLift, Is.EqualTo(lift).Within(0.00001f));
            }
            float end = start + PlayerCubeVisual.BackflipDuration + 0.1f;
            PlayerCubeVisual.SampleMenuIdle(end, out idle, out rotation, out lift);
            Assert.That(idle, Is.EqualTo(PlayerCubeVisual.JellyScale(end)));
            Assert.That(rotation, Is.EqualTo(Quaternion.identity)); Assert.That(lift, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BackflipRunsOnlyInTheMenuAndGameplayReadyNeverJumpsOrRotates()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Main Menu.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu != null, Is.True, "Main Menu must finish loading before checking its preview.");
            Renderer hero = menu.Hero;
            Assert.That(hero != null, Is.True, "The menu's player renderer must remain valid.");
            PlayerCubeVisual model = hero.gameObject.GetComponent<PlayerCubeVisual>();
            Assert.That(model != null, Is.True);
            Transform root = model.transform.parent;
            Vector3 rootPosition = root.position, rootScale = root.localScale;
            Quaternion rootRotation = root.rotation;
            FieldInfo menuAge = typeof(PlayerCubeVisual).GetField("age", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(menuAge, Is.Not.Null);
            menuAge.SetValue(model, PlayerCubeVisual.BackflipStart + 0.4f);
            float deadline = Time.time + 0.05f;
            while (Time.time < deadline) yield return null;
            Assert.That(Quaternion.Angle(model.transform.localRotation, Quaternion.identity), Is.GreaterThan(30));
            Assert.That(model.transform.localPosition.y, Is.GreaterThan(0.5f));
            Assert.That(root.position, Is.EqualTo(rootPosition)); Assert.That(root.rotation, Is.EqualTo(rootRotation));
            Assert.That(root.localScale, Is.EqualTo(rootScale));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (MeshRenderer eye in model.Eyes)
            {
                Assert.That(eye.transform.parent, Is.EqualTo(model.transform));
                Assert.That(eye.enabled, Is.True);
            }
            model.enabled = false;
            Assert.That(model.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(model.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(model.transform.localScale, Is.EqualTo(Vector3.one), "Disabling a mid-flip preview restores its authored pose.");

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false;
            model = game.Player.Find("Cube Visual").GetComponent<PlayerCubeVisual>();
            Assert.That(model.AutoplayIdle, Is.False, "Gameplay must never run the menu's backflip animator.");
            Vector3 position = game.Player.position, scale = game.Player.localScale;
            Bounds bounds = game.Player.GetComponent<BoxCollider>().bounds;
            Quaternion originalRotation = model.transform.localRotation;
            Vector3 origin = (Vector3)typeof(CubeDashGame).GetField("playerVisualOrigin", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            FieldInfo time = typeof(CubeDashGame).GetField("animationTime", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo animate = typeof(CubeDashGame).GetMethod("UpdatePlayerAnimation", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 200; i++)
            {
                time.SetValue(game, i * 0.1f); animate.Invoke(game, new object[] { 0f });
                Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Ready));
                Assert.That(model.transform.localRotation, Is.EqualTo(originalRotation));
                Assert.That(model.transform.localPosition.y - model.transform.localScale.y * 0.5f, Is.EqualTo(origin.y - 0.5f).Within(0.00001f),
                    "Ready remains grounded even when the menu would backflip.");
                Assert.That(game.Player.position, Is.EqualTo(position)); Assert.That(game.Player.localScale, Is.EqualTo(scale));
                Assert.That(game.Player.GetComponent<BoxCollider>().bounds, Is.EqualTo(bounds));
            }
            game.StartRun();
            Assert.That(model.transform.localRotation, Is.EqualTo(originalRotation));
            Assert.That(model.transform.localPosition, Is.EqualTo(origin));
        }

        [UnityTest]
        public IEnumerator MenuPlayerJelliesButKeyboardAndGamepadCannotMoveOrTransformIt()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Main Menu.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            PlayerCubeVisual model = menu.Hero.GetComponent<PlayerCubeVisual>();
            Transform root = model.transform.parent;
            Vector3 position = root.position, rootScale = root.localScale, initialScale = model.transform.localScale;
            Quaternion rotation = root.rotation;
            Assert.That(model.AutoplayIdle, Is.True);
            Assert.That(Object.FindAnyObjectByType<CubeDashGame>(), Is.Null);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>(); Gamepad pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.RightArrow, Key.I, Key.R));
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.DpadRight));
                InputSystem.Update();
                // These are EditMode tests that enter Play: explicitly wait for game time,
                // rather than relying on WaitForSeconds being scheduled as a runtime coroutine.
                float deadline = Time.time + 0.4f;
                float deformation = 0;
                while (Time.time < deadline)
                {
                    yield return null;
                    deformation = Mathf.Max(deformation, Vector3.Distance(initialScale, model.transform.localScale));
                    Assert.That(root.position, Is.EqualTo(position));
                    Assert.That(root.rotation, Is.EqualTo(rotation));
                    Assert.That(root.localScale, Is.EqualTo(rootScale));
                }
                Assert.That(root.position, Is.EqualTo(position));
                Assert.That(root.rotation, Is.EqualTo(rotation));
                Assert.That(root.localScale, Is.EqualTo(rootScale));
                Assert.That(deformation, Is.GreaterThan(0.01f));
                Assert.That(model.Eyes.All(eye => eye.enabled), Is.True);
                Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true), Has.Length.EqualTo(1));
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main Menu"));
            }
            finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(pad); }
        }

        [UnityTest]
        public IEnumerator GameplayIdleDoesNotResizeTheColliderAndEyesHideWithPowerUpModels()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            PlayerCubeVisual model = game.Player.Find("Cube Visual").GetComponent<PlayerCubeVisual>();
            Assert.That(model, Is.Not.Null, "The original scene reference must survive the prefab update.");
            Assert.That(model.AutoplayIdle, Is.False, "The game is the sole owner of gameplay/pause/pickup animation.");
            Vector3 position = game.Player.position, colliderScale = game.Player.localScale, firstScale = model.transform.localScale;
            BoxCollider collider = game.Player.GetComponent<BoxCollider>(); Bounds bounds = collider.bounds;
            float deadline = Time.time + 0.4f;
            float deformation = 0;
            while (Time.time < deadline)
            {
                yield return null;
                deformation = Mathf.Max(deformation, Vector3.Distance(firstScale, model.transform.localScale));
            }
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Ready));
            Assert.That(deformation, Is.GreaterThan(0.01f));
            Assert.That(game.Player.position, Is.EqualTo(position)); Assert.That(game.Player.localScale, Is.EqualTo(colliderScale));
            Assert.That(collider.bounds, Is.EqualTo(bounds));
            game.StartRun();
            Assert.That(model.transform.localScale, Is.EqualTo(Vector3.one));
            game.TogglePause(); Vector3 paused = model.transform.localScale;
            deadline = Time.time + 0.15f;
            while (Time.time < deadline) yield return null;
            Assert.That(model.transform.localScale, Is.EqualTo(paused));
            game.TogglePause();
            MethodInfo apply = typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (PowerUpType type in new[] { PowerUpType.FighterPlane, PowerUpType.Truck })
            {
                game.StartRun(); apply.Invoke(game, new object[] { type });
                Assert.That(model.Body.enabled, Is.False);
                Assert.That(model.Eyes.All(eye => !eye.enabled), Is.True, "No floating eyes beside the plane or truck.");
            }
            game.StartRun();
            using (var economy = new EconomyTestScope())
            {
                EconomyTestScope.Equip(game);
                Assert.That(model.Body.enabled, Is.False); Assert.That(model.Eyes.All(eye => !eye.enabled), Is.True);
                EconomyTestScope.Equip(game, false);
            }
            game.StartRun();
            Assert.That(model.Body.enabled, Is.True); Assert.That(model.Eyes.All(eye => eye.enabled), Is.True);
            Assert.That(collider.enabled, Is.True); Assert.That(game.Player.localScale, Is.EqualTo(colliderScale));
        }
    }
}
