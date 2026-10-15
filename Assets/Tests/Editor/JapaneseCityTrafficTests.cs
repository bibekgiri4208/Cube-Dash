using System.Collections;
using System.IO;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class JapaneseCityTrafficTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [TestCase(HighwayHazard.Van, 0)] [TestCase(HighwayHazard.Van, 1)] [TestCase(HighwayHazard.Van, 2)]
        [TestCase(HighwayHazard.Truck, 0)] [TestCase(HighwayHazard.Truck, 1)]
        public void CityVehicleModelsHaveGroundedWheelsRealGlassAndMatchingBakedSmashParts(HighwayHazard type, int variant)
        {
            var art = AssetDatabase.LoadAssetAtPath<HighwayHazardVisuals>(JapaneseCityTraffic.VisualsPath);
            var catalog = AssetDatabase.LoadAssetAtPath<ObstacleBreakupCatalog>(JapaneseCityTraffic.BreakupsPath);
            Mesh mesh = art.MeshFor(type, variant); Vector3 size = HighwayPresentation.HazardSize(type);
            Assert.That(AssetDatabase.GetAssetPath(mesh).StartsWith(JapaneseCityTraffic.ArtPath + "/"), Is.True);
            Assert.That(mesh.name.StartsWith(type == HighwayHazard.Van ? "Kei" : "Hachiroku"), Is.True);
            Assert.That(mesh.subMeshCount, Is.EqualTo(9)); Assert.That(mesh.vertexCount, Is.InRange(2000, 40000));
            Assert.That(mesh.bounds.min.y, Is.EqualTo(0).Within(0.0001f));
            Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(size.y));
            Assert.That(mesh.vertices.All(v => Mathf.Abs(v.x) <= size.x * 0.5f + 0.001f && Mathf.Abs(v.z) <= size.z * 0.5f + 0.001f), Is.True,
                "Body, mirrors, bumpers and wheels fit the existing footprint without widening lanes or moving encounters.");
            foreach (int surface in new[] { 0, 2, 3, 4, 5, 6, 7, 8 }) Assert.That(mesh.GetIndexCount(surface), Is.GreaterThan(0));
            Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount)); Assert.That(mesh.colors32.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.normals.All(n => n.sqrMagnitude > 0.9f), Is.True);
            var breakup = catalog.For(mesh); Assert.That(breakup.source, Is.EqualTo(mesh), "Never fall back to generic or old van debris.");
            Assert.That(breakup.fragments.Count(f => f.role == ObstacleFragmentRole.Wheel), Is.EqualTo(4));
            Assert.That(breakup.fragments.Count(f => f.role == ObstacleFragmentRole.Glass), Is.GreaterThanOrEqualTo(2));
            Assert.That(breakup.fragments.Count(f => f.role == ObstacleFragmentRole.BodyPanel), Is.GreaterThanOrEqualTo(4));
            Assert.That(breakup.fragments.All(f => AssetDatabase.GetAssetPath(f.mesh).StartsWith(JapaneseCityTraffic.ArtPath + "/Breakup Parts/")), Is.True);
            foreach (var fragment in breakup.fragments.Where(f => f.role == ObstacleFragmentRole.Wheel))
                Assert.That(fragment.mesh.bounds.size.y, Is.InRange(0.56f, 0.63f));
        }

        [Test]
        public void OnlyCityOptsIntoJdmAndAllNonVehicleArtAndSharedPaintStayUnchanged()
        {
            var local = AssetDatabase.LoadAssetAtPath<HighwayHazardVisuals>(JapaneseCityTraffic.VisualsPath);
            var shared = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            Assert.That(local.Materials, Is.EqualTo(shared.Materials));
            foreach (Color tint in new[] { Color.red, Color.blue, Color.green })
            {
                Assert.That(local.PaintFor(HighwayHazard.Truck, tint), Is.EqualTo(Color.white));
                Assert.That(local.PaintFor(HighwayHazard.Van, tint), Is.EqualTo(new Color(0.90f, 0.86f, 0.74f)));
                foreach (HighwayHazard type in new[] { HighwayHazard.Cube, HighwayHazard.Barrier, HighwayHazard.Van, HighwayHazard.Truck })
                {
                    Assert.That(shared.PaintFor(type, tint), Is.EqualTo(tint), "Other maps retain their own traffic colors.");
                    if (type == HighwayHazard.Cube || type == HighwayHazard.Barrier)
                        Assert.That(local.PaintFor(type, tint), Is.EqualTo(tint));
                }
            }
            foreach (HighwayHazard type in new[] { HighwayHazard.Cube, HighwayHazard.Barrier })
                for (int v = 0; v < shared.VariantCount(type); v++) Assert.That(local.MeshFor(type, v), Is.SameAs(shared.MeshFor(type, v)));
            foreach (HighwayHazard type in new[] { HighwayHazard.Van, HighwayHazard.Truck })
            {
                Assert.That(local.VariantCount(type), Is.EqualTo(shared.VariantCount(type)), "Keep deterministic variation indices and pattern state.");
                Assert.That(AssetDatabase.GetAssetPath(shared.MeshFor(type)).StartsWith("Assets/Highway Hazards/"), Is.True);
            }
            foreach (string name in new[] { "Level", "City", "Jungle", "Desert", "Beach", "Country Side" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
                try
                {
                    var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
                    var reference = new SerializedObject(track).FindProperty("hazardVisuals").objectReferenceValue;
                    var pieces = new SerializedObject(track.Debris).FindProperty("catalogOverride").objectReferenceValue;
                    Assert.That(reference, Is.EqualTo(name == "City" ? local : null), name);
                    Assert.That(pieces, Is.EqualTo(name == "City" ? AssetDatabase.LoadAssetAtPath<ObstacleBreakupCatalog>(JapaneseCityTraffic.BreakupsPath) : null), name);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            string before = File.ReadAllText(JapaneseCityBuilder.ScenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            JapaneseCityTraffic.ApplyFromCommandLine();
            Assert.That(File.ReadAllText(JapaneseCityBuilder.ScenePath), Is.EqualTo(before));
            Assert.That(shared.Materials[0].GetColor("_BaseColor"), Is.EqualTo(Color.white));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [Test]
        public void JdmPresentationUsesWhiteCoupesAndIvoryKeiPaintWithoutChangingCollisionOrRecycling()
        {
            var art = AssetDatabase.LoadAssetAtPath<HighwayHazardVisuals>(JapaneseCityTraffic.VisualsPath);
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/ObstacleCube.prefab"));
            var presentation = new HighwayPresentation(art);
            try
            {
                go.transform.position = Vector3.up * 0.575f; go.transform.localScale = Vector3.one * 1.15f;
                var cube = go.GetComponent<RunnerCube>(); presentation.Prewarm(cube);
                int count = go.GetComponentsInChildren<Transform>(true).Length;
                foreach (CubeColor color in new[] { CubeColor.Blue, CubeColor.Green, CubeColor.Red })
                {
                    var paint = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDash" + color + ".mat");
                    foreach (HighwayHazard type in new[] { HighwayHazard.Van, HighwayHazard.Truck })
                        for (int variant = 0; variant < art.VariantCount(type); variant++)
                        {
                            cube.Configure(color, paint); presentation.ConfigureHazard(cube, type, variant); Physics.SyncTransforms();
                            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
                            Assert.That(cube.HazardSourceMesh, Is.SameAs(art.MeshFor(type, variant)));
                            Assert.That(Vector3.Distance(cube.Collider.bounds.size, HighwayPresentation.HazardSize(type)), Is.LessThan(0.001f));
                            Assert.That(cube.Collider.bounds.min.y, Is.EqualTo(0).Within(0.001f));
                            Color expected = type == HighwayHazard.Truck ? Color.white : new Color(0.90f, 0.86f, 0.74f);
                            Assert.That(Vector4.Distance(go.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor"), expected), Is.LessThan(0.00001f), "Body paint must not revert to blue/green gameplay tint.");
                            Assert.That(cube.Color, Is.EqualTo(color), "Cosmetic paint must not alter collision rules.");
                            Assert.That(go.GetComponent<Renderer>().sharedMaterials[5], Is.SameAs(art.Materials[5]), "Keep the black panda trim.");
                            for (int repeat = 0; repeat < 20; repeat++)
                            { cube.Configure(color, paint); presentation.ConfigureHazard(cube, type, variant); Assert.That(go.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh)); }
                        }
                }
                Assert.That(go.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(go); presentation.Dispose(); }
        }

        [UnityTest]
        public IEnumerator CityJdmRendersFrontRearAndMatchingModelShatterWithoutNewObjects()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(JapaneseCityBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.TogglePause(); game.Track.Reset(7321, CubeColor.Red);
            foreach (var rise in game.Track.GetComponentsInChildren<SceneryRise>()) rise.Settle();
            foreach (var segment in game.Track.Segments)
            {
                foreach (var cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (var power in segment.PowerUps) power.gameObject.SetActive(false);
            }
            var section = game.Track.Segments.OrderBy(s => Mathf.Abs(s.transform.position.z)).First();
            var art = AssetDatabase.LoadAssetAtPath<HighwayHazardVisuals>(JapaneseCityTraffic.VisualsPath);
            var presentation = new HighwayPresentation(art);
            var van = section.Cubes[0]; var coupe = section.Cubes[1];
            var canvas = Object.FindAnyObjectByType<Canvas>(); var camera = game.GameCamera;
            int count = game.Track.GetComponentsInChildren<Transform>(true).Length;
            try
            {
                van.gameObject.SetActive(true); coupe.gameObject.SetActive(true);
                van.transform.localPosition = new Vector3(-2.6f, 0.575f, 10); coupe.transform.localPosition = new Vector3(2.6f, 0.575f, 10);
                van.Configure(CubeColor.Green, game.Track.ColorMaterial(CubeColor.Green)); coupe.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
                presentation.ConfigureHazard(van, HighwayHazard.Van); presentation.ConfigureHazard(coupe, HighwayHazard.Truck, 1);
                Physics.SyncTransforms(); foreach (var trees in Object.FindObjectsByType<CitySakuraTrees>()) trees.SyncPresentation();
                var mode = canvas.renderMode; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                Capture(camera, "jdm-with-hud"); canvas.renderMode = mode; canvas.enabled = false; Capture(camera, "jdm-gameplay");
                van.gameObject.SetActive(false); camera.fieldOfView = 42;
                foreach (HighwayHazard type in new[] { HighwayHazard.Van, HighwayHazard.Truck })
                    for (int variant = 0; variant < art.VariantCount(type); variant++)
                    {
                        coupe.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue)); presentation.ConfigureHazard(coupe, type, variant);
                        Vector3 center = coupe.transform.TransformPoint(coupe.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                        camera.transform.position = center + new Vector3(3, 1.55f, -4.4f); camera.transform.LookAt(center); Capture(camera, type + "-" + variant + "-front");
                        camera.transform.position = center + new Vector3(-3, 1.7f, 4.4f); camera.transform.LookAt(center); Capture(camera, type + "-" + variant + "-rear");
                        game.Track.Debris.ResetDebris(); game.Track.Debris.Shatter(coupe, 0);
                        var fragments = AssetDatabase.LoadAssetAtPath<ObstacleBreakupCatalog>(JapaneseCityTraffic.BreakupsPath).For(art.MeshFor(type, variant));
                        Assert.That(game.Track.Debris.ActiveCount, Is.EqualTo(fragments.fragments.Length));
                        Assert.That(game.Track.Debris.GetComponentsInChildren<MeshFilter>().All(f => fragments.fragments.Any(p => p.mesh == f.sharedMesh)), Is.True);
                        var tint = new MaterialPropertyBlock();
                        foreach (var renderer in game.Track.Debris.GetComponentsInChildren<MeshRenderer>())
                        {
                            renderer.GetPropertyBlock(tint, 0);
                            Color expected = type == HighwayHazard.Truck ? Color.white : new Color(0.90f, 0.86f, 0.74f);
                            Assert.That(Vector4.Distance(tint.GetColor("_BaseColor"), expected), Is.LessThan(0.00001f), "Smash panels retain the vehicle's real paint color.");
                        }
                        game.Track.Debris.Tick(0.12f, 0); game.Track.Debris.ResetDebris();
                    }
                Assert.That(game.Track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { presentation.Dispose(); }
            yield return new ExitPlayMode();
        }

        private static void Capture(Camera camera, string suffix)
        {
            var previous = camera.targetTexture; var active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1440, 1080, 24); var image = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target; Canvas.ForceUpdateCanvases();
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().Count(c => c.r > 230 && c.b > 230 && c.g < 30), Is.LessThan(20));
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_CITY_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
            }
            finally { camera.targetTexture = previous; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }
    }
}
