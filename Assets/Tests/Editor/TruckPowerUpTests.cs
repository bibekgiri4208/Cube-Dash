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
            Assert.That(body.subMeshCount, Is.EqualTo(10));
            Assert.That(body.vertexCount, Is.InRange(2000, 12000));
            Assert.That(body.bounds.max.y, Is.GreaterThan(4), "Twin stacks extend above the cab.");
            Assert.That(prefab.transform.childCount, Is.EqualTo(14));
            ParticleSystem[] smoke = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(smoke.Length, Is.EqualTo(8));
            foreach (string side in new[] { "Left", "Right" })
            {
                ParticleSystem system = prefab.transform.Find(side + " Stack Smoke").GetComponent<ParticleSystem>();
                Assert.That(Mathf.Abs(system.transform.localPosition.x), Is.EqualTo(0.96f).Within(0.001f));
                Assert.That(system.transform.localPosition.y, Is.EqualTo(4.28f).Within(0.001f));
                Assert.That(system.transform.localPosition.z, Is.EqualTo(0.14f).Within(0.001f));
                Assert.That(system.main.playOnAwake, Is.False);
                Assert.That(system.main.maxParticles, Is.EqualTo(80));
                Assert.That(system.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(system.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name, Is.EqualTo("CubeDash/Jet Particle"));
                for (int axle = 1; axle <= 3; axle++)
                {
                    ParticleSystem tires = prefab.transform.Find(side + " Tire Smoke " + axle).GetComponent<ParticleSystem>();
                    Assert.That(tires.transform.parent, Is.SameAs(prefab.transform), "Tire smoke must not spin with the wheel.");
                    Assert.That(Mathf.Abs(tires.transform.localPosition.x), Is.EqualTo(1.2f).Within(0.001f));
                    Assert.That(tires.transform.localPosition.y, Is.EqualTo(0.1f).Within(0.001f));
                    Assert.That(tires.transform.localPosition.z,
                        Is.EqualTo(prefab.transform.Find(side + " Wheel " + axle).localPosition.z - 0.22f).Within(0.001f));
                    Assert.That(tires.main.playOnAwake, Is.False);
                    Assert.That(tires.main.maxParticles, Is.EqualTo(48));
                    Assert.That(tires.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                    Assert.That(tires.collision.enabled, Is.False);
                    Assert.That(tires.GetComponent<ParticleSystemRenderer>().sharedMaterial.name, Is.EqualTo("Tire Smoke"));
                }
            }
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Mesh frontWheel = prefab.transform.Find("Left Wheel 1").GetComponent<MeshFilter>().sharedMesh;
            Mesh rearWheel = prefab.transform.Find("Left Wheel 2").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(rearWheel, Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashTruckRearWheel.asset")));
            Assert.That(rearWheel.bounds.size.x, Is.GreaterThan(frontWheel.bounds.size.x * 1.6f), "Rear assemblies carry dual tires.");
            Assert.That(rearWheel.bounds.size.y, Is.EqualTo(frontWheel.bounds.size.y).Within(0.001f));
            Assert.That(prefab.transform.Find("Right Wheel 3").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(rearWheel));
            Assert.That(prefab.GetComponent<MeshRenderer>().sharedMaterials[8].name, Is.EqualTo("Tanks and Mudguards"));
            Assert.That(prefab.GetComponent<MeshRenderer>().sharedMaterials[9].name, Is.EqualTo("Cab Roof"));
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
                Assert.That(game.TruckPresentation.Exhaust.Length, Is.EqualTo(2));
                Assert.That(game.TruckPresentation.TireSmoke.Length, Is.EqualTo(6));
                Assert.That(game.TruckPresentation.Truck.localScale.x, Is.EqualTo(0.48f).Within(0.001f));
                Assert.That(game.TruckPresentation.ContactHalfSize, Is.EqualTo(new Vector2(0.74f, 1.62f)));
                Assert.That(new SerializedObject(game).FindProperty("truckDuration").floatValue, Is.EqualTo(10));
                Assert.That(new SerializedObject(game).FindProperty("truckShieldDuration").floatValue, Is.EqualTo(3));
                Assert.That(game.Track.Debris.Capacity, Is.EqualTo(128));
                Assert.That(game.Track.Debris.ActiveCount, Is.Zero);
                foreach (TrackSegment segment in game.Track.Segments)
                    foreach (PowerUpPickup pickup in segment.PowerUps)
                    {
                        Transform icon = pickup.transform.Find("Truck Icon");
                        Assert.That(icon, Is.Not.Null);
                        foreach (ParticleSystem system in icon.GetComponentsInChildren<ParticleSystem>(true))
                            Assert.That(system.gameObject.activeSelf, Is.False, "Pickup icons never emit stack or tire smoke.");
                    }
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
            float smokeTime = game.TruckPresentation.Exhaust[0].time;
            Vector3 suspensionPosition = game.TruckPresentation.Truck.localPosition;
            Quaternion bodyRotation = game.TruckPresentation.Truck.localRotation;
            Transform fragment = game.Track.Debris.transform.GetChild(0);
            Vector3 position = fragment.position;
            yield return null; yield return null;
            Assert.That(game.TruckRemaining, Is.EqualTo(timer));
            Assert.That(game.TruckPresentation.Wheels[0].localRotation, Is.EqualTo(wheel));
            Assert.That(game.TruckPresentation.Exhaust[0].time, Is.EqualTo(smokeTime));
            Assert.That(game.TruckPresentation.Truck.localPosition, Is.EqualTo(suspensionPosition));
            Assert.That(game.TruckPresentation.Truck.localRotation, Is.EqualTo(bodyRotation));
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
            foreach (ParticleSystem system in game.TruckPresentation.Exhaust) Assert.That(system.particleCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SuspensionSteeringAndStackSmokeAnimateAndResetWithoutCreatingObjects()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game); game.enabled = false;
            PlayerTruck presentation = game.TruckPresentation;
            Transform truck = presentation.Truck;
            Vector3 home = truck.localPosition;
            Quaternion homeRotation = truck.localRotation;
            int objectCount = game.Player.GetComponentsInChildren<Transform>(true).Length;
            Apply(game, PowerUpType.Truck);
            for (int i = 0; i < 10; i++) presentation.Tick(0.05f, 12, 8);
            Assert.That(Vector3.Distance(truck.localPosition, home), Is.GreaterThan(0.001f));
            Assert.That(Quaternion.Angle(truck.localRotation, homeRotation), Is.GreaterThan(1));
            Transform front = truck.Find("Left Wheel 1"), rear = truck.Find("Left Wheel 2");
            Assert.That(Quaternion.Angle(front.localRotation, rear.localRotation), Is.GreaterThan(10), "Front tires steer independently of rear tires.");
            foreach (ParticleSystem system in presentation.Exhaust)
            {
                Assert.That(system.particleCount, Is.GreaterThan(0));
                Assert.That(system.particleCount, Is.LessThanOrEqualTo(80));
                Assert.That(system.isPaused, Is.True, "Smoke is advanced only by gameplay Tick.");
            }
            Quaternion rotation = front.localRotation;
            float smokeTime = presentation.Exhaust[0].time;
            Apply(game, PowerUpType.Truck);
            Assert.That(front.localRotation, Is.EqualTo(rotation), "Refreshing Truck must not snap its wheels.");
            Assert.That(presentation.Exhaust[0].time, Is.EqualTo(smokeTime));
            TickTruck(game, game.TruckRemaining);
            Assert.That(truck.localPosition, Is.EqualTo(home));
            Assert.That(truck.localRotation, Is.EqualTo(homeRotation));
            Assert.That(front.localRotation, Is.EqualTo(Quaternion.identity));
            foreach (ParticleSystem system in presentation.Exhaust) Assert.That(system.particleCount, Is.Zero);
            Apply(game, PowerUpType.Truck); presentation.Tick(0.5f, 30, 0);
            Assert.That(presentation.Exhaust[0].particleCount, Is.GreaterThan(0));
            game.StartRun();
            foreach (ParticleSystem system in presentation.Exhaust) Assert.That(system.particleCount, Is.Zero);
            Assert.That(game.Player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
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
            Assert.That(game.Track.Debris.ActiveCount, Is.EqualTo(8), "Coins are collected, not shattered into obstacle debris.");
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
            Assert.That(game.Track.Debris.ActiveCount, Is.EqualTo(8), "Only the obstacle creates fragments, not the coin.");

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
            Assert.That(collected, Is.EqualTo(1), "Flight attracts the later coin without collecting the truck pickup.");
            Assert.That(game.Track.LastPowerUps, Is.EqualTo(new[] { PowerUpType.FighterPlane }));
            Assert.That(truck.gameObject.activeSelf, Is.True);
            Assert.That(match.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TireSmokeRespondsToSpeedAndSteeringFreezesOnPauseAndClearsWithoutGrowingThePool()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game); game.enabled = false;
            PlayerTruck presentation = game.TruckPresentation;
            int objects = game.Player.GetComponentsInChildren<Transform>(true).Length;
            Apply(game, PowerUpType.Truck);
            presentation.Tick(0.2f, 0, 0);
            foreach (ParticleSystem system in presentation.TireSmoke)
            {
                Assert.That(system.particleCount, Is.Zero, "Stopped tires do not emit smoke.");
                Assert.That(system.emission.rateOverTime.constant, Is.Zero);
            }
            presentation.Tick(0.3f, 12, 0);
            float straightRate = presentation.TireSmoke[0].emission.rateOverTime.constant;
            foreach (ParticleSystem system in presentation.TireSmoke)
            {
                Assert.That(system.particleCount, Is.GreaterThan(0));
                Assert.That(system.particleCount, Is.LessThanOrEqualTo(48));
                Assert.That(system.isPaused, Is.True, "Tire smoke is simulated only by gameplay Tick.");
            }
            presentation.Tick(0.1f, 12, 12);
            Assert.That(presentation.TireSmoke[0].emission.rateOverTime.constant, Is.GreaterThan(straightRate));
            presentation.Tick(0.1f, 30, 0);
            Assert.That(presentation.TireSmoke[0].emission.rateOverTime.constant, Is.GreaterThan(straightRate));
            game.enabled = true; game.TogglePause();
            float time = presentation.TireSmoke[0].time;
            int count = presentation.TireSmoke[0].particleCount;
            yield return null; yield return null;
            Assert.That(presentation.TireSmoke[0].time, Is.EqualTo(time));
            Assert.That(presentation.TireSmoke[0].particleCount, Is.EqualTo(count));
            game.TogglePause(); yield return null; yield return null;
            Assert.That(presentation.TireSmoke[0].time, Is.GreaterThan(time));
            game.enabled = false;
            presentation.Tick(1, 0, 0);
            foreach (ParticleSystem system in presentation.TireSmoke) Assert.That(system.particleCount, Is.Zero);
            presentation.Tick(0.3f, 30, 0);
            Apply(game, PowerUpType.Truck);
            Assert.That(presentation.TireSmoke[0].particleCount, Is.GreaterThan(0), "Refresh keeps the existing smoke trail.");
            TickTruck(game, game.TruckRemaining);
            foreach (ParticleSystem system in presentation.TireSmoke) Assert.That(system.particleCount, Is.Zero);
            Apply(game, PowerUpType.Truck); presentation.Tick(0.3f, 30, 0); game.StartRun();
            foreach (ParticleSystem system in presentation.TireSmoke) Assert.That(system.particleCount, Is.Zero);
            Apply(game, PowerUpType.Truck); presentation.Tick(0.3f, 30, 0); presentation.enabled = false;
            foreach (ParticleSystem system in presentation.TireSmoke) Assert.That(system.particleCount, Is.Zero);
            Assert.That(game.Player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
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
