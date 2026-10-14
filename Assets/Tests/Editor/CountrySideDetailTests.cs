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
    public sealed class CountrySideDetailTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void HillsAndWoodlandBelongToThreeSavedTravellingLandscapeTiles()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = Find<CountrySideRunnerScenery>(scene);
                Assert.That(scenery.LandscapeTiles.Length, Is.EqualTo(3));
                Assert.That(scenery.LandscapeLength, Is.EqualTo(568));
                foreach (Transform tile in scenery.LandscapeTiles)
                {
                    Assert.That(tile.Cast<Transform>().Count(child => child.name.Contains("Hills")), Is.EqualTo(11));
                    foreach (Transform hill in tile.Cast<Transform>().Where(child => child.name.Contains("Hills")))
                        Assert.That(Mathf.Abs(hill.localPosition.x) - Mathf.Max(hill.localScale.x, hill.localScale.z), Is.GreaterThanOrEqualTo(41.9f),
                            "A former center-horizon hill must never scroll across the road.");
                    Transform forest = tile.Find("Trees - sakura avenue and green woodland");
                    Assert.That(forest.childCount, Is.EqualTo(72));
                    Assert.That(forest.Cast<Transform>().All(tree => scenery.CurvePlants.Contains(tree)), Is.True);
                    Assert.That(tile.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(tile.GetComponentsInChildren<Renderer>().All(renderer => !renderer.gameObject.isStatic), Is.True);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SavedFarmyardsHaveTexturedArchitectureGroundAndNativeLayeredPlanting()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = Find<CountrySideRunnerScenery>(scene);
                foreach (Transform tile in scenery.Tiles)
                {
                    Transform detail = tile.Find(CountrySideDetailBuilder.DetailName);
                    Assert.That(detail, Is.Not.Null);
                    Assert.That(detail.Cast<Transform>().Count(child => child.name.StartsWith("Farmyard -")), Is.EqualTo(6));
                    Assert.That(detail.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(detail.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                    Assert.That(detail.GetComponentsInChildren<Transform>().Count(item => PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject)), Is.GreaterThan(300));
                }
                foreach (string name in new[] { "Warm Plaster", "Dark Timber", "Cedar Timber", "Kawara Roof Tiles", "Weathered Stone", "Farm Path", "Field Soil" })
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(CountrySideSceneBuilder.ArtPath + "Materials/" + name + ".mat");
                    Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, name);
                    Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                }
                Mesh house = AssetDatabase.LoadAssetAtPath<Mesh>(CountrySideSceneBuilder.ArtPath + "Meshes/Minka Farmhouse.asset");
                Assert.That(house.vertexCount, Is.GreaterThan(14000), "Include shutters, siding, rafters and the entry canopy.");
                Assert.That(house.subMeshCount, Is.EqualTo(7));
                foreach (MeshRenderer renderer in scenery.CurvedSurfaces)
                    Assert.That(renderer.sharedMaterials.All(material => !ShaderUtil.ShaderHasError(material.shader)), Is.True, renderer.name);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void RecycledTurfHasMatchingEndRowsWithoutGroundCracks()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CountrySideSceneBuilder.ArtPath + "Meshes/Travelling Turf.asset");
            Vector3[] vertices = mesh.vertices; Color[] colors = mesh.colors;
            var back = Enumerable.Range(0, vertices.Length).Where(i => Mathf.Abs(vertices[i].z + 36) < 0.001f).OrderBy(i => vertices[i].x).ToArray();
            var front = Enumerable.Range(0, vertices.Length).Where(i => Mathf.Abs(vertices[i].z - 248) < 0.001f).OrderBy(i => vertices[i].x).ToArray();
            Assert.That(back.Length, Is.GreaterThan(20)); Assert.That(front.Length, Is.EqualTo(back.Length));
            for (int i = 0; i < back.Length; i++)
            {
                Assert.That(vertices[back[i]].y, Is.EqualTo(vertices[front[i]].y).Within(0.001f));
                Assert.That(Vector4.Distance(colors[back[i]], colors[front[i]]), Is.LessThan(0.001f));
            }
        }

        [Test]
        public void LandscapeWrapsOnlyBehindTheCameraAndKeepsAnUnbrokenPoolOverLongRuns()
        {
            for (int distance = 0; distance < 100000; distance += 37)
            {
                float[] positions = Enumerable.Range(0, 3).Select(i => CountrySideRunnerScenery.TilePosition(distance, i, 3, 568, 700, -200)).OrderBy(z => z).ToArray();
                Assert.That(positions[1] - positions[0], Is.EqualTo(568).Within(0.02f));
                Assert.That(positions[2] - positions[1], Is.EqualTo(568).Within(0.02f));
                Assert.That(positions[0] + 700, Is.GreaterThanOrEqualTo(-200));
                Assert.That(positions[2], Is.GreaterThan(0));
            }
        }

        [Test]
        public void ReapplyingTheUpgradePreservesManualEditsAndRejectsOtherScenes()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = Find<CountrySideRunnerScenery>(scene);
                Transform house = scenery.Tiles[0].Find("Village - original Japanese minka and kura").GetChild(0);
                house.localPosition += Vector3.right; Vector3 position = house.localPosition;
                int count = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
                CountrySideDetailBuilder.ApplyToScene(scene);
                Assert.That(house.localPosition, Is.EqualTo(position));
                Assert.That(scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(count));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Scene other = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try { Assert.Throws<System.InvalidOperationException>(() => CountrySideDetailBuilder.ApplyToScene(other)); }
            finally { EditorSceneManager.ClosePreviewScene(other); }
        }

        [UnityTest]
        public IEnumerator HillsActuallyPassThePlayerAndFreezeResetAndRecycleWithoutSpawning()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CountrySideSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var scenery = Object.FindAnyObjectByType<CountrySideRunnerScenery>();
            game.StartRun(); game.TogglePause(); game.Track.Reset(42, game.PlayerCubeColor); scenery.SyncDistance();
            Transform hill = scenery.LandscapeTiles[0].GetChild(0);
            Vector3 start = hill.position; Vector3 screenStart = game.GameCamera.WorldToViewportPoint(start);
            Transform tree = scenery.LandscapeTiles[0].Find("Trees - sakura avenue and green woodland").GetChild(0);
            Vector3 treeLocal = tree.localPosition;
            int count = game.gameObject.scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
            Capture(game, "start");
            game.Track.Advance(60, 0, 0, 1, Vector2.one * 0.45f, game.Player.position.z, false, false, true, false, out _, out _);
            scenery.SyncDistance();
            Assert.That(hill.position.z, Is.EqualTo(start.z - 60).Within(0.01f), "Distant hills must travel, not remain locked to the player.");
            Assert.That(Vector3.Distance(game.GameCamera.WorldToViewportPoint(hill.position), screenStart), Is.GreaterThan(0.05f));
            Assert.That(tree.localPosition.z, Is.EqualTo(treeLocal.z));
            Vector3 frozen = hill.position;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(hill.position, Is.EqualTo(frozen), "Pause stops landscape travel too.");
            Capture(game, "60m");
            for (int i = 0; i < 100; i++)
            {
                game.Track.Advance(42, 0, 0, 1, Vector2.one * 0.45f, game.Player.position.z, false, false, true, false, out _, out _);
                scenery.SyncDistance();
                if (i == 10) Capture(game, "522m");
                if (i == 35) Capture(game, "1572m");
            }
            Assert.That(game.gameObject.scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(count));
            Assert.That(scenery.LandscapeTiles.All(tile => tile != null), Is.True);
            game.Track.Reset(42, game.PlayerCubeColor); scenery.SyncDistance();
            Assert.That(hill.position, Is.EqualTo(start));
            Assert.That(scenery.LandscapeTiles.Select(tile => tile.localPosition.z), Is.EqualTo(new[] { 0f, 568f, 1136f }));
            Capture(game, "reset");
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static void Capture(CubeDashGame game, string suffix)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_COUNTRY_TRAVEL_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            Camera camera = game.GameCamera; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1672, 940, 24); var image = new Texture2D(1672, 940, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false;
                for (int i = 0; i < 3; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = oldAsync;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
