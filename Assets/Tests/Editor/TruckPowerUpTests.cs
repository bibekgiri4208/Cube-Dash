using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class TruckPowerUpTests
    {
        [Test]
        public void TruckModelAndFragmentPoolAreAuthoredInTheLevel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Truck.prefab");
            Assert.That(prefab, Is.Not.Null);
            Mesh body = prefab.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(body.subMeshCount, Is.EqualTo(8));
            Assert.That(body.vertexCount, Is.InRange(2000, 12000));
            Assert.That(body.bounds.max.y, Is.GreaterThan(4), "Twin stacks extend above the cab.");
            Assert.That(prefab.transform.childCount, Is.EqualTo(6));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                    Assert.That(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                        vertices[triangles[i + 2]] - vertices[triangles[i]]).sqrMagnitude, Is.GreaterThan(1e-12f));
            }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                foreach (GameObject root in scene.GetRootGameObjects()) if (root.TryGetComponent(out CubeDashGame candidate)) game = candidate;
                Assert.That(game.TruckPresentation, Is.SameAs(game.Player.GetComponent<PlayerTruck>()));
                Assert.That(game.TruckPresentation.Truck.gameObject.activeSelf, Is.False);
                Assert.That(game.TruckPresentation.Wheels.Length, Is.EqualTo(6));
                Assert.That(new SerializedObject(game).FindProperty("truckDuration").floatValue, Is.EqualTo(10));
                Assert.That(new SerializedObject(game).FindProperty("truckShieldDuration").floatValue, Is.EqualTo(3));
                Assert.That(game.Track.Debris.Capacity, Is.EqualTo(128));
                Assert.That(game.Track.Debris.ActiveCount, Is.Zero);
                foreach (TrackSegment segment in game.Track.Segments)
                    foreach (PowerUpPickup pickup in segment.PowerUps) Assert.That(pickup.transform.Find("Truck Icon"), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator TruckPickupSmashesAllColorsAndPausesWheelsDebrisAndTimer()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game);
            PowerUpPickup pickup = game.Track.Segments[1].PowerUps[0];
            pickup.Configure(PowerUpType.Truck, game.Track.PowerUpMaterial(PowerUpType.Truck));
            Assert.That(pickup.transform.Find("Truck Icon").gameObject.activeSelf, Is.True);
            Assert.That(pickup.transform.Find("Gem").gameObject.activeSelf, Is.False);
            Assert.That(pickup.transform.Find("Fighter Icon").gameObject.activeSelf, Is.False);
            pickup.transform.position = new Vector3(0, 1.35f, 0.5f); pickup.gameObject.SetActive(true);
            yield return null; yield return null;
            Assert.That(game.Trucking, Is.True);
            Assert.That(game.TruckPresentation.Truck.gameObject.activeSelf, Is.True);
            Assert.That(game.Player.Find("Cube Visual").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(game.Player.GetComponent<BoxCollider>().enabled, Is.True);
            Clear(game);
            RunnerCube wrong = Place(game, 0, CubeColor.Blue, 1.4f);
            yield return null; yield return null;
            Assert.That(wrong.gameObject.activeSelf, Is.False);
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            Assert.That(game.Track.Debris.ActiveCount, Is.GreaterThan(0));
            Assert.That(game.Score, Is.Zero, "Only matching cubes award points, even when smashing.");
            game.TogglePause();
            float timer = game.TruckRemaining;
            Quaternion wheel = game.TruckPresentation.Wheels[0].localRotation;
            Transform fragment = game.Track.Debris.transform.GetChild(0);
            Vector3 position = fragment.position;
            yield return null; yield return null;
            Assert.That(game.TruckRemaining, Is.EqualTo(timer));
            Assert.That(game.TruckPresentation.Wheels[0].localRotation, Is.EqualTo(wheel));
            Assert.That(fragment.position, Is.EqualTo(position));
            game.TogglePause(); Clear(game);
            game.ChangeLane(1);
            for (float t = 0; t < 0.3f; t += Time.deltaTime) yield return null;
            Assert.That(game.Player.position.x, Is.GreaterThan(1));
            Assert.That(game.TruckPresentation.Wheels[0].localRotation, Is.Not.EqualTo(wheel));
            game.StartRun();
            Assert.That(game.Trucking, Is.False);
            Assert.That(game.Track.Debris.ActiveCount, Is.Zero);
            Assert.That(game.TruckPresentation.Truck.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ExpirationGrantsThreeSecondMultiHitShieldWithoutSpendingCharge()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game);
            Apply(game, PowerUpType.Shield); Apply(game, PowerUpType.Truck);
            game.enabled = false;
            TickTruck(game, game.TruckRemaining - 0.01f);
            Assert.That(game.Trucking, Is.True);
            TickTruck(game, 0.02f);
            Assert.That(game.Trucking, Is.False);
            Assert.That(game.LandingShieldRemaining, Is.EqualTo(3));
            Assert.That(game.Player.Find("Cube Visual").GetComponent<Renderer>().enabled, Is.True);
            game.FlightPresentation.Tick(0.1f, 0, true);
            Assert.That(game.FlightPresentation.ShieldVisible, Is.True);
            Place(game, 0, CubeColor.Blue, 2); Place(game, 1, CubeColor.Green, 4);
            Assert.That(game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, true, false, true,
                out int collected, out bool used), Is.False);
            Assert.That(used, Is.False); Assert.That(collected, Is.Zero); Assert.That(game.Shields, Is.EqualTo(1));
            TickFlight(game, 2.9f); Assert.That(game.LandingShieldRemaining, Is.GreaterThan(0));
            TickFlight(game, 0.2f); Assert.That(game.LandingShieldRemaining, Is.Zero);
            game.StartRun();
            Assert.That(game.LandingShieldRemaining, Is.Zero);
            Assert.That(game.FlightPresentation.ShieldVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator SweptTruckActivationProtectsLaterHitsNotEarlierOnesAndFragmentsStayBounded()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            Clear(game);
            PowerUpPickup pickup = game.Track.Segments[1].PowerUps[0];
            pickup.Configure(PowerUpType.Truck, game.Track.PowerUpMaterial(PowerUpType.Truck));
            pickup.transform.position = new Vector3(0, 1.35f, 3); pickup.gameObject.SetActive(true);
            RunnerCube wrong = Place(game, 0, CubeColor.Blue, 6);
            RunnerCube match = Place(game, 1, CubeColor.Red, 8);
            Assert.That(game.Track.Advance(10, 0, 0, 0, Vector2.one * 0.575f, 0, out int collected), Is.False);
            Assert.That(collected, Is.EqualTo(1));
            Assert.That(wrong.gameObject.activeSelf || match.gameObject.activeSelf, Is.False);
            Assert.That(game.Track.LastPowerUps, Does.Contain(PowerUpType.Truck));
            Assert.That(game.Track.Debris.ActiveCount, Is.EqualTo(16));
            Clear(game);
            Place(game, 0, CubeColor.Blue, 2);
            pickup.transform.position = new Vector3(0, 1.35f, 8); pickup.gameObject.SetActive(true);
            Assert.That(game.Track.Advance(10, 0, 0, 0, Vector2.one * 0.575f, 0, out collected), Is.True);
            Assert.That(game.Track.LastPowerUps, Is.Empty);
            int objects = game.Track.Debris.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 100; i++) game.Track.Debris.Shatter(wrong, 0);
            Assert.That(game.Track.Debris.ActiveCount, Is.EqualTo(game.Track.Debris.Capacity));
            Assert.That(game.Track.Debris.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
            game.Track.Debris.Tick(2, 0); Assert.That(game.Track.Debris.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RepeatedPickupRefreshesTruckAndFlightIsBlockedUntilTruckExpires()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game); game.enabled = false;
            Apply(game, PowerUpType.Truck); TickTruck(game, 6); Apply(game, PowerUpType.Truck);
            Assert.That(game.TruckRemaining, Is.EqualTo(10));
            TickTruck(game, 2);
            Apply(game, PowerUpType.FighterPlane);
            Assert.That(game.TruckRemaining, Is.EqualTo(8));
            Assert.That(game.Trucking, Is.True); Assert.That(game.Flying, Is.False);
            Assert.That(game.TruckPresentation.Truck.gameObject.activeSelf, Is.True);
            Assert.That(game.FlightPresentation.Fighter.gameObject.activeSelf, Is.False);
            Apply(game, PowerUpType.Shield); Apply(game, PowerUpType.DoublePoints);
            Assert.That(game.Shields, Is.EqualTo(1)); Assert.That(game.DoublePointsActive, Is.True);
            TickTruck(game, 8);
            Apply(game, PowerUpType.FighterPlane);
            Assert.That(game.Trucking, Is.False); Assert.That(game.Flying, Is.True);
            Assert.That(game.TruckPresentation.Truck.gameObject.activeSelf, Is.False);
            Apply(game, PowerUpType.Truck);
            Assert.That(game.Trucking, Is.True); Assert.That(game.Flying, Is.False);
            Assert.That(game.FlightPresentation.Fighter.gameObject.activeSelf, Is.False);
            Assert.That(game.Player.GetComponent<BoxCollider>().enabled, Is.True);
            game.StartRun();
            Assert.That(game.Trucking || game.Flying, Is.False);
        }

        [UnityTest]
        public IEnumerator TruckSkipsPlaneContactsWithoutSkippingLaterCubesOrBonuses()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game); game.enabled = false;
            Apply(game, PowerUpType.Truck);
            PowerUpPickup plane = PlacePickup(game, 0, PowerUpType.FighterPlane, 2);
            PlacePickup(game, 1, PowerUpType.DoublePoints, 5);
            RunnerCube wrong = Place(game, 0, CubeColor.Blue, 6);
            RunnerCube match = Place(game, 1, CubeColor.Red, 8);
            Assert.That(game.Track.Advance(10, 0, 0, 0, game.TruckPresentation.ContactHalfSize, 0,
                false, false, false, true, out int collected, out bool used), Is.False);
            Assert.That(collected, Is.EqualTo(1)); Assert.That(used, Is.False);
            Assert.That(plane.gameObject.activeSelf, Is.True, "Blocked airplanes must not be consumed.");
            Assert.That(game.Track.LastPowerUps, Is.EqualTo(new[] { PowerUpType.DoublePoints }));
            Assert.That(wrong.gameObject.activeSelf || match.gameObject.activeSelf, Is.False);
            Assert.That(game.Track.Debris.ActiveCount, Is.EqualTo(16));

            // A newly collected truck also blocks a later or simultaneous plane within the same sweep.
            foreach (float planeZ in new[] { 5f, 2f })
            {
                game.StartRun(); Clear(game);
                PlacePickup(game, 0, PowerUpType.Truck, 2);
                plane = PlacePickup(game, 1, PowerUpType.FighterPlane, planeZ);
                PlacePickup(game, 2, PowerUpType.Shield, 6);
                match = Place(game, 0, CubeColor.Red, 8);
                Assert.That(game.Track.Advance(10, 0, 0, 0, Vector2.one * 0.575f, 0, out collected), Is.False);
                Assert.That(collected, Is.EqualTo(1));
                Assert.That(plane.gameObject.activeSelf, Is.True);
                Assert.That(game.Track.LastPowerUps, Is.EqualTo(new[] { PowerUpType.Truck, PowerUpType.Shield }));
                Assert.That(match.gameObject.activeSelf, Is.False);
            }

            // Flight still wins when the airplane is reached before the truck.
            game.StartRun(); Clear(game);
            PlacePickup(game, 0, PowerUpType.FighterPlane, 2);
            PowerUpPickup truck = PlacePickup(game, 1, PowerUpType.Truck, 5);
            match = Place(game, 0, CubeColor.Red, 8);
            Assert.That(game.Track.Advance(10, 0, 0, 0, Vector2.one * 0.575f, 0, out collected), Is.False);
            Assert.That(collected, Is.Zero);
            Assert.That(game.Track.LastPowerUps, Is.EqualTo(new[] { PowerUpType.FighterPlane }));
            Assert.That(truck.gameObject.activeSelf && match.gameObject.activeSelf, Is.True);
        }

        private static PowerUpPickup PlacePickup(CubeDashGame game, int slot, PowerUpType type, float z)
        {
            PowerUpPickup pickup = game.Track.Segments[1].PowerUps[slot];
            pickup.Configure(type, game.Track.PowerUpMaterial(type));
            pickup.transform.position = new Vector3(0, 1.35f, z); pickup.gameObject.SetActive(true); return pickup;
        }

        private static void Apply(CubeDashGame game, PowerUpType type)
            => typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { type });
        private static void TickTruck(CubeDashGame game, float dt)
            => typeof(CubeDashGame).GetMethod("UpdateTruckTimers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { dt });
        private static void TickFlight(CubeDashGame game, float dt)
            => typeof(CubeDashGame).GetMethod("UpdateFlightTimers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { dt });
        private static void Clear(CubeDashGame game)
        {
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
        }
        private static RunnerCube Place(CubeDashGame game, int slot, CubeColor color, float z)
        {
            RunnerCube cube = game.Track.Segments[1].Cubes[slot];
            cube.Configure(color, game.Track.ColorMaterial(color));
            cube.transform.position = new Vector3(0, 0.575f, z); cube.gameObject.SetActive(true); return cube;
        }
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
