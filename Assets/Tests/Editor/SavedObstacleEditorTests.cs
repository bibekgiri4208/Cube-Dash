using System;
using System.IO;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class SavedObstacleEditorTests
    {
        [Test]
        public void BothSavedPoolPrefabsContainDetailedPropsInsteadOfCubeMeshes()
        {
            foreach (string path in new[] { "Assets/Prefab/CubeDash/ObstacleCube.prefab", "Assets/Prefab/CubeDash/TrackSegment.prefab" })
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                RunnerCube[] slots = root.GetComponentsInChildren<RunnerCube>(true);
                Assert.That(slots.Length, Is.EqualTo(path.EndsWith("ObstacleCube.prefab") ? 1 : 9));
                foreach (RunnerCube slot in slots) Check(slot);
                if (slots.Length > 1) Assert.That(slots.Select(slot => slot.GetComponent<MeshFilter>().sharedMesh).Distinct().Count(), Is.EqualTo(3));
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void EverySavedGameplaySceneHasTheNewModelsBeforeEnteringPlay(int index)
        {
            Assert.That(Application.isPlaying, Is.False);
            var scene = EditorSceneManager.OpenPreviewScene(HighwayObstacleEditorVisuals.ScenePaths[index]);
            try
            {
                var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
                Mesh player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/PlayerCube.prefab").GetComponentInChildren<PlayerCubeVisual>(true).GetComponent<MeshFilter>().sharedMesh;
                Assert.That(game.Player.GetComponentInChildren<PlayerCubeVisual>(true).GetComponent<MeshFilter>().sharedMesh, Is.SameAs(player));
                foreach (TrackSegment segment in game.Track.Segments)
                {
                    foreach (RunnerCube slot in segment.Cubes)
                    {
                        Check(slot);
                        Assert.That(slot.CoinVisible, Is.EqualTo(slot.Color == game.PlayerCubeColor));
                        Assert.That(slot.GetComponent<Renderer>().enabled, Is.EqualTo(!slot.CoinVisible));
                    }
                    Assert.That(segment.Cubes.Select(slot => slot.GetComponent<MeshFilter>().sharedMesh).Distinct().Count(), Is.EqualTo(3));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Check(RunnerCube slot)
        {
            Mesh mesh = slot.GetComponent<MeshFilter>().sharedMesh; Renderer renderer = slot.GetComponent<Renderer>();
            Assert.That(AssetDatabase.GetAssetPath(mesh), Does.StartWith(HighwayObstacleEditorVisuals.PreviewPath + "/"));
            Assert.That(mesh.vertexCount, Is.GreaterThan(500));
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(mesh.subMeshCount));
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(9));
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("CubeDash/Highway Hazard"));
            Assert.That(slot.Collider.size, Is.EqualTo(Vector3.one)); Assert.That(slot.Collider.center, Is.EqualTo(Vector3.zero));
            Assert.That(slot.transform.localScale, Is.EqualTo(Vector3.one * 1.15f));
            Assert.That(mesh.bounds.min.y, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(slot.CoinVisual, Is.Not.Null);
            Assert.That(slot.CoinVisual.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashCoin.asset")));
        }

        [Test]
        public void SavedJunglePropsRenderInEditModeWithoutCallingTheRuntimeSpawner()
        {
            Assert.That(Application.isPlaying, Is.False);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Jungle.unity");
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
            Camera camera = game.GameCamera;
            camera.scene = scene;
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation; float fov = camera.fieldOfView;
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            var target = new RenderTexture(1280, 800, 24); var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            Vector4 curve = Shader.GetGlobalVector("_CubeDashRoadCurve"), origin = Shader.GetGlobalVector("_CubeDashRoadOrigin");
            try
            {
                RoadCurve.Clear();
                RunnerCube slot = game.Track.Segments.SelectMany(segment => segment.Cubes).First(cube => cube.gameObject.activeInHierarchy && cube.Color == CubeColor.Blue && cube.GetComponent<Renderer>().enabled);
                camera.fieldOfView = 44; camera.transform.position = slot.transform.position + new Vector3(2.6f, 1.1f, -3.3f);
                camera.transform.LookAt(slot.transform.position);
                camera.targetTexture = target;
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().Count(pixel => pixel.r > 230 && pixel.b > 230 && pixel.g < 30), Is.LessThan(20));
                Assert.That(image.GetPixels(480, 250, 320, 300).Count(pixel => pixel.b > pixel.r * 1.25f && pixel.b > pixel.g * 1.1f), Is.GreaterThan(100), "The selected saved blue prop must actually render before Play, not just the surrounding green scenery.");
                string path = Environment.GetEnvironmentVariable("CUBE_DASH_SAVED_OBSTACLE_PREVIEW");
                if (!string.IsNullOrEmpty(path)) File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
                Shader.SetGlobalVector("_CubeDashRoadCurve", curve); Shader.SetGlobalVector("_CubeDashRoadOrigin", origin);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);
                ShaderUtil.allowAsyncCompilation = oldAsync;
            }
        }
    }
}
