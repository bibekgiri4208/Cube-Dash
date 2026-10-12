using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeDash.Tests
{
    public sealed class MainMenuJapaneseGardenTests
    {
        private const string Art = "Assets/MainMenu/JapaneseGarden/";

        [TestCase("Sakura Tree", 7, 70000)]
        [TestCase("Japanese Pine", 4, 10000)]
        [TestCase("Azalea Shrub", 6, 1500)]
        [TestCase("Susuki Grass", 4, 1000)]
        [TestCase("Roadside Grass", 4, 350)]
        [TestCase("Fallen Sakura Petals", 2, 600)]
        public void BotanicalPrefabsContainDetailedSavedGeometryAndNoRuntimeGenerators(string name, int slots, int minimumVertices)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Prefabs/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            Mesh mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo(Art + "Meshes/" + name + ".asset"));
            Assert.That(mesh.vertexCount, Is.InRange(minimumVertices, 160000));
            Assert.That(mesh.subMeshCount, Is.EqualTo(slots));
            Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
            foreach (Vector3 point in mesh.vertices)
                Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y) ||
                    float.IsNaN(point.z) || float.IsInfinity(point.z), Is.False);
            foreach (int index in mesh.triangles) Assert.That(index, Is.InRange(0, mesh.vertexCount - 1));
            foreach (Vector3 normal in mesh.normals) Assert.That(normal.sqrMagnitude, Is.EqualTo(1).Within(0.001f));
            var renderer = prefab.GetComponent<MeshRenderer>();
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(slots));
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null); Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                Assert.That(material.GetFloat("_Cull"), Is.Zero, "Petals, needles and folded leaf blades must be visible from both sides.");
                Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.False, "Foliage must not wash out the bright menu through extra emission.");
            }
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(1), "Each model is a shared combined mesh, not hundreds of runtime objects.");
        }

        [Test]
        public void MenuHasThreeSakuraPinesAzaleasAndGrassWhileOriginalFoliageRemainsRecoverable()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Transform world = menu.transform.Find("Parking Lot Diorama");
                Transform garden = world.Find("Japanese Garden Foliage");
                Assert.That(garden, Is.Not.Null); Assert.That(garden.gameObject.activeSelf, Is.True);
                MeshFilter[] models = garden.GetComponentsInChildren<MeshFilter>();
                Assert.That(models.Count(model => model.sharedMesh == Mesh("Sakura Tree")), Is.EqualTo(3));
                Assert.That(models.Count(model => model.sharedMesh == Mesh("Japanese Pine")), Is.EqualTo(11));
                Assert.That(models.Count(model => model.sharedMesh == Mesh("Azalea Shrub")), Is.GreaterThanOrEqualTo(40));
                Assert.That(models.Count(model => model.sharedMesh == Mesh("Susuki Grass")), Is.GreaterThanOrEqualTo(15));
                Assert.That(models.Count(model => model.sharedMesh == Mesh("Roadside Grass")), Is.GreaterThan(20));
                Assert.That(models.Count(model => model.sharedMesh == Mesh("Fallen Sakura Petals")), Is.EqualTo(3));
                // Imported bushes and trees each save three LOD renderers; only one level renders at a time.
                Assert.That(garden.GetComponentsInChildren<MeshRenderer>().Count(renderer => renderer.enabled), Is.LessThan(400));
                Assert.That(garden.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                Assert.That(garden.GetComponentsInChildren<Collider>(true), Is.Empty);
                var backups = world.GetComponentsInChildren<Transform>(true).Where(child => child.name == "Layered low-poly evergreen" ||
                    child.name == "Faceted leafy shrub" || child.name == "Asphalt crack weed").ToArray();
                Assert.That(backups.Length, Is.GreaterThan(80));
                Assert.That(backups.All(child => !child.gameObject.activeSelf), Is.True, "Original assets and transforms are saved, but must not overlap the improved models.");
                Assert.That(menu.Hero.GetComponent<PlayerCubeVisual>(), Is.Not.Null);
                Assert.That(menu.GetComponentsInChildren<MenuRibbonGraphic>(true).Length, Is.EqualTo(4));
                Assert.That(world.Find("Concrete wall, graffiti and warm sconces"), Is.Not.Null);
                Assert.That(world.Find("Parking-lot street props"), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SakuraHasBroadAsymmetricCrownAndGroundedTrunkRatherThanAColoredPrimitive()
        {
            Mesh sakura = Mesh("Sakura Tree");
            Assert.That(sakura.bounds.size.x, Is.GreaterThan(5)); Assert.That(sakura.bounds.size.z, Is.GreaterThan(5));
            Assert.That(sakura.bounds.max.y, Is.InRange(6, 9)); Assert.That(sakura.bounds.min.y, Is.LessThan(0.2f));
            Assert.That(sakura.GetIndexCount(0), Is.GreaterThan(3000), "Saved branching trunk and twigs.");
            Assert.That(sakura.GetIndexCount(4), Is.GreaterThan(100000), "Hundreds of real five-petal flowers, not just pink foliage spheres.");
            Assert.That(sakura.GetIndexCount(6), Is.GreaterThan(10000), "Separate modeled flower centers.");
            Assert.That(Mesh("Susuki Grass").bounds.max.y, Is.GreaterThan(1), "Fine ornamental seed heads extend above the curved blades.");
        }

        private static Mesh Mesh(string name) => AssetDatabase.LoadAssetAtPath<Mesh>(Art + "Meshes/" + name + ".asset");
    }
}
