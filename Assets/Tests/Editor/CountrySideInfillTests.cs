using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeDash.Tests
{
    public sealed class CountrySideInfillTests
    {
        [Test]
        public void BothRouteTilesHaveSavedHamletsCropsOrchardsAndDenseMeadowMargins()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>()).Single();
                foreach (Transform tile in scenery.Tiles)
                {
                    Transform infill = tile.Find(CountrySideInfillBuilder.RootName);
                    Assert.That(infill, Is.Not.Null);
                    Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(infill.gameObject), Is.EqualTo(CountrySideInfillBuilder.PrefabPath));
                    Transform hamlets = infill.Find("Hamlets - roadside homes and outbuildings");
                    Assert.That(hamlets.childCount, Is.EqualTo(12));
                    Assert.That(infill.Find("Kitchen gardens and cultivated field strips").childCount, Is.GreaterThan(5));
                    Assert.That(infill.Find("Orchards and farm windbreaks").childCount, Is.GreaterThan(15));
                    Assert.That(infill.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(infill.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                    foreach (var renderer in infill.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Assert.That(renderer.gameObject.isStatic, Is.False);
                        Assert.That(renderer.sharedMaterials.All(material => material != null && !ShaderUtil.ShaderHasError(material.shader)), Is.True, renderer.name);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void NewLandmarksFillBothSidesIncludingThePreviouslyEmptyTileEndsWithoutEnteringTheRoad()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CountrySideInfillBuilder.PrefabPath);
            Transform homes = prefab.transform.Find("Hamlets - roadside homes and outbuildings");
            Assert.That(homes.Cast<Transform>().Count(home => home.localPosition.x < 0), Is.EqualTo(6));
            Assert.That(homes.Cast<Transform>().Count(home => home.localPosition.x > 0), Is.EqualTo(6));
            Assert.That(homes.Cast<Transform>().Count(home => home.localPosition.z > 210), Is.EqualTo(4));
            foreach (Transform home in homes) Assert.That(Mathf.Abs(home.localPosition.x), Is.GreaterThan(14));
            foreach (string name in new[] { "Valley Undergrowth", "Valley Village Props", "Village Crop Beds", "Village Crops" })
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CountrySideSceneBuilder.ArtPath + "Meshes/" + name + ".asset");
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertices.All(point => Mathf.Abs(point.x) > 5.6f), Is.True, name + " must leave the road and guardrails clear.");
            }
            Mesh grass = AssetDatabase.LoadAssetAtPath<Mesh>(CountrySideSceneBuilder.ArtPath + "Meshes/Valley Undergrowth.asset");
            Assert.That(grass.vertexCount, Is.GreaterThan(50000), "Saved batched ground cover, not a few isolated tufts.");
            Assert.That(grass.bounds.min.z, Is.LessThan(-34)); Assert.That(grass.bounds.max.z, Is.GreaterThan(245));
        }

        [Test]
        public void NativePlantAndRigidSurfaceCurveListsIncludeTheInfillAndReapplyingPreservesEdits()
        {
            var scene = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>()).Single();
                Transform infill = scenery.Tiles[0].Find(CountrySideInfillBuilder.RootName);
                Transform plant = infill.Find("Orchards and farm windbreaks").GetChild(0);
                Assert.That(scenery.CurvePlants.Contains(plant), Is.True);
                Assert.That(scenery.CurvedSurfaces.Any(renderer => renderer.transform == infill.Find("Continuous meadow undergrowth")), Is.True);
                plant.localPosition += Vector3.right; Vector3 position = plant.localPosition;
                int count = scenery.Tiles.Sum(tile => tile.GetComponentsInChildren<Transform>(true).Length);
                CountrySideInfillBuilder.ApplyToScene(scene);
                Assert.That(plant.localPosition, Is.EqualTo(position));
                Assert.That(scenery.Tiles.Sum(tile => tile.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(count));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var other = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try { Assert.Throws<System.InvalidOperationException>(() => CountrySideInfillBuilder.ApplyToScene(other)); }
            finally { EditorSceneManager.ClosePreviewScene(other); }
        }
    }
}
