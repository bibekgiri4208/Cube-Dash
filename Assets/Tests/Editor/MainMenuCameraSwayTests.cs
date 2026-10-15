using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class MainMenuCameraSwayTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void SwayStartsAtAuthoredPoseAndStaysSmallAndDriftFree()
        {
            var camera = new GameObject("Sway sample camera", typeof(Camera));
            try
            {
                camera.transform.SetPositionAndRotation(new Vector3(3, 4, -11), Quaternion.Euler(12, 8, 2));
                Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
                var sway = camera.AddComponent<MainMenuCameraSway>();
                sway.SamplePose(0, out Vector3 offset, out Quaternion turn);
                Assert.That(offset, Is.EqualTo(Vector3.zero)); Assert.That(Quaternion.Angle(turn, Quaternion.identity), Is.LessThan(0.001f));
                for (int index = 0; index < 1000; index++)
                {
                    sway.SamplePose(index * 0.25f, out offset, out turn);
                    Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(0.0601f));
                    Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(0.0351f)); Assert.That(offset.z, Is.Zero);
                    Assert.That(Quaternion.Angle(turn, Quaternion.identity), Is.LessThan(0.6f));
                }
                sway.SamplePose(18, out Vector3 first, out Quaternion firstTurn);
                sway.SamplePose(18, out offset, out turn);
                Assert.That(offset, Is.EqualTo(first)); Assert.That(turn, Is.EqualTo(firstTurn));
                sway.enabled = false;
                Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.transform.rotation, Is.EqualTo(rotation), "Edit mode must not change the authored pose.");
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void OnlyTheMenuCameraHasSwayNotTheGameplayCamera()
        {
            var menu = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            var level = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                var components = menu.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuCameraSway>(true)).ToArray();
                Assert.That(components.Length, Is.EqualTo(1)); Assert.That(components[0].enabled, Is.True);
                Assert.That(components[0].CompareTag("MainCamera"), Is.True); Assert.That(components[0].GetComponent<Camera>(), Is.Not.Null);
                Assert.That(level.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuCameraSway>(true)), Is.Empty);
            }
            finally { EditorSceneManager.ClosePreviewScene(menu); EditorSceneManager.ClosePreviewScene(level); }
        }

        [UnityTest]
        public IEnumerator CameraSwaysWithUnscaledTimeRestoresItsAnchorAndDoesNotFollowIntoGameplay()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Main Menu.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Camera camera = Camera.main;
            MainMenuCameraSway sway = camera.GetComponent<MainMenuCameraSway>(); Assert.That(sway, Is.Not.Null);
            sway.enabled = false;
            Vector3 position = camera.transform.localPosition; Quaternion rotation = camera.transform.localRotation;
            float fov = camera.fieldOfView, oldTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0; sway.enabled = true;
                float deadline = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.InRange(0.005f, 0.071f));
                Assert.That(SmallRotationAngle(rotation, camera.transform.localRotation), Is.InRange(0.01f, 0.6f));
                Assert.That(camera.fieldOfView, Is.EqualTo(fov));
                sway.enabled = false;
                Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(0.00001f));
                Assert.That(Quaternion.Angle(camera.transform.localRotation, rotation), Is.LessThan(0.001f));
                // Re-enabling must not capture an already-offset pose and start drifting.
                sway.enabled = true;
                Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(0.00001f));
            }
            finally { Time.timeScale = oldTimeScale; if (sway != null) sway.enabled = false; }
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.StartMap(menu.GameplayScene);
            float loadDeadline = Time.realtimeSinceStartup + 30;
            while ((SceneManager.GetActiveScene().name != "Level" || SceneLoader.IsLoading) && Time.realtimeSinceStartup < loadDeadline) yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level"));
            Assert.That(Object.FindAnyObjectByType<MainMenuCameraSway>(), Is.Null);
        }

        private static float SmallRotationAngle(Quaternion from, Quaternion to)
        {
            // Quaternion.Angle rounds very small rotations to zero; the sway is deliberately below that tolerance.
            Quaternion delta = Quaternion.Inverse(from) * to;
            return 2 * Mathf.Atan2(new Vector3(delta.x, delta.y, delta.z).magnitude, Mathf.Abs(delta.w)) * Mathf.Rad2Deg;
        }
    }
}
