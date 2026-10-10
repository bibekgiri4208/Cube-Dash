using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CubeDash.Editor
{
    /// <summary>Original PCM pickup cues and edited CC0 vehicle recordings; no runtime synthesis.</summary>
    internal static class RunnerSoundDesign
    {
        private const int Rate = 44100;
        private static readonly float[] PointNotes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        public static AudioClip Pickup(PowerUpType type)
        {
            string[] names = { "ShieldPickup", "DoublePointsPickup", "FlightPickup", "TruckPickup", "MagnetPickup" };
            float[] lengths = { 0.48f, 0.58f, 0.65f, 0.62f, 0.52f };
            int count = Mathf.RoundToInt(lengths[(int)type] * Rate);
            float[] samples = new float[count];
            System.Random random = new System.Random(673 + (int)type * 137);
            float air = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate, age = i / (float)(count - 1);
                float envelope = Mathf.SmoothStep(0, 1, t / 0.012f) * Mathf.Pow(1 - age, 1.6f);
                air = Mathf.Lerp(air, (float)random.NextDouble() * 2 - 1, 0.12f);
                float sound;
                switch (type)
                {
                    case PowerUpType.Shield:
                        sound = Tone(659, t) * 0.3f + Tone(988, t) * 0.15f + Tone(1318, t) * 0.07f
                            + air * 0.12f * Mathf.Exp(-t * 12);
                        break;
                    case PowerUpType.DoublePoints:
                        float note = PointNotes[Mathf.Min(3, (int)(t / 0.11f))];
                        float beat = t % 0.11f;
                        float pluck = Mathf.SmoothStep(0, 1, beat / 0.008f) * Mathf.Clamp01((0.11f - beat) / 0.016f) * Mathf.Exp(-beat * 13);
                        sound = (Tone(note, beat) * 0.42f + Tone(note * 2, beat) * 0.08f) * pluck;
                        break;
                    case PowerUpType.FighterPlane:
                        sound = Mathf.Sin(2 * Mathf.PI * (260 * t + 850 * t * t)) * 0.28f
                            + air * 0.25f + Tone(1046.5f, t) * 0.08f * age;
                        break;
                    case PowerUpType.Truck:
                        sound = Mathf.Sin(2 * Mathf.PI * (65 * t + 95 * t * t)) * 0.34f
                            + Tone(130, t) * 0.13f + air * 0.23f * Mathf.Exp(-t * 6);
                        break;
                    default:
                        sound = Tone(880 + Mathf.Sin(t * 28) * 110, t) * 0.27f
                            + Tone(1320, t) * 0.15f + air * 0.06f;
                        break;
                }
                samples[i] = sound * envelope;
            }
            return Save(names[(int)type], samples);
        }

        public static AudioClip Engine(bool flight)
        {
            string path = "Assets/Audio/VehicleSources/" + (flight ? "FlightEngineSource" : "TruckEngineSource") + ".mp3";
            if (!File.Exists(path)) throw new InvalidOperationException("The CC0 vehicle recording is required: " + path);
            ImportPcm(path);
            AudioClip recording = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            float[] source = new float[recording.samples];
            if (!recording.GetData(source, 0)) throw new InvalidOperationException("Could not decode the vehicle recording: " + path);

            // Remove DC/subsonic rumble and tame high-frequency hiss without adding musical oscillators.
            float highAlpha = Mathf.Exp(-2 * Mathf.PI * (flight ? 65 : 28) / recording.frequency);
            float lowAlpha = 1 - Mathf.Exp(-2 * Mathf.PI * (flight ? 2800 : 1700) / recording.frequency);
            float previous = 0, high = 0, low = 0;
            for (int i = 0; i < source.Length; i++)
            {
                high = highAlpha * (high + source[i] - previous); previous = source[i];
                low += lowAlpha * (high - low); source[i] = low;
            }
            const int count = Rate * 6, overlap = Rate / 2;
            float[] samples = new float[count];
            int start = SmoothBoundary(source, StableSection(source, recording.frequency), recording.frequency);
            float step = recording.frequency / (float)Rate;
            float Sample(float position)
            {
                int first = Mathf.FloorToInt(position);
                return Mathf.Lerp(source[first], source[Mathf.Min(first + 1, source.Length - 1)], position - first);
            }
            for (int i = 0; i < count; i++)
            {
                float value = Sample(start + i * step);
                if (i < overlap)
                {
                    float angle = i / (float)overlap * Mathf.PI * 0.5f;
                    // The tail continues directly into sample zero; half-second equal-power overlap hides the join.
                    value = Sample(start + (count + i) * step) * Mathf.Cos(angle) + value * Mathf.Sin(angle);
                }
                samples[i] = value;
            }
            double mean = 0, energy = 0;
            foreach (float sample in samples) mean += sample;
            mean /= count;
            float peak = 0;
            for (int i = 0; i < count; i++)
            {
                samples[i] -= (float)mean; peak = Mathf.Max(peak, Mathf.Abs(samples[i])); energy += samples[i] * samples[i];
            }
            float rms = (float)Math.Sqrt(energy / count);
            float gain = Mathf.Min((flight ? 0.13f : 0.15f) / Mathf.Max(0.001f, rms), 0.7f / Mathf.Max(0.001f, peak));
            for (int i = 0; i < count; i++) samples[i] *= gain;
            return Save(flight ? "FlightEngineLoop" : "TruckEngineLoop", samples);
        }

        private static int SmoothBoundary(float[] source, int start, int frequency)
        {
            // Put the wrap on a low-slope part of the recording, rather than a steep exhaust transient.
            int best = start, span = Mathf.CeilToInt(frequency * 6.5f) + 2;
            float bestScore = float.MaxValue;
            for (int offset = -frequency / 50; offset <= frequency / 50; offset++)
            {
                int candidate = start + offset;
                if (candidate < 0 || candidate + span >= source.Length) continue;
                int seam = candidate + frequency * 6;
                float score = Mathf.Abs(source[seam] - source[seam - 1])
                    + Mathf.Abs(source[seam - 1] - source[seam - 2]) + Mathf.Abs(source[seam]) * 0.01f;
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            return best;
        }

        private static int StableSection(float[] source, int frequency)
        {
            // Prefer a steady engine bed over a start, voice, abrupt clunk or shutdown.
            int block = frequency / 4, span = Mathf.CeilToInt(frequency * 6.5f) + 2;
            if (source.Length <= span + frequency) throw new InvalidOperationException("Vehicle recording is too short for a six-second loop.");
            int best = frequency;
            double bestScore = double.MaxValue;
            for (int start = frequency; start + span < source.Length; start += frequency / 2)
            {
                double total = 0, squared = 0;
                for (int window = 0; window < 26; window++)
                {
                    double energy = 0;
                    for (int i = start + window * block; i < start + (window + 1) * block; i++) energy += source[i] * source[i];
                    double rms = Math.Sqrt(energy / block); total += rms; squared += rms * rms;
                }
                double mean = total / 26;
                if (mean < 0.005) continue;
                double score = Math.Max(0, squared / 26 - mean * mean) / (mean * mean);
                if (score < bestScore) { bestScore = score; best = start; }
            }
            return best;
        }

        private static float Tone(float hz, float t) => Mathf.Sin(2 * Mathf.PI * hz * t);
        private static AudioClip Save(string name, float[] samples)
        {
            string path = "Assets/Audio/" + name + ".wav";
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(Rate); writer.Write(Rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (float sample in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -0.95f, 0.95f) * short.MaxValue));
            }
            ImportPcm(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void ImportPcm(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings; importer.forceToMono = true;
            importer.SaveAndReimport();
        }
    }
}
