using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class ObstacleBreakupTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void EveryMapKeepsItsExistingInactive128PartPoolAndResolvesSavedCompactModels(int index)
        {
            var scene = EditorSceneManager.OpenPreviewScene(CubeDash.Editor.HighwayObstacleEditorVisuals.ScenePaths[index]);
            try
            {
                var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
                var catalog = Resources.Load<ObstacleBreakupCatalog>(ObstacleBreakupCatalog.ResourcePath);
                Assert.That(game.Track.Debris.Capacity, Is.EqualTo(128)); Assert.That(game.Track.Debris.ActiveCount, Is.Zero);
                Assert.That(game.Track.Debris.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(game.Track.Debris.GetComponentsInChildren<Collider>(true), Is.Empty);
                foreach (var slot in game.Track.Segments.SelectMany(segment => segment.Cubes))
                {
                    Mesh saved = slot.GetComponent<MeshFilter>().sharedMesh;
                    var breakup = catalog.For(saved);
                    Assert.That(breakup.aliases, Does.Contain(saved), "Saved models must select their own breakup, not fall back to a drum.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void AllElevenModelsHaveTheirOwnTexturedRecognisableFragments()
        {
            var catalog = Resources.Load<ObstacleBreakupCatalog>(ObstacleBreakupCatalog.ResourcePath);
            var art = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            Assert.That(catalog.Models.Length, Is.EqualTo(11));
            Assert.That(catalog.Materials, Is.EqualTo(art.Materials));
            var unique = new System.Collections.Generic.HashSet<Mesh>();
            foreach (var model in catalog.Models)
            {
                Assert.That(catalog.For(model.source), Is.SameAs(model));
                foreach (Mesh alias in model.aliases) Assert.That(catalog.For(alias), Is.SameAs(model));
                bool vehicle = model.fragments.Any(fragment => fragment.role == ObstacleFragmentRole.Wheel);
                Assert.That(model.fragments.Length, Is.EqualTo(vehicle ? 14 : 8));
                Bounds combined = new Bounds(model.fragments[0].center, Vector3.zero);
                foreach (var fragment in model.fragments)
                {
                    Mesh mesh = fragment.mesh;
                    Assert.That(unique.Add(mesh), Is.True, "Models must not share a generic fragment shape.");
                    Assert.That(mesh.name, Does.StartWith(model.source.name));
                    Assert.That(mesh.subMeshCount, Is.EqualTo(9));
                    Assert.That(mesh.vertexCount, Is.GreaterThan(15));
                    Assert.That(mesh.bounds.center.magnitude, Is.LessThan(0.02f));
                    Assert.That(mesh.normals.All(n => n.sqrMagnitude > 0.98f), Is.True);
                    Assert.That(mesh.tangents.All(t => !float.IsNaN(t.x) && !float.IsInfinity(t.x)), Is.True);
                    Vector3[] vertices = mesh.vertices; int[] triangles = mesh.triangles;
                    foreach (Vector3 p in vertices) combined.Encapsulate(p + fragment.center);
                    for (int i = 0; i < triangles.Length; i += 3)
                        Assert.That(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).sqrMagnitude, Is.GreaterThan(1e-12f));
                    if (fragment.role == ObstacleFragmentRole.Wheel)
                    {
                        float heightScale = model.source.name == "Long Wheelbase Delivery Van" ? 1.20f : 1;
                        float lengthScale = model.source.name == "Long Wheelbase Delivery Van" ? 1.345f : 1;
                        Assert.That(mesh.bounds.size.y / heightScale, Is.EqualTo(mesh.bounds.size.z / lengthScale).Within(0.015f), "Retain the source tire shape, including the delivery variant's authored stretch.");
                        Assert.That(mesh.GetIndexCount(2), Is.GreaterThan(100), "Round treaded tire geometry stays intact.");
                        Assert.That(mesh.GetIndexCount(3), Is.GreaterThan(30), "Tires retain their alloy hubs.");
                    }
                    if (fragment.role == ObstacleFragmentRole.Glass)
                        Assert.That(Enumerable.Range(0, 9).Where(m => m != 4).Sum(m => (long)mesh.GetIndexCount(m)), Is.Zero);
                }
                Assert.That(Vector3.Distance(combined.min, model.source.bounds.min), Is.LessThan(0.03f));
                Assert.That(Vector3.Distance(combined.max, model.source.bounds.max), Is.LessThan(0.03f));
                if (vehicle)
                {
                    Assert.That(model.fragments.Count(f => f.role == ObstacleFragmentRole.Wheel), Is.EqualTo(4));
                    Assert.That(model.fragments.Count(f => f.role == ObstacleFragmentRole.Glass), Is.EqualTo(4));
                }
            }
        }

        [UnityTest]
        public IEnumerator ActualTruckContactsSelectEveryVariantAndDoNotChangeScoringOrHitboxes()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.enabled = false;
            var catalog = Resources.Load<ObstacleBreakupCatalog>(ObstacleBreakupCatalog.ResourcePath);
            var art = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            var presentation = new HighwayPresentation();
            try
            {
                foreach (HighwayHazard type in Enum.GetValues(typeof(HighwayHazard)))
                    for (int variant = 0; variant < art.VariantCount(type); variant++)
                        foreach (CubeColor color in new[] { CubeColor.Blue, CubeColor.Green })
                        {
                            game.Track.Reset(7321, CubeColor.Red);
                            Clear(game); game.Track.Debris.ResetDebris();
                            RunnerCube slot = game.Track.Segments[1].Cubes[0];
                            slot.transform.position = new Vector3(0, 0.575f, 4);
                            slot.Configure(color, game.Track.ColorMaterial(color), false);
                            presentation.ConfigureHazard(slot, type, variant); slot.gameObject.SetActive(true);
                            Assert.That(slot.HazardSourceMesh, Is.SameAs(art.MeshFor(type, variant)));
                            Vector3 size = slot.Collider.size, center = slot.Collider.center;
                            var breakup = catalog.For(slot.HazardSourceMesh);
                            Assert.That(game.Track.Advance(8, 0, 0, 0, game.TruckPresentation.ContactHalfSize, 0,
                                false, false, false, true, out int collected, out bool used), Is.False);
                            Assert.That(collected, Is.Zero); Assert.That(used, Is.False);
                            Assert.That(game.Track.LastTruckSmashes, Is.EqualTo(1));
                            Assert.That(slot.gameObject.activeSelf, Is.False);
                            Assert.That(slot.Collider.size, Is.EqualTo(size)); Assert.That(slot.Collider.center, Is.EqualTo(center));
                            CheckActive(game.Track.Debris, breakup, game.Track.ColorMaterial(color).GetColor("_BaseColor"));
                            Vector3 position = game.Track.Debris.transform.GetChild(0).position;
                            game.Track.Debris.Tick(0, 0);
                            Assert.That(game.Track.Debris.transform.GetChild(0).position, Is.EqualTo(position));
                            game.Track.Debris.Tick(2, 0); Assert.That(game.Track.Debris.ActiveCount, Is.Zero);
                        }
                Clear(game); game.Track.Debris.ResetDebris();
                var coin = game.Track.Segments[1].Cubes[0]; coin.Configure(CubeColor.Red, game.Track.ColorMaterial(CubeColor.Red), true);
                coin.transform.position = new Vector3(0, 0.575f, 4); coin.gameObject.SetActive(true);
                Assert.That(game.Track.Advance(8, 0, 0, 0, game.TruckPresentation.ContactHalfSize, 0,
                    false, false, false, true, out int coins, out _), Is.False);
                Assert.That(coins, Is.EqualTo(1)); Assert.That(game.Track.LastTruckSmashes, Is.Zero); Assert.That(game.Track.Debris.ActiveCount, Is.Zero);
            }
            finally { presentation.Dispose(); }
        }

        [UnityTest]
        public IEnumerator PoolReusesPartsWithoutCreatingMeshesMaterialsObjectsOrImpactGarbage()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Jungle.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.enabled = false; Clear(game);
            var presentation = new HighwayPresentation();
            try
            {
                var slot = game.Track.Segments[1].Cubes[0]; slot.gameObject.SetActive(true);
                slot.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue), false);
                presentation.ConfigureHazard(slot, HighwayHazard.Truck, 0); Physics.SyncTransforms();
                var debris = game.Track.Debris; debris.Shatter(slot, 0); debris.ResetDebris();
                int meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length, materials = Resources.FindObjectsOfTypeAll<Material>().Length;
                int objects = debris.GetComponentsInChildren<Transform>(true).Length;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 100; i++) debris.Shatter(slot, 0);
                long garbage = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Assert.That(garbage, Is.Zero, "All geometry/palettes/state must be prepared before a Truck impact.");
                Assert.That(debris.ActiveCount, Is.EqualTo(debris.Capacity));
                Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Length, Is.EqualTo(meshes));
                Assert.That(Resources.FindObjectsOfTypeAll<Material>().Length, Is.EqualTo(materials));
                Assert.That(debris.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                for (int i = 0; i < 100; i++)
                {
                    debris.Tick(0.01f, 0.1f);
                    foreach (Transform fragment in debris.transform)
                        if (fragment.gameObject.activeSelf) Assert.That(fragment.GetComponent<MeshFilter>().sharedMesh.vertices.Min(p => fragment.TransformPoint(p).y), Is.GreaterThanOrEqualTo(-0.002f));
                }
                debris.ResetDebris(); Assert.That(debris.ActiveCount, Is.Zero);
                slot.Configure(CubeColor.Green, game.Track.ColorMaterial(CubeColor.Green), false);
                presentation.ConfigureHazard(slot, HighwayHazard.Cube, 1); debris.Shatter(slot, 0);
                CheckActive(debris, Resources.Load<ObstacleBreakupCatalog>(ObstacleBreakupCatalog.ResourcePath).For(slot.HazardSourceMesh), game.Track.ColorMaterial(CubeColor.Green).GetColor("_BaseColor"));
                debris.Tick(2, 0); Assert.That(debris.ActiveCount, Is.Zero);
            }
            finally { presentation.Dispose(); }
        }

        [UnityTest]
        public IEnumerator RenderRealModelBreakupsForVisualReview()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Jungle.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.TogglePause(); Clear(game);
            foreach (SceneryRise rise in game.Track.GetComponentsInChildren<SceneryRise>()) rise.Settle();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
            RoadCurve.Clear();
            var catalog = Resources.Load<ObstacleBreakupCatalog>(ObstacleBreakupCatalog.ResourcePath);
            var art = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            var presentation = new HighwayPresentation();
            var slot = game.Track.Segments[1].Cubes[0];
            try
            {
                foreach (HighwayHazard type in Enum.GetValues(typeof(HighwayHazard)))
                    for (int variant = 0; variant < art.VariantCount(type); variant++)
                    {
                        game.Track.Debris.ResetDebris(); slot.gameObject.SetActive(true);
                        slot.transform.position = new Vector3(0, 0.575f, 7);
                        slot.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue), false); presentation.ConfigureHazard(slot, type, variant);
                        Physics.SyncTransforms();
                        var camera = game.GameCamera; camera.fieldOfView = 46;
                        camera.transform.position = new Vector3(4, 3.7f, 0); camera.transform.LookAt(new Vector3(0, 0.85f, 7.5f));
                        string name = art.MeshFor(type, variant).name;
                        Capture(camera, name + "-before");
                        game.Track.Debris.Shatter(slot, 0); slot.gameObject.SetActive(false);
                        for (int frame = 0; frame < 16; frame++) game.Track.Debris.Tick(0.02f, 0);
                        Assert.That(game.Track.Debris.ActiveCount, Is.EqualTo(catalog.For(slot.HazardSourceMesh).fragments.Length));
                        Capture(camera, name + "-smash");
                    }
            }
            finally { presentation.Dispose(); }
        }

        private static void CheckActive(ObstacleDebris debris, ObstacleBreakupCatalog.Breakup model, Color tint)
        {
            Assert.That(debris.ActiveCount, Is.EqualTo(model.fragments.Length));
            var active = debris.GetComponentsInChildren<MeshFilter>().Where(filter => filter.gameObject.activeSelf).ToArray();
            Assert.That(active.Select(filter => filter.sharedMesh), Is.EquivalentTo(model.fragments.Select(f => f.mesh)));
            var block = new MaterialPropertyBlock();
            foreach (var filter in active)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                renderer.GetPropertyBlock(block, 0); Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), tint), Is.LessThan(0.00001f));
                Assert.That(renderer.sharedMaterials, Is.EqualTo(Resources.Load<ObstacleBreakupCatalog>(ObstacleBreakupCatalog.ResourcePath).Materials));
                renderer.GetPropertyBlock(block, 2); Assert.That(block.isEmpty, Is.True, "Rubber must not become blue/green paint.");
            }
        }
        private static void Clear(CubeDashGame game)
        {
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube slot in segment.Cubes) slot.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
        }
        private static void Capture(Camera camera, string name)
        {
            string prefix = Environment.GetEnvironmentVariable("CUBE_DASH_BREAKUP_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1280, 800, 24); var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().Count(pixel => pixel.r > 230 && pixel.b > 230 && pixel.g < 30), Is.LessThan(20));
                File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
