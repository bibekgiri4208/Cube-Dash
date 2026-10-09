using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class CoinPickupTests
    {
        [Test]
        public void CoinIsAnAuthoredDoubleSidedMeshAndLevelRewardsUseItBeforePlay()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Coin.prefab");
            Assert.That(prefab, Is.Not.Null);
            Mesh mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashCoin.asset")));
            Assert.That(mesh.subMeshCount, Is.EqualTo(3));
            Assert.That(mesh.vertexCount, Is.InRange(1000, 4000));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(mesh.bounds.size.y).Within(0.001f));
            Assert.That(mesh.bounds.size.z, Is.InRange(0.1f, 0.2f), "A coin is a real solid model, not a flat sprite.");
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
                Assert.That(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]).sqrMagnitude, Is.GreaterThan(1e-12f));
            foreach (Material material in prefab.GetComponent<MeshRenderer>().sharedMaterials)
            {
                Assert.That(material.GetColor("_BaseColor").r, Is.GreaterThan(material.GetColor("_BaseColor").b));
                Assert.That(material.enableInstancing, Is.True);
            }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                RunnerHud hud = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out CubeDashGame candidate)) game = candidate;
                    if (root.TryGetComponent(out RunnerHud candidateHud)) hud = candidateHud;
                }
                foreach (TrackSegment segment in game.Track.Segments)
                    foreach (RunnerCube slot in segment.Cubes)
                    {
                        Assert.That(slot.CoinVisual, Is.Not.Null);
                        bool reward = slot.Color == game.PlayerCubeColor;
                        Assert.That(slot.CoinVisible, Is.EqualTo(reward));
                        Assert.That(slot.GetComponent<Renderer>().enabled, Is.EqualTo(!reward));
                        Assert.That(slot.Collider.size, Is.EqualTo(Vector3.one));
                    }
                Assert.That(hud.transform.Find("Safe Area/Menu Overlay/Menu Card/Description").GetComponent<Text>().text,
                    Is.EqualTo("Collect coins. Dodge obstacles."));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator EveryPlayerColorGetsGoldCoinsAndSweptCollectionStillAwardsOnePoint()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.enabled = false;
            int objects = game.Track.GetComponentsInChildren<Transform>(true).Length;
            foreach (CubeColor color in new[] { CubeColor.Red, CubeColor.Blue, CubeColor.Green })
            {
                game.Track.Reset(42, color);
                foreach (TrackSegment segment in game.Track.Segments)
                {
                    foreach (RunnerCube slot in segment.Cubes)
                    {
                        if (slot.gameObject.activeSelf)
                            Assert.That(slot.CoinVisible, Is.EqualTo(slot.Color == color));
                        slot.gameObject.SetActive(false);
                    }
                    foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
                }
                RunnerCube coin = game.Track.Segments[1].Cubes[0];
                coin.Configure(color, game.Track.ColorMaterial(color), true);
                coin.transform.position = new Vector3(0, 0.95f, 2); coin.gameObject.SetActive(true);
                Assert.That(coin.CoinVisible, Is.True);
                Assert.That(game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, out int collected), Is.False);
                Assert.That(collected, Is.EqualTo(1)); Assert.That(coin.gameObject.activeSelf, Is.False);
                game.Track.Advance(0, 0, 0, 0, Vector2.one * 0.575f, 0, out collected);
                Assert.That(collected, Is.Zero, "A consumed coin must not award points twice.");
            }
            Assert.That(game.Track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        }

        [UnityTest]
        public IEnumerator CoinsSpinAndBobWithoutMovingTheirHitboxAndPauseFreezesThem()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube slot in segment.Cubes) slot.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
            RunnerCube coin = game.Track.Segments[1].Cubes[0];
            coin.Configure(CubeColor.Red, game.Track.ColorMaterial(CubeColor.Red), true);
            coin.transform.position = new Vector3(0, 0.95f, 8); coin.gameObject.SetActive(true);
            Physics.SyncTransforms();
            Bounds hitbox = coin.Collider.bounds;
            Quaternion rotation = coin.CoinVisual.localRotation;
            Vector3 home = coin.CoinVisual.localPosition;
            coin.TickCoin(0.2f); Physics.SyncTransforms();
            Assert.That(coin.CoinVisual.localRotation, Is.Not.EqualTo(rotation));
            Assert.That(coin.CoinVisual.localPosition, Is.Not.EqualTo(home));
            Assert.That(coin.Collider.bounds, Is.EqualTo(hitbox));
            game.TogglePause();
            rotation = coin.CoinVisual.localRotation;
            Vector3 position = coin.CoinVisual.localPosition;
            yield return null; yield return null;
            Assert.That(coin.CoinVisual.localRotation, Is.EqualTo(rotation));
            Assert.That(coin.CoinVisual.localPosition, Is.EqualTo(position));
            game.StartRun(); coin.Configure(CubeColor.Red, game.Track.ColorMaterial(CubeColor.Red), true);
            Assert.That(coin.CoinVisual.localPosition, Is.EqualTo(home));
            Assert.That(coin.CoinVisual.localRotation, Is.EqualTo(Quaternion.Euler(0, 25, 0)));
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
