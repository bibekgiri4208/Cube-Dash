using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class PlayerBlinkAndFacingTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void BlinkClosesBrieflyReopensSmoothlyAndDoesNotConsumeGameplayRandomness()
        {
            float start = PlayerCubeVisual.BlinkStart;
            Assert.That(PlayerCubeVisual.BlinkOpenness(-1), Is.EqualTo(1));
            Assert.That(PlayerCubeVisual.BlinkOpenness(start - 0.01f), Is.EqualTo(1));
            Assert.That(PlayerCubeVisual.BlinkOpenness(start + 0.03f), Is.EqualTo(0.53f).Within(0.001f));
            Assert.That(PlayerCubeVisual.BlinkOpenness(start + 0.07f), Is.EqualTo(0.06f).Within(0.001f));
            Assert.That(PlayerCubeVisual.BlinkOpenness(start + PlayerCubeVisual.BlinkDuration + 0.01f), Is.EqualTo(1));
            foreach (float time in new[] { start, start + 0.06f, start + 0.085f, start + PlayerCubeVisual.BlinkDuration })
                Assert.That(Mathf.Abs(PlayerCubeVisual.BlinkOpenness(time - 0.00001f) - PlayerCubeVisual.BlinkOpenness(time + 0.00001f)), Is.LessThan(0.001f));
            var random = Random.state;
            try
            {
                Random.InitState(572); float expected = Random.value; Random.InitState(572);
                for (int i = 0; i < 1000; i++)
                {
                    float time = i * 0.013f, openness = PlayerCubeVisual.BlinkOpenness(time);
                    Assert.That(openness, Is.InRange(0.06f, 1f));
                    Assert.That(PlayerCubeVisual.BlinkOpenness(time + PlayerCubeVisual.BlinkInterval), Is.EqualTo(openness).Within(0.0001f));
                }
                Assert.That(Random.value, Is.EqualTo(expected));
            }
            finally { Random.state = random; }
        }

        [Test]
        public void BothEyesBlinkWithoutMovingTheBodyEyeCentersOrColliderAndHideWithTheBody()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/PlayerCube.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var model = root.GetComponentInChildren<PlayerCubeVisual>();
                Vector3[] scales = model.Eyes.Select(eye => eye.transform.localScale).ToArray();
                Vector3[] positions = model.Eyes.Select(eye => eye.transform.localPosition).ToArray();
                Quaternion[] rotations = model.Eyes.Select(eye => eye.transform.localRotation).ToArray();
                Vector3 bodyScale = model.transform.localScale, bodyPosition = model.transform.localPosition;
                Quaternion bodyRotation = model.transform.localRotation;
                Bounds bounds = root.GetComponent<BoxCollider>().bounds;
                model.TickBlink(PlayerCubeVisual.BlinkStart + 0.07f);
                for (int i = 0; i < model.Eyes.Length; i++)
                {
                    Transform eye = model.Eyes[i].transform;
                    Assert.That(eye.localScale.x, Is.EqualTo(scales[i].x));
                    Assert.That(eye.localScale.z, Is.EqualTo(scales[i].z));
                    Assert.That(eye.localScale.y, Is.EqualTo(scales[i].y * 0.06f).Within(0.00001f));
                    Assert.That(eye.localPosition, Is.EqualTo(positions[i]));
                    Assert.That(eye.localRotation, Is.EqualTo(rotations[i]));
                }
                Assert.That(model.transform.localScale, Is.EqualTo(bodyScale));
                Assert.That(model.transform.localPosition, Is.EqualTo(bodyPosition));
                Assert.That(model.transform.localRotation, Is.EqualTo(bodyRotation));
                Assert.That(root.GetComponent<BoxCollider>().bounds, Is.EqualTo(bounds));
                model.Body.enabled = false; model.SyncEyes();
                Assert.That(model.Eyes.All(eye => !eye.enabled), Is.True);
                model.Body.enabled = true; model.SyncEyes();
                Assert.That(model.Eyes.All(eye => eye.enabled), Is.True);
                model.UseGameplayAnimation();
                for (int i = 0; i < model.Eyes.Length; i++) Assert.That(model.Eyes[i].transform.localScale, Is.EqualTo(scales[i]));
                Assert.That(model.BlinkAge, Is.Zero);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SavedLevelTurnsOnlyItsCubeVisualTowardTheRoadAndKeepsTheMenuPrefabPose()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
                Transform visual = game.Player.Find("Cube Visual");
                Assert.That(Vector3.Dot(visual.rotation * Vector3.back, Vector3.forward), Is.GreaterThan(0.999f));
                Assert.That(game.Player.localRotation, Is.EqualTo(Quaternion.identity));
                var model = visual.GetComponent<PlayerCubeVisual>();
                Assert.That(model.Eyes.All(eye => eye.transform.position.z > visual.position.z), Is.True);
                Assert.That(Vector3.Dot(visual.rotation * Vector3.back, game.GameCamera.transform.position - visual.position), Is.LessThan(0));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/PlayerCubeMenuPreview.prefab");
            Assert.That(prefab.transform.Find("Cube Visual").localRotation, Is.EqualTo(Quaternion.identity), "The menu's camera-facing mascot stays unchanged.");
        }

        [UnityTest]
        public IEnumerator MenuAutomaticallyTicksBlinkAndDisablingItRestoresOpenEyes()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Main Menu.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var model = menu.Hero.GetComponent<PlayerCubeVisual>();
            Transform root = model.transform.parent;
            Vector3 rootPosition = root.position, rootScale = root.localScale;
            Quaternion rootRotation = root.rotation;
            float before = model.BlinkAge, deadline = Time.time + 0.1f;
            while (Time.time < deadline) yield return null;
            Assert.That(model.BlinkAge, Is.GreaterThan(before));
            Vector3[] scales = model.Eyes.Select(eye => eye.transform.localScale).ToArray();
            model.TickBlink(PlayerCubeVisual.BlinkStart + 0.07f - model.BlinkAge);
            for (int i = 0; i < scales.Length; i++)
                Assert.That(model.Eyes[i].transform.localScale.y, Is.EqualTo(scales[i].y * 0.06f).Within(0.00001f));
            model.enabled = false;
            for (int i = 0; i < scales.Length; i++) Assert.That(model.Eyes[i].transform.localScale, Is.EqualTo(scales[i]));
            Assert.That(root.position, Is.EqualTo(rootPosition)); Assert.That(root.rotation, Is.EqualTo(rootRotation));
            Assert.That(root.localScale, Is.EqualTo(rootScale)); Assert.That(root.GetComponentsInChildren<Collider>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator LevelKeepsForwardFacingThroughRunsAndSteeringAndBlinkFreezesOnPause()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>();
            var model = game.Player.Find("Cube Visual").GetComponent<PlayerCubeVisual>();
            var collider = game.Player.GetComponent<BoxCollider>();
            Bounds bounds = collider.bounds; Vector3 rootScale = game.Player.localScale;
            Assert.That(model.AutoplayIdle, Is.False);
            Assert.That(Vector3.Dot(model.transform.rotation * Vector3.back, Vector3.forward), Is.GreaterThan(0.999f));
            game.enabled = false;
            model.TickBlink(PlayerCubeVisual.BlinkStart + 0.07f - model.BlinkAge);
            game.StartRun(); game.TogglePause(); game.enabled = true;
            float pausedAge = model.BlinkAge;
            Vector3[] closed = model.Eyes.Select(eye => eye.transform.localScale).ToArray();
            float deadline = Time.time + 0.15f;
            while (Time.time < deadline) yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
            Assert.That(model.BlinkAge, Is.EqualTo(pausedAge));
            for (int i = 0; i < closed.Length; i++) Assert.That(model.Eyes[i].transform.localScale, Is.EqualTo(closed[i]));
            Assert.That(collider.bounds, Is.EqualTo(bounds)); Assert.That(game.Player.localScale, Is.EqualTo(rootScale));
            game.TogglePause(); game.ChangeLane(1);
            deadline = Time.time + 0.2f; float bank = 0;
            while (Time.time < deadline)
            {
                yield return null;
                bank = Mathf.Max(bank, model.transform.up.x);
                Assert.That(Vector3.Dot(model.transform.rotation * Vector3.back, Vector3.forward), Is.GreaterThan(0.9f));
            }
            Assert.That(model.BlinkAge, Is.GreaterThan(pausedAge));
            Assert.That(bank, Is.GreaterThan(0.01f), "Right-lane movement still leans right after the visual-only facing correction.");
            game.StartRun();
            // StartRun teleports the root; refresh physics before reading its world-space bounds.
            Physics.SyncTransforms();
            Assert.That(Vector3.Dot(model.transform.rotation * Vector3.back, Vector3.forward), Is.GreaterThan(0.999f));
            Assert.That(collider.bounds, Is.EqualTo(bounds)); Assert.That(game.Player.localScale, Is.EqualTo(rootScale));
        }
    }
}
