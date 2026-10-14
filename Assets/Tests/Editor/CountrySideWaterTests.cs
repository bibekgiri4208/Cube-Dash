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
    public sealed class CountrySideWaterTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void CanalAndRiceHaveDistinctSavedAnimatedWaterWithoutOceanWavesOrCameraCopies()
        {
            Material canal = Load(CountrySideWaterBuilder.CanalMaterialPath), paddy = Load(CountrySideWaterBuilder.PaddyMaterialPath);
            Assert.That(canal, Is.Not.SameAs(paddy));
            foreach (Material material in new[] { canal, paddy })
            {
                Assert.That(material.shader.name, Is.EqualTo(CountrySideWaterBuilder.ShaderName));
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                Assert.That(material.shader.isSupported, Is.True);
                Assert.That(material.renderQueue, Is.LessThan(2500), "Opaque shallow-bed optics avoid reed/rice sorting and scene-copy overhead.");
                Assert.That(material.FindPass("Water Forward"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.GetFloat("_RippleStrength"), Is.GreaterThan(0));
                Assert.That(material.GetFloat("_ReflectionStrength"), Is.GreaterThan(0.5f));
            }
            Assert.That(paddy.GetFloat("_RippleStrength"), Is.LessThan(canal.GetFloat("_RippleStrength")));
            Assert.That(paddy.GetVector("_Flow").magnitude, Is.LessThan(canal.GetVector("_Flow").magnitude * 0.1f));
            Assert.That(paddy.GetFloat("_BedVisibility"), Is.GreaterThan(canal.GetFloat("_BedVisibility")));
        }

        [Test]
        public void WaterSlotsAreUpwardSurfacesWithNormalizedShorelineCoordinatesAndContinuousCanalEnds()
        {
            foreach (var item in new[] { ("Rural Infrastructure", 3), ("Rice Paddy", 1) })
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CountrySideSceneBuilder.ArtPath + "Meshes/" + item.Item1 + ".asset");
                int[] indices = mesh.GetTriangles(item.Item2).Distinct().ToArray();
                Assert.That(indices.Length, Is.EqualTo(4));
                Assert.That(indices.All(i => mesh.normals[i].y > 0.99f), Is.True);
                Assert.That(indices.All(i => mesh.uv[i].x >= 0 && mesh.uv[i].x <= 1 && mesh.uv[i].y >= 0 && mesh.uv[i].y <= 1), Is.True);
                Assert.That(indices.Select(i => mesh.uv[i]).Distinct().Count(), Is.EqualTo(4));
                if (item.Item2 != 3) continue;
                Assert.That(indices.Min(i => mesh.vertices[i].z), Is.EqualTo(-36).Within(0.001f));
                Assert.That(indices.Max(i => mesh.vertices[i].z), Is.EqualTo(248).Within(0.001f));
                Assert.That(indices.Min(i => mesh.vertices[i].x), Is.GreaterThan(6), "Road and shoulder remain dry.");
            }
        }

        [Test]
        public void BothTravellingTilesKeepTheCorrectWaterMaterialsAndCurveReferences()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>(true)).Single();
                foreach (Transform tile in scenery.Tiles)
                {
                    var water = tile.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.sharedMaterials.Any(material => material.shader.name == CountrySideWaterBuilder.ShaderName)).ToArray();
                    Assert.That(water.Length, Is.GreaterThanOrEqualTo(4));
                    foreach (var renderer in water)
                    {
                        Assert.That(scenery.CurvedSurfaces.Contains(renderer), Is.True, renderer.name);
                        Assert.That(renderer.sharedMaterials.Where(material => material.shader.name == CountrySideWaterBuilder.ShaderName)
                            .All(material => AssetDatabase.GetAssetPath(material) == (renderer.name.Contains("Paddy") ? CountrySideWaterBuilder.PaddyMaterialPath : CountrySideWaterBuilder.CanalMaterialPath)), Is.True, renderer.name);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void WaterNormalAnimationIsDeterministicNonflatAndCalmerInTheRiceFields()
        {
            Shader shader = Shader.Find("Hidden/CubeDash/Country Water Probe");
            Assert.That(shader, Is.Not.Null); Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var probe = new Material(shader);
            try
            {
                float canalVariation = 0, paddyVariation = 0;
                foreach (string path in new[] { CountrySideWaterBuilder.CanalMaterialPath, CountrySideWaterBuilder.PaddyMaterialPath })
                {
                    Material material = Load(path);
                    probe.SetVector("_Flow", material.GetVector("_Flow"));
                    probe.SetFloat("_RippleScale", material.GetFloat("_RippleScale"));
                    probe.SetFloat("_RippleStrength", material.GetFloat("_RippleStrength"));
                    Color[] first = Sample(probe, 0), later = Sample(probe, 2), reset = Sample(probe, 0);
                    Assert.That(first, Is.EqualTo(reset), "A paused/reset clock gives exactly the same water.");
                    Assert.That(first.Zip(later, (a,b) => Mathf.Abs(a.r-b.r) + Mathf.Abs(a.b-b.b)).Average(), Is.GreaterThan(0.008f));
                    float variation = first.Average(color => Mathf.Abs(color.r - 0.5f) + Mathf.Abs(color.b - 0.5f));
                    Assert.That(variation, Is.GreaterThan(0.015f));
                    if (path == CountrySideWaterBuilder.CanalMaterialPath) canalVariation = variation; else paddyVariation = variation;
                }
                Assert.That(paddyVariation, Is.LessThan(canalVariation * 0.65f));
            }
            finally { Object.DestroyImmediate(probe); }
        }

        private static Material Load(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); Assert.That(material, Is.Not.Null, path); return material;
        }
        [Test]
        public void ReapplyingWaterPreservesSavedMeshesAndManualSceneEditsAndRejectsOtherScenes()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>(true)).Single();
                var house = scenery.Tiles[0].Find("Village - original Japanese minka and kura").GetChild(0);
                house.localPosition += Vector3.right; Vector3 position = house.localPosition;
                var renderers = scenery.CurvedSurfaces.ToArray();
                var water = Load(CountrySideWaterBuilder.PaddyMaterialPath);
                Color color = water.GetColor("_BaseColor");
                CountrySideWaterBuilder.ApplyToScene(scene);
                Assert.That(house.localPosition, Is.EqualTo(position));
                Assert.That(scenery.CurvedSurfaces, Is.EqualTo(renderers));
                Assert.That(water.GetColor("_BaseColor"), Is.EqualTo(color));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var other = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try { Assert.Throws<System.InvalidOperationException>(() => CountrySideWaterBuilder.ApplyToScene(other)); }
            finally { EditorSceneManager.ClosePreviewScene(other); }
        }

        [UnityTest]
        public IEnumerator WaterKeepsItsShaderDuringTravelAndItsAnimationClockFreezesAndResetsWithTheGame()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CountrySideSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>();
            var scenery = Object.FindAnyObjectByType<CountrySideRunnerScenery>();
            game.StartRun(); game.ToggleInvincibility();
            float start = Shader.GetGlobalFloat("_CubeDashEnvironmentTime");
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Shader.GetGlobalFloat("_CubeDashEnvironmentTime"), Is.GreaterThan(start));
            game.TogglePause();
            float paused = Shader.GetGlobalFloat("_CubeDashEnvironmentTime");
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Shader.GetGlobalFloat("_CubeDashEnvironmentTime"), Is.EqualTo(paused));
            game.Track.Advance(1572,0,0,1,Vector2.one * 0.45f,game.Player.position.z,false,false,true,false,out _,out _);
            scenery.SyncDistance();
            foreach (Transform tile in scenery.Tiles)
            {
                var water = tile.GetComponentsInChildren<MeshRenderer>(true).SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material.name == "Canal Water" || material.name == "Rice Paddy Water").ToArray();
                Assert.That(water.Length, Is.EqualTo(5));
                Assert.That(water.All(material => material.shader.name == CountrySideWaterBuilder.ShaderName), Is.True);
            }
            game.StartRun(); scenery.SyncDistance();
            Assert.That(Shader.GetGlobalFloat("_CubeDashEnvironmentTime"), Is.Zero);
            Assert.That(scenery.Tiles[0].localPosition.z, Is.Zero);
        }

        private static Color[] Sample(Material material, float time)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(64,64,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var texture = new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);
            try
            {
                material.SetFloat("_ProbeTime", time); Graphics.Blit(Texture2D.blackTexture,target,material);
                RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,64,64),0,0); texture.Apply(); return texture.GetPixels();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(texture); }
        }
    }
}
