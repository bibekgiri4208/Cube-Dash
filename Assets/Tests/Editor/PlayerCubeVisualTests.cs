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
            Assert.That(red.r, Is.GreaterThan(0.9f));
            Assert.That(red.g, Is.LessThan(0.05f)); Assert.That(red.b, Is.LessThan(0.05f));
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
            game.PressSupercarEnter(1); Assert.That(game.PressSupercarEnter(1.1), Is.True);
            Assert.That(model.Body.enabled, Is.False); Assert.That(model.Eyes.All(eye => !eye.enabled), Is.True);
            game.StartRun();
            Assert.That(model.Body.enabled, Is.True); Assert.That(model.Eyes.All(eye => eye.enabled), Is.True);
            Assert.That(collider.enabled, Is.True); Assert.That(game.Player.localScale, Is.EqualTo(colliderScale));
        }
    }
}
