using System.Collections;
using System.Reflection;
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
            float initialSpeed = game.Speed;
            Vector3 colliderScale = game.Player.localScale;
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
            RunnerCube target = game.Track.Segments[1].Cubes[0];
            target.Configure(CubeColor.Red, game.Track.ColorMaterial(CubeColor.Red));
            target.transform.position = new Vector3(0, 0.575f, 0.5f);
            target.gameObject.SetActive(true);
            yield return null;
            Assert.That(game.Score, Is.EqualTo(1));
            Assert.That(game.Difficulty, Is.GreaterThan(0));
            Assert.That(game.CollectionPulse, Is.GreaterThan(0));
            Assert.That(game.Player.localScale, Is.EqualTo(colliderScale), "Pickup animation must not resize the gameplay collider.");
            Text feedback = Object.FindAnyObjectByType<RunnerHud>().transform.Find("Safe Area/Pickup Feedback").GetComponent<Text>();
            Assert.That(feedback.gameObject.activeSelf, Is.True);
            Assert.That(feedback.text, Is.EqualTo("+1"));
            CubeWake wake = Object.FindAnyObjectByType<CubeWake>();
            game.ChangeLane(1);
            yield return null;
            yield return null;
            Assert.That(game.Speed, Is.GreaterThan(initialSpeed), "Successful collections should raise the speed target.");
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
            Assert.That(game.Difficulty, Is.Zero);
            Assert.That(game.Speed, Is.EqualTo(initialSpeed));
            Assert.That(feedback.gameObject.activeSelf, Is.False);
            Assert.That(wake.Pieces[0].position.x, Is.Zero);
            Assert.That(game.GetComponent<AudioSource>().isPlaying, Is.False);
            Assert.That(game.GetComponent<AudioSource>().pitch, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator RapidPickupsCombineAndRestorePopupOpacityAndPosition()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            RunnerHud hud = Object.FindAnyObjectByType<RunnerHud>();
            Text feedback = hud.transform.Find("Safe Area/Pickup Feedback").GetComponent<Text>();
            Vector2 origin = feedback.rectTransform.anchoredPosition;
            game.StartRun();
            hud.NotifyCollection(1);
            hud.NotifyCollection(2);
            Assert.That(feedback.text, Is.EqualTo("+3"));
            Assert.That(feedback.color.a, Is.EqualTo(1));
            Assert.That(feedback.rectTransform.anchoredPosition, Is.EqualTo(origin));
            hud.NotifyCollection(0);
            Assert.That(feedback.text, Is.EqualTo("+3"));
            game.StartRun();
            Assert.That(feedback.gameObject.activeSelf, Is.False);
            hud.NotifyCollection(1);
            Assert.That(feedback.text, Is.EqualTo("+1"));
            Assert.That(feedback.color.a, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PickupCompressesThenReboundsAndLightTravelsDownTheExistingTail()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            game.enabled = false; // Step the animation deterministically, independent of rendering speed.
            Transform visual = game.Player.Find("Cube Visual");
            Vector3 scale = visual.localScale;
            Bounds collider = game.Player.GetComponent<BoxCollider>().bounds;
            typeof(CubeDashGame).GetField("pickupAge", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, 0f);
            typeof(CubeDashGame).GetField("absorptionPulse", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, 1f);
            MethodInfo animate = typeof(CubeDashGame).GetMethod("UpdatePlayerAnimation", BindingFlags.Instance | BindingFlags.NonPublic);
            animate.Invoke(game, new object[] { 0.035f });
            Assert.That(visual.localScale.y, Is.LessThan(scale.y));
            MaterialPropertyBlock appearance = new MaterialPropertyBlock();
            visual.GetComponent<Renderer>().GetPropertyBlock(appearance);
            Assert.That(appearance.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(1f));
            CubeWake wake = Object.FindAnyObjectByType<CubeWake>();
            yield return null;
            wake.Pieces[0].GetComponent<Renderer>().GetPropertyBlock(appearance);
            float headFlash = appearance.GetColor("_Tint").r;
            wake.Pieces[6].GetComponent<Renderer>().GetPropertyBlock(appearance);
            Assert.That(headFlash, Is.GreaterThan(appearance.GetColor("_Tint").r));
            animate.Invoke(game, new object[] { 0.175f });
            Assert.That(visual.localScale.y, Is.GreaterThan(scale.y));
            yield return null;
            wake.Pieces[0].GetComponent<Renderer>().GetPropertyBlock(appearance);
            float fadedHead = appearance.GetColor("_Tint").r;
            wake.Pieces[6].GetComponent<Renderer>().GetPropertyBlock(appearance);
            Assert.That(appearance.GetColor("_Tint").r, Is.GreaterThan(fadedHead));
            Assert.That(game.Player.GetComponent<BoxCollider>().bounds, Is.EqualTo(collider));
            game.TogglePause();
            Color frozen = appearance.GetColor("_Tint");
            yield return null;
            wake.Pieces[6].GetComponent<Renderer>().GetPropertyBlock(appearance);
            Assert.That(appearance.GetColor("_Tint"), Is.EqualTo(frozen));
            game.TogglePause();
            animate.Invoke(game, new object[] { 0.15f });
            Assert.That(Vector3.Distance(visual.localScale, scale), Is.LessThan(0.0001f));
            game.StartRun();
            Assert.That(game.CollectionAge, Is.GreaterThan(0.8f));
            Assert.That(visual.localScale, Is.EqualTo(scale));
            Assert.That(wake.Pieces.Length, Is.EqualTo(12));
        }

        [UnityTest]
        public IEnumerator EveryMatchingColorUsesThePitchCycleButFatalContactDoesNot()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            AudioSource audio = game.GetComponent<AudioSource>();
            foreach (CubeColor color in new[] { CubeColor.Red, CubeColor.Blue, CubeColor.Green })
            {
                SerializedObject serialized = new SerializedObject(game);
                serialized.FindProperty("playerCubeColor").enumValueIndex = (int)color;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                game.StartRun();
                foreach (TrackSegment segment in game.Track.Segments)
                    foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                RunnerCube target = game.Track.Segments[1].Cubes[0];
                for (int pickup = 0; pickup < 2; pickup++)
                {
                    target.Configure(color, game.Track.ColorMaterial(color));
                    target.transform.position = new Vector3(0, 0.575f, 0.5f);
                    target.gameObject.SetActive(true);
                    yield return null;
                    Assert.That(game.Score, Is.EqualTo(pickup + 1));
                    Assert.That(audio.pitch, Is.EqualTo(1f + pickup * 0.045f).Within(0.0001f));
                }
                audio.Stop();
                CubeColor wrong = color == CubeColor.Red ? CubeColor.Blue : CubeColor.Red;
                target.Configure(wrong, game.Track.ColorMaterial(wrong));
                target.transform.position = new Vector3(0, 0.575f, 0.5f);
                target.gameObject.SetActive(true);
                yield return null;
                Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver));
                Assert.That(game.Score, Is.EqualTo(2));
                Assert.That(audio.isPlaying, Is.False, "Fatal contact must not play the collection chime.");
                game.StartRun();
                Assert.That(audio.pitch, Is.EqualTo(1f));
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
