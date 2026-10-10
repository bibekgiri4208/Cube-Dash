using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class BiomeAmbienceTests
    {
        [Test]
        public void CrossfadesFollowEverySceneryBoundaryIncludingBeachToCityAndRepeatedCycles()
        {
            for (int boundary = 1; boundary <= 15; boundary++)
            {
                float center = boundary * BiomeRules.Length(8);
                BiomeRules.BlendAtDistance(center, 8, 42, out EnvironmentBiome previous, out EnvironmentBiome next, out float blend);
                Assert.That(previous, Is.EqualTo((EnvironmentBiome)((boundary - 1) % 5)));
                Assert.That(next, Is.EqualTo((EnvironmentBiome)(boundary % 5)));
                Assert.That(blend, Is.EqualTo(0.5f).Within(0.00001f));
                for (float offset = -21; offset <= 21; offset += 0.5f)
                {
                    BiomeRules.BlendAtDistance(center + offset, 8, 42, out previous, out next, out blend);
                    float energy = 0;
                    for (int i = 0; i < BiomeRules.Count; i++)
                    {
                        float gain = BiomeAmbience.Weight((EnvironmentBiome)i, previous, next, blend);
                        energy += gain * gain;
                    }
                    Assert.That(energy, Is.EqualTo(1).Within(0.00001f), "Equal-power crossfades cannot dip at the boundary.");
                }
                BiomeRules.BlendAtDistance(center - 0.001f, 8, 42, out previous, out next, out float before);
                BiomeRules.BlendAtDistance(center + 0.001f, 8, 42, out previous, out next, out float after);
                Assert.That(after - before, Is.InRange(0, 0.0001f));
            }
        }

        [TestCase(2, 10)]
        [TestCase(8, 84)]
        [TestCase(12, 30)]
        public void TransitionSettingsAndInitialSilentCityAreRespected(int segments, float width)
        {
            BiomeRules.BlendAtDistance(-42, segments, width, out EnvironmentBiome previous, out EnvironmentBiome next, out float blend);
            Assert.That(previous, Is.EqualTo(EnvironmentBiome.City)); Assert.That(next, Is.EqualTo(EnvironmentBiome.City));
            Assert.That(BiomeAmbience.Weight(EnvironmentBiome.Jungle, previous, next, blend), Is.Zero);
            float boundary = BiomeRules.Length(segments);
            BiomeRules.BlendAtDistance(boundary - width * 0.5f, segments, width, out previous, out next, out blend);
            Assert.That(BiomeAmbience.Weight(EnvironmentBiome.Jungle, previous, next, blend), Is.Zero);
            BiomeRules.BlendAtDistance(boundary + width * 0.5f, segments, width, out previous, out next, out blend);
            Assert.That(BiomeAmbience.Weight(EnvironmentBiome.Jungle, previous, next, blend), Is.EqualTo(1));
        }

        [Test]
        public void SavedStereoLoopsAreBalancedHaveNaturalSeamsAndUseStreamingWithoutAutoplay()
        {
            foreach (string name in new[] { "Jungle", "Mountain", "Desert", "Beach" })
            {
                string path = "Assets/Audio/Ambience/Loops/" + name + "AmbienceLoop.wav";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.That(clip, Is.Not.Null); Assert.That(clip.channels, Is.EqualTo(2));
                Assert.That(clip.length, Is.InRange(3, 60.01f));
                AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.forceToMono, Is.False);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
                Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis));
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    reader.BaseStream.Position = 22; Assert.That(reader.ReadInt16(), Is.EqualTo(2));
                    reader.BaseStream.Position = 44;
                    int count = (int)((reader.BaseStream.Length - 44) / 2); float[] data = new float[count];
                    double sum = 0, energy = 0, slope = 0; float peak = 0;
                    for (int i = 0; i < count; i++)
                    {
                        data[i] = reader.ReadInt16() / 32768f;
                        sum += data[i]; energy += data[i] * data[i]; peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                        if (i >= 2) slope += Mathf.Abs(data[i] - data[i - 2]);
                    }
                    Assert.That(Math.Sqrt(energy / count), Is.InRange(0.095, 0.105), "All four biomes need matched bed levels for smooth transitions.");
                    Assert.That(Math.Abs(sum / count), Is.LessThan(0.001)); Assert.That(peak, Is.LessThan(0.86f));
                    for (int channel = 0; channel < 2; channel++)
                        Assert.That(Mathf.Abs(data[channel] - data[count - 2 + channel]),
                            Is.LessThan((float)Math.Max(0.025, slope / (count - 2) * 10)), "The wrap follows the original waveform, not an abrupt jump.");
                }
            }
        }

        [Test]
        public void SavedLevelHasFourDedicatedStereoSourcesAndDoesNotChangeTheGameplayAudio()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if ((game = root.GetComponentInChildren<CubeDashGame>(true)) != null) break;
                Assert.That(game, Is.Not.Null); BiomeAmbience ambience = game.GetComponent<BiomeAmbience>();
                Assert.That(ambience, Is.Not.Null);
                Assert.That(ambience.Source(EnvironmentBiome.City), Is.Null);
                for (int i = 1; i < BiomeRules.Count; i++)
                {
                    AudioSource source = ambience.Source((EnvironmentBiome)i);
                    Assert.That(source, Is.Not.Null); Assert.That(source.clip, Is.Not.Null);
                    Assert.That(source.loop, Is.True); Assert.That(source.playOnAwake, Is.False);
                    Assert.That(source.spatialBlend, Is.Zero); Assert.That(source.panStereo, Is.Zero);
                    Assert.That(source.pitch, Is.EqualTo(1)); Assert.That(source.volume, Is.Zero);
                    Assert.That(source.dopplerLevel, Is.Zero);
                    Assert.That(source, Is.Not.SameAs(game.AudioPresentation.FlightSource));
                    Assert.That(source, Is.Not.SameAs(game.AudioPresentation.TruckSource));
                    Assert.That(source, Is.Not.SameAs(game.AudioPresentation.PickupSource));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator AmbienceCrossfadesPausesResumesFadesOutAndResetsWithoutCreatingSources()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun();
            BiomeAmbience ambience = game.AmbiencePresentation; Assert.That(ambience, Is.Not.Null);
            int sources = game.GetComponentsInChildren<AudioSource>(true).Length;
            ambience.Tick(1, game.State, 400);
            Assert.That(ambience.Active(EnvironmentBiome.Jungle), Is.True);
            Assert.That(ambience.Source(EnvironmentBiome.Jungle).volume, Is.GreaterThan(0.2f));
            ambience.Tick(0.1f, game.State, 672);
            float jungle = ambience.Source(EnvironmentBiome.Jungle).volume, mountain = ambience.Source(EnvironmentBiome.Mountains).volume;
            Assert.That(jungle, Is.EqualTo(mountain).Within(0.00001f));
            Assert.That(ambience.Active(EnvironmentBiome.Jungle) && ambience.Active(EnvironmentBiome.Mountains), Is.True);
            game.TogglePause(); Assert.That(ambience.Paused, Is.True);
            ambience.Tick(5, game.State, 800);
            Assert.That(ambience.Source(EnvironmentBiome.Jungle).volume, Is.EqualTo(jungle));
            Assert.That(ambience.Source(EnvironmentBiome.Mountains).volume, Is.EqualTo(mountain));
            game.TogglePause(); Assert.That(ambience.Paused, Is.False);
            ambience.Tick(0, game.State, 672);
            Assert.That(ambience.Source(EnvironmentBiome.Jungle).volume, Is.EqualTo(jungle));
            ambience.Tick(0.2f, CubeDashGame.RunState.GameOver, 672);
            Assert.That(ambience.Source(EnvironmentBiome.Jungle).volume, Is.InRange(0.0001f, jungle - 0.0001f));
            ambience.Tick(2, CubeDashGame.RunState.GameOver, 672);
            Assert.That(ambience.Active(EnvironmentBiome.Jungle), Is.False);
            Assert.That(ambience.Active(EnvironmentBiome.Mountains), Is.False);
            ambience.Tick(1, CubeDashGame.RunState.Running, 1400);
            Assert.That(ambience.Active(EnvironmentBiome.Beach), Is.True);
            ambience.Tick(0, CubeDashGame.RunState.Running, 1720);
            Assert.That(ambience.Active(EnvironmentBiome.Beach), Is.False, "Beach fades into the silent City.");
            ambience.Tick(1, CubeDashGame.RunState.Running, 1400);
            game.TogglePause(); game.StartRun();
            Assert.That(ambience.RunGain, Is.Zero); Assert.That(ambience.Paused, Is.False);
            for (int i = 1; i < BiomeRules.Count; i++)
            {
                Assert.That(ambience.Active((EnvironmentBiome)i), Is.False);
                Assert.That(ambience.Source((EnvironmentBiome)i).volume, Is.Zero);
            }
            Assert.That(game.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(sources));
            ambience.Tick(1, game.State, 400); ambience.enabled = false;
            ambience.Tick(1, game.State, 400);
            Assert.That(ambience.Active(EnvironmentBiome.Jungle), Is.False);
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
