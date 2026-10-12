using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeDash.Tests
{
    public sealed class MainMenuAssetStoreGroundCoverTests
    {
        private const string Art = "Assets/MainMenu/JapaneseGarden/Prefabs/";

        [Test]
        public void PackageBushesAndGrassReplaceEveryRoundShrubAndGrassMeshIncludingPlanterTufts()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Transform garden = menu.transform.Find("Parking Lot Diorama/Japanese Garden Foliage");
                Transform plants = garden.Find("Idyllic Fantasy Nature Bushes and Grass");
                Assert.That(plants, Is.Not.Null);
                var originals = garden.GetComponentsInChildren<MeshRenderer>().Where(renderer =>
                    PrefabUtility.IsAnyPrefabInstanceRoot(renderer.gameObject) && IsOldPlant(renderer.gameObject)).ToArray();
                int bushes = originals.Count(renderer => Path(renderer.gameObject) == Art + "Azalea Shrub.prefab");
                Assert.That(bushes, Is.GreaterThanOrEqualTo(40)); Assert.That(originals.Length - bushes, Is.GreaterThan(40));
                Assert.That(originals.All(renderer => !renderer.enabled), Is.True, "Do not leave any round shrub or procedural grass visible.");
                Assert.That(originals.All(renderer => renderer.gameObject.activeInHierarchy), Is.True, "Preserve their transforms and user-added children as backups.");
                Assert.That(plants.childCount, Is.GreaterThan(0), "Plant instances may be removed or duplicated when editing the garden layout.");
                var paths = plants.Cast<Transform>().Select(plant => Path(plant.gameObject)).ToArray();
                Assert.That(paths.All(path => path.StartsWith("Assets/Idyllic Fantasy Nature/Prefabs/")), Is.True);
                Assert.That(paths.Any(path => path.Contains("Bush_")), Is.True);
                Assert.That(paths.Any(path => path.Contains("Grass_")), Is.True);
                Assert.That(paths.Where(path => path.Contains("Bush_")).Distinct().Count(), Is.EqualTo(6));
                Assert.That(paths.Where(path => path.Contains("Grass_")).Distinct().Count(), Is.EqualTo(3));
                Assert.That(garden.GetComponentsInChildren<MeshRenderer>().Count(renderer => renderer.name == "Low stone planter rim" && renderer.enabled), Is.EqualTo(4));
                Assert.That(garden.GetComponentsInChildren<MeshRenderer>().Count(renderer => renderer.name == "Sakura planting soil" && renderer.enabled), Is.EqualTo(1));
                Assert.That(garden.Find("Idyllic Fantasy Nature Trees").childCount, Is.GreaterThanOrEqualTo(3));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void PlantInstancesKeepPackageMaterialsAndBushLodsWithoutDemoInteractions()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Transform plants = menu.transform.Find("Parking Lot Diorama/Japanese Garden Foliage/Idyllic Fantasy Nature Bushes and Grass");
                Assert.That(plants, Is.Not.Null);
                Assert.That(plants.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                Assert.That(plants.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(plants.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
                foreach (Transform plant in plants)
                {
                    Assert.That(plant.localScale.x, Is.GreaterThan(0));
                    if (Path(plant.gameObject).Contains("Bush_"))
                    {
                        LODGroup lod = plant.GetComponent<LODGroup>();
                        Assert.That(lod, Is.Not.Null); Assert.That(lod.enabled, Is.True);
                        Assert.That(lod.GetLODs(), Has.Length.EqualTo(3));
                        foreach (LOD level in lod.GetLODs())
                            Assert.That(level.renderers.Length, Is.EqualTo(1));
                    }
                    foreach (MeshRenderer renderer in plant.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                        Assert.That(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject), Is.EqualTo((StaticEditorFlags)0));
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            Assert.That(material, Is.Not.Null);
                            Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/Idyllic Fantasy Nature/Materials/"));
                            Assert.That(material.shader.isSupported, Is.True); Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                        }
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static string Path(GameObject plant) => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(plant);
        private static bool IsOldPlant(GameObject plant) => Path(plant) == Art + "Azalea Shrub.prefab" ||
            Path(plant) == Art + "Susuki Grass.prefab" || Path(plant) == Art + "Roadside Grass.prefab";
    }
}
