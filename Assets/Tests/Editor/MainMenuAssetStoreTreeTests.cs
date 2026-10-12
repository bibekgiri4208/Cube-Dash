using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeDash.Tests
{
    public sealed class MainMenuAssetStoreTreeTests
    {
        private const string Prefabs = "Assets/Idyllic Fantasy Nature/Prefabs/";

        [Test]
        public void MenuUsesFourteenLinkedPackageTreesWithThreeBlossomVariantsAndWorkingLods()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Transform trees = menu.transform.Find("Parking Lot Diorama/Japanese Garden Foliage/Idyllic Fantasy Nature Trees");
                Assert.That(trees, Is.Not.Null); Assert.That(trees.childCount, Is.EqualTo(14));
                string[] paths = trees.Cast<Transform>().Select(tree => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree.gameObject)).ToArray();
                Assert.That(paths.All(path => path.StartsWith(Prefabs)), Is.True);
                Assert.That(paths.Count(path => path.Contains("BlossomTree_")), Is.EqualTo(3));
                Assert.That(paths.Count(path => path.Contains("BroadleafTree_")), Is.EqualTo(7));
                Assert.That(paths.Count(path => path.Contains("Fir_")), Is.EqualTo(4));
                Assert.That(paths.Where(path => path.Contains("BlossomTree_")).Distinct().Count(), Is.EqualTo(3));
                foreach (Transform tree in trees)
                {
                    LODGroup lod = tree.GetComponent<LODGroup>(); Assert.That(lod, Is.Not.Null); Assert.That(lod.enabled, Is.True);
                    Assert.That(lod.GetLODs(), Has.Length.EqualTo(3));
                    foreach (LOD level in lod.GetLODs())
                    {
                        Assert.That(level.renderers, Is.Not.Empty);
                        foreach (Renderer renderer in level.renderers)
                        {
                            Assert.That(renderer, Is.Not.Null);
                            foreach (Material material in renderer.sharedMaterials)
                            {
                                Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/Idyllic Fantasy Nature/Materials/"));
                                Assert.That(material.shader.isSupported, Is.True); Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                            }
                        }
                    }
                    Assert.That(tree.localScale.x, Is.GreaterThan(0));
                    Assert.That(tree.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(tree.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, "Do not add demo player, camera, wind or interaction controllers to the menu.");
                    Assert.That(GameObjectUtility.GetStaticEditorFlags(tree.gameObject), Is.EqualTo((StaticEditorFlags)0));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void OldTreeMeshesAreHiddenButTheirPlantersAndPetalsStayVisible()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Transform garden = menu.transform.Find("Parking Lot Diorama/Japanese Garden Foliage");
                var originals = garden.GetComponentsInChildren<MeshRenderer>().Where(renderer =>
                    PrefabUtility.IsAnyPrefabInstanceRoot(renderer.gameObject) &&
                    (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject).EndsWith("/Japanese Pine.prefab") ||
                     PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject).EndsWith("/Sakura Tree.prefab"))).ToArray();
                Assert.That(originals.Length, Is.EqualTo(14)); Assert.That(originals.All(renderer => !renderer.enabled), Is.True);
                Assert.That(originals.All(renderer => renderer.gameObject.activeInHierarchy), Is.True, "Preserve the original roots and any user-added children.");
                Assert.That(garden.GetComponentsInChildren<MeshRenderer>().Count(renderer => renderer.name == "Low stone planter rim" && renderer.enabled), Is.EqualTo(4));
                Assert.That(garden.GetComponentsInChildren<MeshRenderer>().Count(renderer => renderer.name == "Sakura planting soil" && renderer.enabled), Is.EqualTo(1));
                Assert.That(garden.GetComponentsInChildren<MeshRenderer>().Count(renderer =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject).EndsWith("/Fallen Sakura Petals.prefab") && renderer.enabled), Is.EqualTo(3));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
