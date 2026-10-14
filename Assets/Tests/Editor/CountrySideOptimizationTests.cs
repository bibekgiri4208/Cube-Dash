using System.Collections;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class CountrySideOptimizationTests
    {
        [UnityTearDown] public IEnumerator Leave() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void SavedOptimizationKeepsNativeFoliageRenderingWithoutChangingQualityOrArt()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>(true)).Single();
                var data = new SerializedObject(scenery); var templates = data.FindProperty("plantInstancingTemplates");
                Assert.That(data.FindProperty("instanceNativePlants").boolValue, Is.False,"Forced foliage instancing must not return when the scene is optimized again.");
                Assert.That(templates.arraySize, Is.EqualTo(2));
                for (int i = 0; i < templates.arraySize; i++)
                {
                    var material = templates.GetArrayElementAtIndex(i).objectReferenceValue as Material;
                    Assert.That(material, Is.Not.Null); Assert.That(material.enableInstancing, Is.True);
                    Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                }
                var materials = scenery.CurvePlants.SelectMany(plant => plant.GetComponentsInChildren<Renderer>(true))
                    .SelectMany(renderer => renderer.sharedMaterials).ToArray();
                Assert.That(materials.All(material => AssetDatabase.GetAssetPath(material).StartsWith("Assets/Idyllic Fantasy Nature/")), Is.True);
                int count = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
                CountrySideOptimization.ApplyToScene(scene);
                Assert.That(scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(count));
                Assert.That(scenery.CurvePlants.SelectMany(plant => plant.GetComponentsInChildren<Renderer>(true)).SelectMany(renderer => renderer.sharedMaterials), Is.EqualTo(materials));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var other = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try { Assert.Throws<System.InvalidOperationException>(() => CountrySideOptimization.ApplyToScene(other)); }
            finally { EditorSceneManager.ClosePreviewScene(other); }
        }

        [Test]
        public void PlantInstancingPreservesOverridesRestoresMaterialsAndNeverChangesTheSource()
        {
            var plant = new GameObject("Test native plant",typeof(MeshFilter),typeof(MeshRenderer));
            Shader shader = Shader.Find("Idyllic Fantasy Nature/Vegetation");
            var source = new Material(shader); var template = new Material(shader) { enableInstancing = true };
            CountrySidePlantInstancing instancing = null;
            try
            {
                plant.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                var renderer = plant.GetComponent<MeshRenderer>(); renderer.sharedMaterial = source;
                var properties = new MaterialPropertyBlock(); properties.SetFloat("_TestOverride",0.7f); renderer.SetPropertyBlock(properties);
                instancing = new CountrySidePlantInstancing(new[] { plant.transform },new[] { template });
                Assert.That(instancing.RendererCount, Is.Zero,"Custom per-renderer authoring must not be overwritten.");
                instancing.Dispose(); renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_TestOverride"), Is.EqualTo(0.7f));
                renderer.SetPropertyBlock(null);
                instancing = new CountrySidePlantInstancing(new[] { plant.transform },new[] { template });
                if (SystemInfo.supportsInstancing)
                {
                    Assert.That(instancing.RendererCount, Is.EqualTo(1));
                    Assert.That(renderer.sharedMaterial.enableInstancing, Is.True);
                    Assert.That(renderer.sharedMaterial.shader, Is.SameAs(source.shader));
                    Assert.That(source.enableInstancing, Is.False);
                }
                instancing.Dispose(); instancing = null;
                Assert.That(renderer.sharedMaterial, Is.SameAs(source)); Assert.That(renderer.HasPropertyBlock(), Is.False);
            }
            finally { instancing?.Dispose(); Object.DestroyImmediate(plant); Object.DestroyImmediate(source); Object.DestroyImmediate(template); }
        }

        [Test]
        public void SharedCurveSamplerMatchesTheOriginalAtNearFarAndEnvelopeBoundaries()
        {
            foreach (float distance in new[] { 0f,59,60,61,120,239,240,522,1572,100000 })
            foreach (float phase in new[] { 0f,0.042f,3.14f,6.282f })
            foreach (float strength in new[] { 0f,0.5f,1f })
            {
                var sampler = new RoadCurve.Sampler(distance,phase,strength);
                foreach (float z in new[] { -568f,-1,0,6,6.001f,30,65,180,185.99f,186,200,568,2000 })
                    Assert.That(sampler.Offset(z), Is.EqualTo(RoadCurve.Offset(z,distance,phase,strength)).Within(0.0001f));
            }
        }

        [UnityTest]
        public IEnumerator NativeFoliageKeepsOriginalMaterialsAndRendererOverridesAcrossMovingFrames()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CountrySideSceneBuilder.ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var scenery = Object.FindAnyObjectByType<CountrySideRunnerScenery>();
            game.StartRun(); game.TogglePause(); game.Track.Reset(42,game.PlayerCubeColor); scenery.SyncDistance();
            var native = scenery.CurvePlants.SelectMany(plant => plant.GetComponentsInChildren<MeshRenderer>(true)).Distinct().ToArray();
            var palettes = native.Select(renderer => renderer.sharedMaterials).ToArray();
            var meshes = native.Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).ToArray();
            Assert.That(palettes.SelectMany(palette => palette).All(material => AssetDatabase.GetAssetPath(material).StartsWith("Assets/Idyllic Fantasy Nature/")), Is.True);
            var properties = new MaterialPropertyBlock();
            for (int frame = 0; frame < 48; frame++)
            {
                game.Track.Advance(1.5f,0,0,1,Vector2.one * 0.45f,game.Player.position.z,false,false,true,false,out _,out _);
                scenery.SyncDistance(); yield return null;
                Assert.That(scenery.InstancedPlantRendererCount, Is.Zero);
                for (int i = 0; i < native.Length; i++)
                {
                    Assert.That(native[i].sharedMaterials, Is.EqualTo(palettes[i]),native[i].name);
                    Assert.That(native[i].GetComponent<MeshFilter>().sharedMesh, Is.SameAs(meshes[i]));
                    native[i].GetPropertyBlock(properties);
                    Assert.That(properties.GetFloat("_CubeDashPlantBatch"), Is.Zero,"No forced SRP opt-out may return during travel or LOD changes.");
                }
            }
        }

        [UnityTest]
        public IEnumerator BatchingPreservesRenderedSceneryAndPlantPositionsThroughTravelPauseAndReset()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CountrySideSceneBuilder.ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var scenery = Object.FindAnyObjectByType<CountrySideRunnerScenery>();
            game.StartRun(); game.TogglePause(); game.Track.Reset(42,game.PlayerCubeColor); scenery.SyncDistance();
            Assert.That(scenery.BatchedSourceCount, Is.GreaterThan(40));
            Assert.That(scenery.SceneryBatchCount, Is.LessThan(scenery.BatchedSourceCount * 0.6f));
            Assert.That(scenery.InstancedPlantRendererCount, Is.Zero,"Native foliage uses its stable SRP rendering path.");
            var initial = scenery.CurvePlants.Select(plant => plant.localPosition).ToArray();
            var native = scenery.CurvePlants.SelectMany(plant => plant.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            var nativeMeshes = native.Select(filter => filter.sharedMesh).ToArray();
            var sources = scenery.CurvedSurfaces.Where(renderer => !renderer.enabled).ToArray();
            var batches = scenery.Tiles.SelectMany(tile => tile.GetComponentsInChildren<MeshRenderer>()).Where(renderer => renderer.name == "Countryside rigid batch").ToArray();
            long IndexCount(Renderer renderer)
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh; long total = 0;
                for (int i = 0; i < mesh.subMeshCount; i++) total += mesh.GetIndexCount(i);
                return total;
            }
            Assert.That(batches.Sum(renderer => IndexCount(renderer)), Is.EqualTo(sources.Sum(renderer => IndexCount(renderer))),"All authored triangles are retained exactly; this is batching, not simplification.");
            var homes = (Vector3[])typeof(CountrySideRunnerScenery).GetField("plantHomes",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(scenery);
            int count = game.gameObject.scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
            foreach (float step in new[] { 0f,522f,1050f })
            {
                if (step > 0) game.Track.Advance(step,0,0,1,Vector2.one * 0.45f,game.Player.position.z,false,false,true,false,out _,out _);
                scenery.SyncDistance();
                Vector4 curve = Shader.GetGlobalVector("_CubeDashRoadCurve");
                for (int i = 0; i < scenery.CurvePlants.Length; i++)
                {
                    Transform plant = scenery.CurvePlants[i];
                    Vector3 world = plant.parent.TransformPoint(homes[i]);
                    world.x += RoadCurve.Offset(world.z-curve.w,curve.x,curve.y,curve.z);
                    Assert.That(Vector3.Distance(plant.position,world), Is.LessThan(0.001f),plant.name);
                }
                CompareRendering(game,scenery,game.Track.Travelled.ToString("F0"));
            }
            Vector3[] paused = scenery.CurvePlants.Select(plant => plant.localPosition).ToArray();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(scenery.CurvePlants.Select(plant => plant.localPosition), Is.EqualTo(paused));
            Assert.That(game.gameObject.scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(count));
            game.Track.Reset(42,game.PlayerCubeColor); scenery.SyncDistance();
            Assert.That(scenery.CurvePlants.Select(plant => plant.localPosition), Is.EqualTo(initial));
            Assert.That(native.Select(filter => filter.sharedMesh), Is.EqualTo(nativeMeshes),"Native foliage LOD meshes are never combined or replaced.");
        }

        private static void CompareRendering(CubeDashGame game, CountrySideRunnerScenery scenery, string suffix)
        {
            var batches = scenery.Tiles.SelectMany(tile => tile.GetComponentsInChildren<MeshRenderer>()).Where(renderer => renderer.name == "Countryside rigid batch").ToArray();
            var sources = scenery.CurvedSurfaces.Where(renderer => !renderer.enabled).ToArray();
            var instancing = (CountrySidePlantInstancing)typeof(CountrySideRunnerScenery).GetField("plantInstancing",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(scenery);
            Camera camera = game.GameCamera; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1672,940,24); var image = new Texture2D(1672,940,TextureFormat.RGB24,false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                Color32[] optimized = Capture("optimized");
                foreach (var batch in batches) batch.enabled = false;
                foreach (var source in sources) source.enabled = true;
                instancing?.SetEnabled(false);
                Color32[] reference = Capture("reference");
                double error = 0; int changed = 0;
                for (int i = 0; i < reference.Length; i++)
                {
                    int difference = Mathf.Abs(reference[i].r-optimized[i].r) + Mathf.Abs(reference[i].g-optimized[i].g) + Mathf.Abs(reference[i].b-optimized[i].b);
                    error += difference; if (difference > 24) changed++;
                }
                double mean = error / (reference.Length*765.0), fraction = changed / (double)reference.Length;
                TestContext.WriteLine($"{suffix}m render comparison: mean RGB error {mean:F6}, changed pixels {fraction:P3}.");
                Assert.That(mean, Is.LessThan(0.002),"Batching must retain the original shading, UVs, geometry and shadows.");
                Assert.That(fraction, Is.LessThan(0.005));
                Color32[] Capture(string name)
                {
                    for (int i = 0; i < 3; i++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
                    string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_COUNTRY_OPT_PREVIEW");
                    if (!string.IsNullOrEmpty(prefix)) System.IO.File.WriteAllBytes(prefix + "-" + suffix + "m-" + name + ".png",image.EncodeToPNG());
                    return image.GetPixels32();
                }
            }
            finally
            {
                foreach (var batch in batches) batch.enabled = true;
                foreach (var source in sources) source.enabled = false;
                instancing?.SetEnabled(true);
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = oldAsync;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
