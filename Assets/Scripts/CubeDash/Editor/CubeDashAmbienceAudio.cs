using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeDash.Editor
{
    /// <summary>Edits provided recordings offline; originals are never rewritten or reimported.</summary>
    public static class CubeDashAmbienceAudio
    {
        private const string Folder = "Assets/Audio/Ambience/";
        private static readonly string[] Files = { "jungle ambience.mp3", "snow mountain ambience.mp3", "desert ambience.mp3", "beach ambience.mp3" };
        private static readonly string[] Names = { "Jungle", "Mountain", "Desert", "Beach" };

        [MenuItem("Tools/Cube Dash/Apply Biome Ambience Audio")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (!AssetDatabase.IsValidFolder(Folder + "Loops")) AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Loops");
            AudioClip[] loops = new AudioClip[Files.Length];
            for (int i = 0; i < loops.Length; i++) loops[i] = BakeLoop(Folder + Files[i], Folder + "Loops/" + Names[i] + "AmbienceLoop.wav");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = UnityEngine.Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null || game.Track.Environment == null) throw new InvalidOperationException("The authored level and Environment Director are required.");
            BiomeAmbience ambience = game.GetComponent<BiomeAmbience>();
            if (ambience == null) ambience = game.gameObject.AddComponent<BiomeAmbience>();
            Transform root = game.transform.Find("Biome Ambience Audio");
            if (root == null) { root = new GameObject("Biome Ambience Audio").transform; root.SetParent(game.transform, false); }
            var settings = new SerializedObject(ambience);
            settings.FindProperty("environment").objectReferenceValue = game.Track.Environment;
            var sources = settings.FindProperty("sources"); sources.arraySize = BiomeRules.Count;
            sources.GetArrayElementAtIndex(0).objectReferenceValue = null;
            for (int i = 0; i < loops.Length; i++)
            {
                Transform child = root.Find(Names[i]);
                if (child == null) { child = new GameObject(Names[i], typeof(AudioSource)).transform; child.SetParent(root, false); }
                AudioSource source = child.GetComponent<AudioSource>();
                if (source == null) source = child.gameObject.AddComponent<AudioSource>();
                source.clip = loops[i]; source.playOnAwake = false; source.loop = true;
                // A full-width stereo bed surrounds the listener without camera-distance pumping.
                source.spatialBlend = 0; source.panStereo = 0; source.dopplerLevel = 0;
                source.pitch = 1; source.volume = 0; source.priority = 192;
                sources.GetArrayElementAtIndex(i + 1).objectReferenceValue = source;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            var manager = new SerializedObject(game);
            manager.FindProperty("ambiencePresentation").objectReferenceValue = ambience;
            manager.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Provided Jungle, Mountain, Desert and Beach recordings saved as balanced stereo ambience loops. Equal-power scenery crossfades, pause/resume and fade-out enabled. Source MP3s, other sounds and gameplay preserved.");
        }

        private static AudioClip BakeLoop(string sourcePath, string outputPath)
        {
            AudioClip recording = AssetDatabase.LoadAssetAtPath<AudioClip>(sourcePath);
            if (recording == null) throw new InvalidOperationException("Ambience recording not found: " + sourcePath);
            if (recording.loadType != AudioClipLoadType.DecompressOnLoad)
                throw new InvalidOperationException("To bake ambience, source recordings must use Decompress On Load: " + sourcePath);
            recording.LoadAudioData();
            float[] source = new float[recording.samples * recording.channels];
            if (!recording.GetData(source, 0)) throw new InvalidOperationException("Cannot decode ambience: " + sourcePath);
            int rate = recording.frequency, channels = recording.channels;
            if (channels > 2 || recording.length < 5) throw new InvalidOperationException("Expected a mono/stereo ambience recording of at least five seconds: " + sourcePath);

            // Skip digital silence at the ends, but retain quiet natural details within the recording.
            int block = Mathf.Max(1, rate / 10), start = 0, end = recording.samples;
            while (start + block < end && BlockRms(source, channels, start, block) < 0.001f) start += block;
            while (end - block > start && BlockRms(source, channels, end - block, block) < 0.001f) end -= block;
            int overlap = Mathf.Min(rate * 2, (end - start) / 4);
            int frames = Mathf.Min(rate * 60, end - start - overlap);
            if (frames < rate * 3) throw new InvalidOperationException("Not enough non-silent ambience: " + sourcePath);
            float[] output = new float[frames * 2];
            double[] mean = new double[2];
            for (int frame = 0; frame < frames; frame++)
                for (int channel = 0; channel < 2; channel++)
                {
                    int c = Mathf.Min(channel, channels - 1);
                    float sample = source[(start + frame) * channels + c];
                    if (frame < overlap)
                    {
                        float angle = frame / (float)overlap * Mathf.PI * 0.5f;
                        sample = source[(start + frames + frame) * channels + c] * Mathf.Cos(angle) + sample * Mathf.Sin(angle);
                    }
                    output[frame * 2 + channel] = sample; mean[channel] += sample;
                }
            double energy = 0;
            for (int i = 0; i < output.Length; i++)
            {
                output[i] -= (float)(mean[i % 2] / frames);
                energy += output[i] * output[i];
            }
            float rms = (float)Math.Sqrt(energy / output.Length);
            if (rms < 0.001f) throw new InvalidOperationException("Ambience recording is silent: " + sourcePath);
            // A few loud bird calls/gusts must not make a whole biome quieter than the others.
            // Stereo-linked soft peak control preserves direction while matching the background level.
            float gain = 0.1f / rms;
            for (int pass = 0; pass < 4; pass++)
            {
                double limitedEnergy = 0;
                for (int frame = 0; frame < frames; frame++)
                {
                    float factor = SoftPeakGain(output[frame * 2] * gain, output[frame * 2 + 1] * gain);
                    for (int c = 0; c < 2; c++)
                    {
                        float value = output[frame * 2 + c] * gain * factor;
                        limitedEnergy += value * value;
                    }
                }
                gain *= 0.1f / (float)Math.Sqrt(limitedEnergy / output.Length);
            }
            mean[0] = mean[1] = 0;
            for (int frame = 0; frame < frames; frame++)
            {
                float factor = SoftPeakGain(output[frame * 2] * gain, output[frame * 2 + 1] * gain);
                for (int c = 0; c < 2; c++)
                {
                    output[frame * 2 + c] *= gain * factor;
                    mean[c] += output[frame * 2 + c];
                }
            }
            energy = 0;
            for (int i = 0; i < output.Length; i++)
            {
                output[i] -= (float)(mean[i % 2] / frames);
                energy += output[i] * output[i];
            }
            rms = (float)Math.Sqrt(energy / output.Length);
            using (var writer = new BinaryWriter(File.Create(outputPath)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + output.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)2); writer.Write(rate); writer.Write(rate * 4);
                writer.Write((short)4); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(output.Length * 2);
                foreach (float value in output) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(value, -1, 1) * 32767));
            }
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(outputPath);
            importer.forceToMono = false; importer.ambisonic = false; importer.loadInBackground = false;
            var defaults = importer.defaultSampleSettings;
            defaults.loadType = AudioClipLoadType.Streaming; defaults.compressionFormat = AudioCompressionFormat.Vorbis;
            defaults.quality = 0.8f; defaults.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            defaults.preloadAudioData = true; importer.defaultSampleSettings = defaults;
            importer.SaveAndReimport();
            Debug.Log($"Ambience {sourcePath}: {recording.length:F2}s, {channels} channels; loop {frames / (float)rate:F2}s, overlap {overlap / (float)rate:F2}s, balanced RMS {rms:F3}. Original unchanged.");
            return AssetDatabase.LoadAssetAtPath<AudioClip>(outputPath);
        }

        private static float SoftPeakGain(float left, float right)
        {
            float peak = Mathf.Max(Mathf.Abs(left), Mathf.Abs(right)) / 0.75f;
            return 1 / Mathf.Sqrt(1 + peak * peak);
        }

        private static float BlockRms(float[] samples, int channels, int frame, int count)
        {
            double energy = 0;
            for (int i = frame * channels; i < (frame + count) * channels; i++) energy += samples[i] * samples[i];
            return (float)Math.Sqrt(energy / (count * channels));
        }
    }
}
