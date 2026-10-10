using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDash.Tests
{
    public sealed class BiomeBoundaryTests
    {
        [Test]
        public void EveryJoinUsesPairedGroundSettingsAndMixedSceneryAndResetsOnRecycle()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab");
            GameObject a = Object.Instantiate(prefab), b = Object.Instantiate(prefab);
            try
            {
                var outgoing = a.GetComponent<SegmentEnvironment>(); var incoming = b.GetComponent<SegmentEnvironment>();
                var blendA = a.GetComponent<BiomeBoundaryBlend>(); var blendB = b.GetComponent<BiomeBoundaryBlend>();
                Assert.That(blendA, Is.Not.Null); Assert.That(blendA.Regions.Length, Is.EqualTo(5));
                int count = a.GetComponentsInChildren<Transform>(true).Length;
                for (int join = 1; join <= 10; join++)
                {
                    outgoing.Configure(join * 8 - 1, 8); incoming.Configure(join * 8, 8);
                    Assert.That(blendA.Exit, Is.True); Assert.That(blendB.Entrance, Is.True);
                    var from = blendA.Regions[(join - 1) % 5]; var to = blendB.Regions[join % 5];
                    Assert.That(from.ExitDressing.activeSelf, Is.True); Assert.That(to.EntranceDressing.activeSelf, Is.True);
                    Assert.That(from.ExitDressing.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(100));
                    Assert.That(to.EntranceDressing.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(100));
                    foreach (Renderer surface in from.Ground)
                    {
                        var block = new MaterialPropertyBlock(); surface.GetPropertyBlock(block);
                        Vector4 boundary = block.GetVector("_BiomeBoundary");
                        Assert.That(boundary.x, Is.EqualTo(1)); Assert.That(boundary.z, Is.EqualTo(1));
                        float offset = a.transform.InverseTransformPoint(surface.transform.position).z;
                        Assert.That(boundary.y + offset, Is.EqualTo(42).Within(0.001f));
                        Assert.That(Vector4.Distance(block.GetColor("_NeighborColor"), to.Floor.GetColor("_BaseColor")), Is.LessThan(0.00001f));
                    }
                    foreach (Renderer surface in to.Ground)
                    {
                        var block = new MaterialPropertyBlock(); surface.GetPropertyBlock(block);
                        Vector4 boundary = block.GetVector("_BiomeBoundary");
                        Assert.That(boundary.x, Is.EqualTo(1)); Assert.That(boundary.z, Is.EqualTo(-1));
                        float offset = b.transform.InverseTransformPoint(surface.transform.position).z;
                        Assert.That(boundary.y + offset, Is.EqualTo(0).Within(0.001f));
                        Assert.That(Vector4.Distance(block.GetColor("_NeighborColor"), from.Floor.GetColor("_BaseColor")), Is.LessThan(0.00001f));
                    }
                }
                outgoing.Configure(11, 8);
                Assert.That(blendA.Entrance || blendA.Exit, Is.False);
                foreach (var region in blendA.Regions)
                {
                    Assert.That(region.EntranceDressing.activeSelf || region.ExitDressing.activeSelf, Is.False);
                    for (int i = 0; i < region.Ground.Length; i++)
                    {
                        var block = new MaterialPropertyBlock(); region.Ground[i].GetPropertyBlock(block);
                        Assert.That(block.GetVector("_BiomeBoundary").x, Is.Zero);
                        Assert.That(region.Ground[i].GetComponent<MeshFilter>().sharedMesh, Is.SameAs(region.NormalGround[i]));
                        Assert.That(region.Ground[i].sharedMaterial.GetVector("_BiomeBoundary").x, Is.Zero, "Do not edit shared materials.");
                    }
                }
                Assert.That(a.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                outgoing.Configure(0, 8); Assert.That(blendA.Entrance, Is.False);
                outgoing.Configure(-1, 8); Assert.That(blendA.Exit, Is.False);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void RiverEndsTaperAtBothBoundariesAndTransitionPropsKeepTheRoadClear()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                var environment = root.GetComponent<SegmentEnvironment>(); var blend = root.GetComponent<BiomeBoundaryBlend>();
                foreach (int section in new[] { 8, 15, 11 })
                {
                    environment.Configure(section, 8);
                    foreach (Renderer river in blend.Regions[1].Water)
                    {
                        var block = new MaterialPropertyBlock(); river.GetPropertyBlock(block);
                        Vector4 ends = block.GetVector("_RiverEnds");
                        Assert.That(ends.x, Is.EqualTo(section == 8 ? 1 : 0));
                        Assert.That(ends.y, Is.EqualTo(section == 15 ? 1 : 0));
                    }
                }
                foreach (var region in blend.Regions)
                    foreach (GameObject dressing in new[] { region.EntranceDressing, region.ExitDressing })
                    {
                        Assert.That(dressing.GetComponentsInChildren<Collider>(true), Is.Empty);
                        Mesh mesh = dressing.GetComponent<MeshFilter>().sharedMesh; Vector3[] vertices = mesh.vertices;
                        foreach (int index in mesh.triangles) Assert.That(Mathf.Abs(vertices[index].x), Is.GreaterThan(5));
                        Assert.That(mesh.subMeshCount, Is.EqualTo(dressing.GetComponent<Renderer>().sharedMaterials.Length));
                    }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CityOutskirtsStepDownAndRestoreOriginalBuildingsAfterRecycling()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                var environment = root.GetComponent<SegmentEnvironment>();
                Transform city = environment.Biomes[0].transform;
                var originals = new System.Collections.Generic.Dictionary<Transform, Vector3>();
                foreach (Transform child in city) if (child.name.Contains("Skyscraper")) originals[child] = child.localScale;
                Assert.That(originals.Count, Is.GreaterThan(0));
                environment.Configure(7, 8);
                int smaller = 0, hidden = 0;
                foreach (var pair in originals)
                {
                    if (pair.Key.localScale.y < pair.Value.y) smaller++;
                    if (!pair.Key.gameObject.activeSelf) hidden++;
                    Assert.That(pair.Key.localPosition.y, Is.EqualTo(-9));
                }
                Assert.That(smaller, Is.GreaterThan(0)); Assert.That(hidden, Is.GreaterThan(0));
                environment.Configure(3, 8);
                foreach (var pair in originals)
                {
                    Assert.That(pair.Key.localScale, Is.EqualTo(pair.Value));
                    Assert.That(pair.Key.gameObject.activeSelf, Is.True);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavedLevelInheritsAllFiveTransitions()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                EndlessTrack track = null;
                foreach (GameObject root in scene.GetRootGameObjects()) if ((track = root.GetComponentInChildren<EndlessTrack>(true)) != null) break;
                Assert.That(track, Is.Not.Null);
                foreach (TrackSegment segment in track.Segments)
                    Assert.That(segment.GetComponent<BiomeBoundaryBlend>().Regions.Length, Is.EqualTo(5));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
