using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class SkyAndAudioTests
    {
        [Test]
        public void TruckImpactVariantsAreShortNonSilentPcmWithClickFreeEdges()
        {
            var distinct = new System.Collections.Generic.HashSet<AudioClip>();
            for (int i = 1; i <= 3; i++)
            {
                string path = "Assets/Audio/TruckImpact" + i + ".wav";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.That(clip, Is.Not.Null); Assert.That(distinct.Add(clip), Is.True);
                Assert.That(clip.channels, Is.EqualTo(1)); Assert.That(clip.frequency, Is.EqualTo(44100));
                Assert.That(clip.length, Is.InRange(0.44f, 0.52f));
                var settings = ((AudioImporter)AssetImporter.GetAtPath(path)).defaultSampleSettings;
                Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM));
                Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
                Assert.That(settings.preloadAudioData, Is.True);
                float[] data = new float[clip.samples]; Assert.That(clip.GetData(data, 0), Is.True);
                float peak = 0, energy = 0;
                foreach (float sample in data) { peak = Mathf.Max(peak, Mathf.Abs(sample)); energy += sample * sample; }
                Assert.That(peak, Is.InRange(0.7f, 0.85f));
                Assert.That(Mathf.Sqrt(energy / data.Length), Is.InRange(0.04f, 0.25f));
                Assert.That(Mathf.Abs(data[0]), Is.LessThan(0.001f));
                Assert.That(Mathf.Abs(data[data.Length - 1]), Is.LessThan(0.001f));
            }
        }

        [Test]
        public void SavedTruckImpactVoicesAreIndependentAndNeverAutoplayOrLoop()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                foreach (GameObject root in scene.GetRootGameObjects()) if (root.TryGetComponent(out CubeDashGame found)) game = found;
                RunnerAudio audio = game.AudioPresentation;
                Assert.That(audio.TruckImpactSources.Length, Is.EqualTo(3));
                var sources = new System.Collections.Generic.HashSet<AudioSource>();
                for (int i = 0; i < 3; i++)
                {
                    AudioSource source = audio.TruckImpactSources[i];
                    Assert.That(source, Is.Not.Null); Assert.That(sources.Add(source), Is.True);
                    Assert.That(source, Is.Not.SameAs(audio.TruckSource));
                    Assert.That(source, Is.Not.SameAs(audio.PickupSource));
                    Assert.That(source, Is.Not.SameAs(game.GetComponent<AudioSource>()));
                    Assert.That(source.playOnAwake || source.loop, Is.False);
                    Assert.That(source.spatialBlend, Is.Zero); Assert.That(source.volume, Is.Zero);
                    Assert.That(audio.TruckImpactClip(i).name, Is.EqualTo("TruckImpact" + (i + 1)));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator TruckImpactPoolVariesBoundsOverlapsAndPausesResumesAndClears()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.enabled = false; game.StartRun(); Clear(game);
            RunnerAudio audio = game.AudioPresentation;
            AudioSource[] voices = audio.TruckImpactSources;
            int count = game.GetComponentsInChildren<AudioSource>(true).Length;
            AudioSource coins = game.GetComponent<AudioSource>(); coins.pitch = 1.135f;
            audio.PlayTruckImpacts(0);
            Assert.That(System.Array.Exists(voices, source => source.isPlaying), Is.False);
            Apply(game, PowerUpType.Truck);
            audio.Tick(0.1f, game.State, false, true, 20, 0);
            float enginePitch = audio.TruckSource.pitch;
            audio.PlayTruckImpacts(2);
            yield return null;
            Assert.That(voices[0].isPlaying && voices[1].isPlaying, Is.True);
            Assert.That(voices[2].isPlaying, Is.False);
            Assert.That(voices[0].clip, Is.Not.SameAs(voices[1].clip));
            Assert.That(voices[0].pitch, Is.Not.EqualTo(voices[1].pitch));
            Assert.That(voices[0].volume, Is.LessThan(0.5f), "Simultaneous impacts are gain-controlled.");
            Assert.That(coins.pitch, Is.EqualTo(1.135f)); Assert.That(audio.TruckSource.pitch, Is.EqualTo(enginePitch));
            game.TogglePause(); yield return null;
            int position = voices[0].timeSamples;
            audio.PlayTruckImpacts(1);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(voices[0].timeSamples, Is.EqualTo(position));
            Assert.That(voices[2].clip, Is.Null, "No new collision voices may start while paused.");
            game.TogglePause();
            yield return new WaitForSecondsRealtime(0.04f);
            Assert.That(voices[0].timeSamples, Is.GreaterThan(position));
            for (int i = 0; i < 20; i++) audio.PlayTruckImpacts(100);
            Assert.That(game.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(count));
            foreach (AudioSource voice in voices) Assert.That(voice.volume, Is.LessThanOrEqualTo(0.35f));
            game.StartRun();
            foreach (AudioSource voice in voices) { Assert.That(voice.isPlaying, Is.False); Assert.That(voice.clip, Is.Null); }
            audio.PlayTruckImpacts(1);
            typeof(CubeDashGame).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            foreach (AudioSource voice in voices) Assert.That(voice.isPlaying, Is.False);
            game.StartRun(); audio.PlayTruckImpacts(1); audio.enabled = false;
            foreach (AudioSource voice in voices) Assert.That(voice.isPlaying, Is.False);
        }

        [Test]
        public void SavedPowerUpSoundsAreDistinctNonSilentPcmAndEngineLoopsHaveSmoothSeams()
        {
            string[] names = { "ShieldPickup", "DoublePointsPickup", "FlightPickup", "TruckPickup", "MagnetPickup", "FlightEngineLoop", "TruckEngineLoop", "SupercarEngineLoop" };
            foreach (string name in names)
            {
                string path = "Assets/Audio/" + name + ".wav";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.That(clip, Is.Not.Null); Assert.That(clip.channels, Is.EqualTo(1));
                bool loop = name.EndsWith("Loop");
                Assert.That(clip.length, Is.InRange(loop ? 5.99f : 0.4f, loop ? 6.01f : 0.7f));
                Assert.That(((AudioImporter)AssetImporter.GetAtPath(path)).defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM));
                float[] data = new float[clip.samples]; Assert.That(clip.GetData(data, 0), Is.True);
                float peak = 0, energy = 0;
                foreach (float sample in data) { peak = Mathf.Max(peak, Mathf.Abs(sample)); energy += sample * sample; }
                Assert.That(peak, Is.InRange(0.1f, 0.9f));
                Assert.That(Mathf.Sqrt(energy / data.Length), Is.InRange(0.025f, 0.3f));
                if (loop) Assert.That(Mathf.Abs(data[0] - data[data.Length - 1]), Is.LessThan(0.025f), "No sudden jump at the engine loop seam.");
                else
                {
                    Assert.That(Mathf.Abs(data[0]), Is.LessThan(0.001f));
                    Assert.That(Mathf.Abs(data[data.Length - 1]), Is.LessThan(0.001f));
                }
            }
        }

        [Test]
        public void VehicleLoopsKeepRecordedTextureAndRetainTheirCc0Sources()
        {
            Assert.That(System.IO.File.Exists("Assets/Audio/VehicleSources/CREDITS.md"), Is.True);
            foreach (string vehicle in new[] { "Flight", "Truck" })
            {
                AudioClip recording = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/VehicleSources/" + vehicle + "EngineSource.mp3");
                Assert.That(recording, Is.Not.Null); Assert.That(recording.length, Is.GreaterThan(15));
                AudioClip loop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + vehicle + "EngineLoop.wav");
                float[] data = new float[loop.samples]; Assert.That(loop.GetData(data, 0), Is.True);
                double difference = 0;
                int offset = loop.frequency * 2;
                for (int i = 0; i < loop.frequency; i++) difference += (data[i] - data[i + offset]) * (data[i] - data[i + offset]);
                Assert.That(System.Math.Sqrt(difference / loop.frequency), Is.GreaterThan(0.03),
                    "Recorded variation must replace the repeating two-second synthetic oscillator bed.");
            }
        }

        [Test]
        public void LevelHasLayeredSkyAlignedSunAndIndependentSavedAudioSources()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                foreach (GameObject root in scene.GetRootGameObjects()) if (root.TryGetComponent(out CubeDashGame found)) game = found;
                RunnerAudio audio = game.AudioPresentation;
                Assert.That(audio, Is.Not.Null);
                Assert.That(audio, Is.SameAs(game.GetComponent<RunnerAudio>()));
                AudioSource coinSource = game.GetComponent<AudioSource>();
                foreach (AudioSource source in new[] { audio.PickupSource, audio.FlightSource, audio.TruckSource, audio.SupercarSource })
                {
                    Assert.That(source, Is.Not.Null); Assert.That(source, Is.Not.SameAs(coinSource));
                    Assert.That(source.playOnAwake, Is.False); Assert.That(source.spatialBlend, Is.Zero);
                }
                Assert.That(audio.PickupSource.loop, Is.False);
                Assert.That(audio.FlightSource.loop && audio.TruckSource.loop, Is.True);
                Assert.That(audio.FlightSource.clip.name, Is.EqualTo("FlightEngineLoop"));
                Assert.That(audio.TruckSource.clip.name, Is.EqualTo("TruckEngineLoop"));
                Assert.That(audio.SupercarSource.loop, Is.True);
                Assert.That(audio.SupercarSource.clip.name, Is.EqualTo("SupercarEngineLoop"));
                var clips = new System.Collections.Generic.HashSet<AudioClip>();
                foreach (PowerUpType type in System.Enum.GetValues(typeof(PowerUpType)))
                {
                    Assert.That(audio.PickupClip(type), Is.Not.Null);
                    Assert.That(clips.Add(audio.PickupClip(type)), Is.True);
                    Assert.That(audio.PickupClip(type), Is.Not.SameAs(coinSource.clip));
                }
                Material sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashHorizon.mat");
                Assert.That(sky.shader.name, Is.EqualTo("CubeDash/Horizon Skybox"));
                Assert.That(sky.GetFloat("_CloudCoverage"), Is.InRange(0.3f, 0.65f));
                Assert.That(sky.GetFloat("_CloudOpacity"), Is.InRange(0.6f, 0.85f));
                Assert.That(sky.GetFloat("_SunSize"), Is.InRange(0.4f, 1f));
                var settings = new SerializedObject(game.Track.Environment);
                Light sun = (Light)settings.FindProperty("sunlight").objectReferenceValue;
                Assert.That(Vector3.Angle(sky.GetVector("_SunDirection"), -sun.transform.forward), Is.LessThan(0.01f));
                Assert.That(sky.GetColor("_SunColor").maxColorComponent, Is.GreaterThan(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator VehicleAudioTracksModesSpeedPauseExpiryRefreshAndRestartWithoutNewSources()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game);
            RunnerAudio audio = game.AudioPresentation;
            int sources = game.GetComponentsInChildren<AudioSource>(true).Length;
            Apply(game, PowerUpType.FighterPlane); yield return null; yield return null;
            Assert.That(audio.FlightActive, Is.True); Assert.That(audio.TruckActive, Is.False);
            Assert.That(audio.FlightSource.volume, Is.GreaterThan(0));
            float lowPitch = audio.FlightSource.pitch;
            audio.Tick(0.5f, game.State, true, false, 30, 0);
            Assert.That(audio.FlightSource.pitch, Is.GreaterThan(lowPitch));
            Assert.That(audio.FlightSource.pitch, Is.InRange(0.94f, 1.06f), "Recorded turbines use subtle pitch changes, not an arcade whine.");
            game.TogglePause(); float pitch = audio.FlightSource.pitch, volume = audio.FlightSource.volume;
            audio.Tick(1, game.State, true, false, 12, 0); yield return null;
            Assert.That(audio.FlightSource.pitch, Is.EqualTo(pitch)); Assert.That(audio.FlightSource.volume, Is.EqualTo(volume));
            game.TogglePause(); Apply(game, PowerUpType.Truck); yield return null;
            Assert.That(audio.TruckActive, Is.True); Assert.That(audio.FlightActive, Is.False);
            Assert.That(audio.FlightSource.volume, Is.Zero);
            audio.Tick(0.5f, game.State, false, true, 30, 12);
            Assert.That(audio.TruckSource.pitch, Is.InRange(0.86f, 1.15f));
            Apply(game, PowerUpType.FighterPlane); yield return null;
            Assert.That(game.Flying, Is.False); Assert.That(audio.FlightActive, Is.False, "Blocked flight never starts turbine audio.");
            Apply(game, PowerUpType.Truck);
            Assert.That(audio.TruckSource.pitch, Is.Not.EqualTo(1), "Refresh doesn't restart the engine loop.");
            typeof(CubeDashGame).GetMethod("UpdateTruckTimers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { 10f });
            yield return null; Assert.That(audio.TruckActive, Is.False); Assert.That(audio.TruckSource.volume, Is.Zero);
            Apply(game, PowerUpType.FighterPlane); yield return null;
            typeof(CubeDashGame).GetMethod("UpdateFlightTimers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { 10f });
            yield return null; Assert.That(audio.FlightActive, Is.False);
            Apply(game, PowerUpType.Truck); yield return null; game.StartRun();
            Assert.That(audio.TruckActive || audio.FlightActive, Is.False);
            Assert.That(game.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(sources));
        }

        [UnityTest]
        public IEnumerator PickupAudioNeverChangesCoinPitchAndCrashOrDisableStopsBothEngines()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game);
            AudioSource coins = game.GetComponent<AudioSource>(); coins.pitch = 1.135f;
            foreach (PowerUpType type in System.Enum.GetValues(typeof(PowerUpType))) Apply(game, type);
            Assert.That(coins.pitch, Is.EqualTo(1.135f));
            yield return null;
            typeof(CubeDashGame).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(game.AudioPresentation.FlightActive || game.AudioPresentation.TruckActive, Is.False);
            game.StartRun(); Clear(game); Apply(game, PowerUpType.Truck); yield return null;
            game.enabled = false;
            Assert.That(game.AudioPresentation.FlightActive || game.AudioPresentation.TruckActive, Is.False);
        }

        private static void Apply(CubeDashGame game, PowerUpType type)
            => typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { type });
        private static void Clear(CubeDashGame game)
        {
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
        }
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
