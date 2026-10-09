using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class ArcadePolishTests
    {
        [Test]
        public void BloomIsSelectiveAndAllGameplayColorsHaveEmission()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/CubeDashVisuals.asset");
            Assert.That(profile.TryGet(out Bloom bloom), Is.True);
            Assert.That(bloom.active, Is.True);
            Assert.That(bloom.threshold.value, Is.GreaterThanOrEqualTo(1));
            Assert.That(bloom.intensity.value, Is.InRange(0.1f, 0.6f));
            foreach (string color in new[] { "Red", "Blue", "Green" })
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDash" + color + ".mat");
                Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True);
                Assert.That(material.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(0.1f));
            }
        }

        [Test]
        public void PickupClipIsShortNonSilentAndHasClickFreeEnds()
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ArcadeCollect.wav");
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.length, Is.InRange(0.18f, 0.3f));
            float[] samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True);
            float peak = 0;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            Assert.That(peak, Is.InRange(0.15f, 0.95f));
            Assert.That(Mathf.Abs(samples[0]), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(0.001f));
        }

        [Test]
        public void SceneHasAuthoredAudioAnimatedButtonsAndReadablePanels()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                RunnerHud hud = null;
                Light fill = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<CubeDashGame>() != null) game = root.GetComponent<CubeDashGame>();
                    if (root.GetComponent<RunnerHud>() != null) hud = root.GetComponent<RunnerHud>();
                    if (root.name == "Sky Bounce Fill") fill = root.GetComponent<Light>();
                }
                Assert.That(game.GetComponent<AudioSource>().playOnAwake, Is.False);
                Assert.That(game.GetComponent<AudioSource>().spatialBlend, Is.Zero);
                Assert.That(game.GetComponent<AudioSource>().clip, Is.Not.Null);
                SerializedObject serialized = new SerializedObject(game);
                Assert.That(serialized.FindProperty("collectionAudio").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("collectionSound").objectReferenceValue, Is.Not.Null);
                Assert.That(fill, Is.Not.Null);
                Assert.That(fill.shadows, Is.EqualTo(LightShadows.None));
                foreach (Button button in hud.GetComponentsInChildren<Button>(true))
                    Assert.That(button.GetComponent<ArcadeButton>(), Is.Not.Null);
                Image panel = hud.transform.Find("Safe Area/Score Panel").GetComponent<Image>();
                Assert.That(panel.sprite, Is.Not.Null);
                Assert.That(panel.raycastTarget, Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator PickupPulsesAndPauseFreezesTheTailWhileRestartClearsFeedback()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            Vector3 colliderScale = game.Player.localScale;
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
            RunnerCube target = game.Track.Segments[1].Cubes[0];
            target.Configure(CubeColor.Red, game.Track.ColorMaterial(CubeColor.Red));
            target.transform.position = new Vector3(0, 0.575f, 0.5f);
            target.gameObject.SetActive(true);
            yield return null;
            Assert.That(game.Score, Is.EqualTo(1));
            Assert.That(game.CollectionPulse, Is.GreaterThan(0));
            Assert.That(game.Player.localScale, Is.EqualTo(colliderScale), "Pickup animation must not resize the gameplay collider.");
            Text feedback = Object.FindAnyObjectByType<RunnerHud>().transform.Find("Safe Area/Pickup Feedback").GetComponent<Text>();
            Assert.That(feedback.gameObject.activeSelf, Is.True);
            Assert.That(feedback.text, Is.EqualTo("+1"));
            CubeWake wake = Object.FindAnyObjectByType<CubeWake>();
            game.ChangeLane(1);
            yield return null;
            yield return null;
            Assert.That(wake.Pieces[0].position.x, Is.GreaterThan(0));
            Assert.That(wake.Pieces[0].localScale.x, Is.GreaterThan(wake.Pieces[11].localScale.x));
            game.TogglePause();
            Vector3 frozen = wake.Pieces[0].position;
            float frozenPulse = game.CollectionPulse;
            yield return null;
            yield return null;
            Assert.That(wake.Pieces[0].position, Is.EqualTo(frozen));
            Assert.That(game.CollectionPulse, Is.EqualTo(frozenPulse));
            game.StartRun();
            Assert.That(game.CollectionPulse, Is.Zero);
            Assert.That(feedback.gameObject.activeSelf, Is.False);
            Assert.That(wake.Pieces[0].position.x, Is.Zero);
            Assert.That(game.GetComponent<AudioSource>().isPlaying, Is.False);
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
