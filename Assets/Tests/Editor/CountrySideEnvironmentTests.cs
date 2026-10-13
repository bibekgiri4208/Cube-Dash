using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Tests
{
    public sealed class CountrySideEnvironmentTests
    {
        [Test]
        public void SavedCountrySideHasOneEnvironmentAndPreservesTheOriginalCameraAndSun()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                GameObject environment = roots.Single(root => root.name == CountrySideSceneBuilder.RootName);
                Assert.That(environment.transform.Find("Landscape - green valley and layered hills"), Is.Not.Null);
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Light>(true)).Count(), Is.EqualTo(1));
                Camera camera = roots.Single(root => root.name == "Main Camera").GetComponent<Camera>();
                Assert.That(camera.CompareTag("MainCamera"), Is.True);
                Assert.That(camera.transform.position.z, Is.LessThan(0));
                Assert.That(camera.transform.forward.z, Is.GreaterThan(0.9f));
                Assert.That(environment.GetComponentsInChildren<CubeDashGame>(true), Is.Empty, "Environment design must not introduce an unrequested gameplay/navigation change.");
                Assert.That(environment.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(environment.GetComponentInChildren<Volume>(true).sharedProfile, Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TreesGrassAndFlowersAreRealLinkedNaturePackInstancesWithTheirOwnMaterials()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CountrySideSceneBuilder.EnvironmentPath);
            var nature = prefab.GetComponentsInChildren<Transform>(true)
                .Where(item => PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject))
                .Select(item => new { Root = item, Path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject) })
                .Where(item => item.Path.StartsWith("Assets/Idyllic Fantasy Nature/Prefabs/")).ToArray();
            Assert.That(nature.Length, Is.GreaterThan(500));
            Assert.That(nature.Count(item => item.Path.Contains("BlossomTree")), Is.EqualTo(10));
            Assert.That(nature.Count(item => item.Path.Contains("FlowerMeadow")), Is.GreaterThan(250));
            Assert.That(nature.Count(item => item.Path.Contains("Grass_")), Is.EqualTo(170));
            foreach (var item in nature)
            {
                Assert.That(item.Root.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                Assert.That(item.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(item.Root.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
                foreach (MeshRenderer renderer in item.Root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.That((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) == 0, Is.True);
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null);
                        Assert.That(AssetDatabase.GetAssetPath(material).StartsWith("Assets/Idyllic Fantasy Nature/"), Is.True);
                    }
                }
            }
        }

        [Test]
        public void OriginalJapaneseArchitectureHasDetailedSavedGeometryAndCompleteMaterialSlots()
        {
            foreach (string name in new[] { "Minka Farmhouse", "Kura Storehouse", "Village Torii", "Stone Lantern", "Canal Footbridge", "Rice Paddy" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CountrySideSceneBuilder.ArtPath + "Prefabs/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null);
                MeshFilter mesh = prefab.GetComponentInChildren<MeshFilter>();
                MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
                Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Does.StartWith(CountrySideSceneBuilder.ArtPath + "Meshes/"));
                Assert.That(mesh.sharedMesh.subMeshCount, Is.EqualTo(renderer.sharedMaterials.Length));
                Assert.That(renderer.sharedMaterials.All(material => material != null), Is.True);
                Assert.That(mesh.sharedMesh.vertexCount, Is.GreaterThan(100));
                Assert.That(mesh.sharedMesh.vertices.All(p => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z)), Is.True);
                if (name == "Minka Farmhouse")
                {
                    Assert.That(mesh.sharedMesh.vertexCount, Is.GreaterThan(5000), "Modeled roof tiles, timber frames and shoji grids, not a plain cube house.");
                    Assert.That(mesh.sharedMesh.bounds.max.y, Is.GreaterThan(5));
                    Assert.That(mesh.sharedMesh.bounds.size.z, Is.GreaterThan(7));
                }
            }
        }

        [Test]
        public void FlowerFieldsPaddiesVillageAndLayeredHillsAreSeparateEditableGroups()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CountrySideSceneBuilder.EnvironmentPath);
            Assert.That(prefab.transform.Find("Flower fields - linked Idyllic meadows").childCount, Is.EqualTo(6));
            Assert.That(prefab.transform.Find("Village - original Japanese minka and kura").childCount, Is.EqualTo(6));
            Assert.That(prefab.transform.Find("Rice paddies - flooded seedling terraces").childCount, Is.EqualTo(4));
            Assert.That(prefab.transform.Find("Landscape - green valley and layered hills").childCount, Is.EqualTo(12));
            Transform road = prefab.transform.Find("Runner road - clear 8.4 metre corridor");
            Assert.That(road, Is.Not.Null);
            Mesh mesh = road.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(mesh.bounds.size.x, Is.EqualTo(8.4f).Within(0.01f));
            Assert.That(mesh.bounds.size.z, Is.GreaterThan(250));
            Assert.That(mesh.bounds.max.y, Is.LessThan(0.1f));
        }

        [Test]
        public void WoodlandIsPlantedOnTheLandscapeAndTheRoadCorridorIsClear()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CountrySideSceneBuilder.EnvironmentPath);
            Transform trees = prefab.transform.Find("Trees - sakura avenue and green woodland");
            Assert.That(trees.childCount, Is.EqualTo(82));
            foreach (Transform tree in trees)
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree.gameObject);
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Bounds bounds = (Bounds)typeof(MainMenuAssetStoreTrees).GetMethod("BoundsOf", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { source });
                Vector3 position = tree.localPosition;
                float bottom = position.y + bounds.min.y * tree.localScale.y;
                float height = (float)typeof(CountrySideSceneBuilder).GetMethod("LandscapeHeight", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { position.x, position.z });
                Assert.That(bottom, Is.EqualTo(height).Within(0.2f), tree.name);
                Assert.That(Mathf.Abs(position.x), Is.GreaterThan(10));
            }
            Transform village = prefab.transform.Find("Village - original Japanese minka and kura");
            foreach (Transform house in village)
            {
                Vector3 front = house.localRotation * Vector3.back;
                Assert.That(front.x * house.localPosition.x, Is.LessThan(0), "Verandas face the central road.");
                Assert.That(Mathf.Abs(house.localPosition.x), Is.GreaterThan(15));
            }
        }

        [Test]
        public void ReapplyingTheDesignPreservesExistingCountrySideEditsAndDoesNotDuplicateAnything()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                GameObject environment = scene.GetRootGameObjects().Single(root => root.name == CountrySideSceneBuilder.RootName);
                Transform house = environment.transform.Find("Countryside Route Tile 01/Village - original Japanese minka and kura").GetChild(0);
                house.localPosition += Vector3.right * 0.75f;
                Vector3 authored = house.localPosition;
                int count = environment.GetComponentsInChildren<Transform>(true).Length;
                CountrySideSceneBuilder.ApplyToScene(scene);
                Assert.That(house.localPosition, Is.EqualTo(authored));
                Assert.That(environment.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(scene.GetRootGameObjects().Count(root => root.name == CountrySideSceneBuilder.RootName), Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void CountrysideBuilderRefusesTheExistingLevelScene()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => CountrySideSceneBuilder.ApplyToScene(scene));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
