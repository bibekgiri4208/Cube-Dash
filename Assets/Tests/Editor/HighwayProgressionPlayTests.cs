using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class HighwayProgressionPlayTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeTrafficBendsAndRecyclesWithoutGrowingTheScene()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            game.TogglePause();
            EndlessTrack track = game.Track;
            track.Reset(7321, game.PlayerCubeColor);
            int count = track.GetComponentsInChildren<Transform>(true).Length;
            var visual = new HighwayPresentation();
            try
            {
                RunnerCube slot = track.Segments[1].Cubes[0];
                slot.Configure(CubeColor.Blue, track.ColorMaterial(CubeColor.Blue), false);
                slot.gameObject.SetActive(true);
                Mesh original = slot.GetComponent<MeshFilter>().sharedMesh;
                Vector3 colliderSize = slot.Collider.size;
                visual.ConfigureHazard(slot, HighwayHazard.Truck);
                Physics.SyncTransforms();
                Assert.That(slot.Hazard, Is.EqualTo(HighwayHazard.Truck));
                Assert.That(slot.Collider.bounds.size.z, Is.EqualTo(4.6f).Within(0.01f));
                Assert.That(slot.Collider.bounds.min.y, Is.EqualTo(0).Within(0.01f));
                Assert.That(slot.GetComponent<Renderer>().sharedMaterials.Length, Is.EqualTo(9));
                Assert.That(slot.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(6000), "No legacy box-built traffic may remain in the truck slot.");
                Assert.That(slot.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("CubeDash/Highway Hazard"));
                slot.Configure(CubeColor.Red, track.ColorMaterial(CubeColor.Red), true);
                Assert.That(slot.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(original));
                Assert.That(slot.Collider.size, Is.EqualTo(colliderSize));
                Assert.That(slot.CoinVisible, Is.True);
                Assert.That(slot.GetComponent<Renderer>().enabled, Is.False);
            }
            finally { visual.Dispose(); }

            int trafficSeen = 0;
            var variants = new[] { new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>() };
            for (int step = 0; step < 60; step++)
            {
                foreach (TrackSegment segment in track.Segments)
                    foreach (RunnerCube cube in segment.Cubes)
                        if (cube.gameObject.activeSelf && !cube.CoinVisible)
                        {
                            Assert.That(cube.GetComponent<Renderer>().sharedMaterials.Length, Is.EqualTo(9), "Every visible obstacle, including warm-up slots, uses authored traffic art.");
                            if (cube.Hazard == HighwayHazard.Truck || cube.Hazard == HighwayHazard.Van) trafficSeen++;
                            variants[(int)cube.Hazard].Add(cube.GetComponent<MeshFilter>().sharedMesh.name);
                        }
                track.Advance(42, 0, 0, 1, Vector2.one * 0.45f, 0, false, false, true, false, out _, out _);
                if (step == 12) RenderPreviewIfRequested(game.GameCamera);
            }
            Assert.That(trafficSeen, Is.GreaterThan(0));
            var art = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            foreach (HighwayHazard type in new[] { HighwayHazard.Cube, HighwayHazard.Barrier, HighwayHazard.Van, HighwayHazard.Truck })
                Assert.That(variants[(int)type].Count, Is.EqualTo(art.VariantCount(type)), "Every authored variant must actually spawn through the normal pooled progression: " + type);
            Assert.That(track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(Shader.GetGlobalVector("_CubeDashRoadCurve").x, Is.EqualTo(track.Travelled));
            float frozen = track.Travelled;
            yield return null;
            Assert.That(track.Travelled, Is.EqualTo(frozen), "Pause freezes bends and encounters together.");
            game.StartRun();
            Assert.That(track.Travelled, Is.Zero);
            Assert.That(Shader.GetGlobalVector("_CubeDashRoadCurve").x, Is.Zero);
            Assert.That(track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }

        private static void RenderPreviewIfRequested(Camera camera)
        {
            string path = System.Environment.GetEnvironmentVariable("CUBE_DASH_HIGHWAY_PREVIEW");
            if (string.IsNullOrEmpty(path)) return;
            RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                // Catch a GPU-side property transfer regression, not just readable C# values.
                CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
                Vector3 probe = camera.WorldToScreenPoint(game.Player.position);
                Color pixel = image.GetPixel(Mathf.Clamp(Mathf.RoundToInt(probe.x), 0, image.width - 1),
                    Mathf.Clamp(Mathf.RoundToInt(probe.y), 0, image.height - 1));
                Assert.That(pixel.r, Is.GreaterThan(pixel.g * 1.2f), "The red player must remain red in the curved-world render.");
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                Object.Destroy(target); Object.Destroy(image);
            }
        }
    }
}
