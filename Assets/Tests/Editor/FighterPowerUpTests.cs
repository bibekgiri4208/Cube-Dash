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
    public sealed class FighterPowerUpTests
    {
        [Test]
        public void FighterIsAnAuthoredDetailedTwinEngineModelWithBoundedEffects()
        {
            GameObject fighter = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/FighterPlane.prefab");
            Assert.That(fighter, Is.Not.Null);
            Mesh mesh = fighter.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.name, Is.EqualTo(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mesh))));
            Assert.That(mesh, Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashFighter.asset")));
            Assert.That(mesh.vertexCount, Is.InRange(600, 5000));
            Assert.That(mesh.subMeshCount, Is.EqualTo(4));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(5.3f).Within(0.05f));
            Assert.That(mesh.bounds.size.z, Is.GreaterThan(5));
            Assert.That(mesh.bounds.max.y, Is.GreaterThan(1.3f), "Twin vertical fins must be modelled, not painted on.");
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-12f));
            }
            foreach (Vector3 normal in normals) Assert.That(normal.sqrMagnitude, Is.InRange(0.9f, 1.1f));
            Assert.That(fighter.GetComponentsInChildren<Collider>(true), Is.Empty);
            ParticleSystem[] systems = fighter.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems.Length, Is.EqualTo(4));
            foreach (ParticleSystem system in systems)
            {
                Assert.That(system.main.playOnAwake, Is.False);
                Assert.That(system.main.maxParticles, Is.LessThanOrEqualTo(96));
                Assert.That(system.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(system.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("CubeDash/Jet Particle"));
            }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.TryGetComponent(out CubeDashGame candidate)) game = candidate;
                var settings = new SerializedObject(game);
                Assert.That(settings.FindProperty("flightDuration").floatValue, Is.EqualTo(10));
                Assert.That(settings.FindProperty("landingShieldDuration").floatValue, Is.EqualTo(3));
                Assert.That(game.FlightPresentation, Is.SameAs(game.Player.GetComponent<PlayerFlight>()));
                Assert.That(game.FlightPresentation.Fighter.gameObject.activeSelf, Is.False);
                foreach (TrackSegment segment in game.Track.Segments)
                    foreach (PowerUpPickup pickup in segment.PowerUps)
                        Assert.That(pickup.transform.Find("Fighter Icon"), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator FlightTransformsPausesExpiresIntoThreeSecondProtectionAndRestartsCleanly()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); ClearTrack(game);
            Vector3 home = game.Player.position;
            int pool = game.Track.GetComponentsInChildren<Transform>(true).Length;
            PowerUpPickup pickup = game.Track.Segments[1].PowerUps[0];
            pickup.Configure(PowerUpType.FighterPlane, game.Track.PowerUpMaterial(PowerUpType.FighterPlane));
            pickup.transform.position = new Vector3(home.x, 1.35f, 0.5f); pickup.gameObject.SetActive(true);
            yield return null; yield return null;
            Assert.That(game.Flying, Is.True);
            Assert.That(game.FlightRemaining, Is.InRange(9.5f, 10));
            Assert.That(game.Player.Find("Cube Visual").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(game.Player.GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(game.FlightPresentation.Fighter.gameObject.activeSelf, Is.True);
            game.FlightPresentation.Tick(0.5f, 10, false);
            Assert.That(game.FlightPresentation.Exhaust[0].particleCount, Is.GreaterThan(0));
            game.TogglePause();
            float timer = game.FlightRemaining, smokeTime = game.FlightPresentation.Exhaust[0].time;
            Vector3 pausedPosition = game.Player.position;
            yield return null; yield return null;
            Assert.That(game.FlightRemaining, Is.EqualTo(timer));
            Assert.That(game.FlightPresentation.Exhaust[0].time, Is.EqualTo(smokeTime));
            Assert.That(game.Player.position, Is.EqualTo(pausedPosition));
            game.TogglePause(); game.enabled = false;
            TickTimers(game, game.FlightRemaining - 0.01f);
            Assert.That(game.Flying, Is.True);
            TickTimers(game, 0.02f);
            Assert.That(game.Flying, Is.False);
            Assert.That(game.LandingShieldRemaining, Is.EqualTo(3));
            Assert.That(game.Player.Find("Cube Visual").GetComponent<Renderer>().enabled, Is.True);
            Assert.That(game.Player.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(game.FlightPresentation.Fighter.gameObject.activeSelf, Is.False);
            game.FlightPresentation.Tick(0.1f, 0, true);
            Assert.That(game.FlightPresentation.ShieldVisible, Is.True);
            foreach (ParticleSystem system in game.FlightPresentation.Exhaust) Assert.That(system.particleCount, Is.Zero);
            TickTimers(game, 2.9f);
            Assert.That(game.LandingShieldRemaining, Is.GreaterThan(0));
            TickTimers(game, 0.2f);
            Assert.That(game.LandingShieldRemaining, Is.Zero);
            game.StartRun();
            Assert.That(game.FlightRemaining, Is.Zero);
            Assert.That(game.LandingShieldRemaining, Is.Zero);
            Assert.That(game.Player.position, Is.EqualTo(home));
            Assert.That(game.FlightPresentation.ShieldVisible, Is.False);
            Assert.That(game.Track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(pool));
        }

        [UnityTest]
        public IEnumerator SteeringAndAltitudeWorkDuringFlightAndRestartClearsActiveSmoke()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); ClearTrack(game);
            Vector3 home = game.Player.position;
            typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { PowerUpType.FighterPlane });
            game.ChangeLane(1);
            for (float elapsed = 0; elapsed < 0.45f; elapsed += Time.deltaTime) yield return null;
            Assert.That(game.Flying, Is.True);
            Assert.That(game.Player.position.y, Is.GreaterThan(home.y + 2), "Flight must actually rise above the rows.");
            Assert.That(game.Player.position.x, Is.GreaterThan(home.x + 1), "Normal lane steering remains available.");
            Assert.That(game.FlightPresentation.Exhaust[0].particleCount, Is.GreaterThan(0));
            game.StartRun();
            Assert.That(game.Flying, Is.False);
            Assert.That(game.LandingShieldRemaining, Is.Zero);
            Assert.That(game.FlightPresentation.Fighter.gameObject.activeSelf, Is.False);
            Assert.That(game.Player.position, Is.EqualTo(home));
            Assert.That(game.Player.GetComponent<BoxCollider>().enabled, Is.True);
            foreach (ParticleSystem system in game.FlightPresentation.Exhaust) Assert.That(system.particleCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AirborneCollectsLaneCoinsButSkipsHazardsAndBonusesAndLandingShieldProtectsEveryHit()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            ClearTrack(game);
            RunnerCube wrong = PlaceCube(game, 0, CubeColor.Blue, 2);
            RunnerCube match = PlaceCube(game, 1, CubeColor.Red, 4);
            PowerUpPickup pickup = game.Track.Segments[1].PowerUps[0];
            pickup.Configure(PowerUpType.FighterPlane, game.Track.PowerUpMaterial(PowerUpType.FighterPlane));
            pickup.transform.position = new Vector3(0, 1.35f, 3); pickup.gameObject.SetActive(true);
            Assert.That(game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, false, true, false, false,
                false, 8, new Vector3(0, 3.775f, 0), 1,
                out int collected, out bool used), Is.False);
            Assert.That(collected, Is.EqualTo(1)); Assert.That(used, Is.False);
            Assert.That(match.gameObject.activeSelf, Is.False);
            Assert.That(wrong.gameObject.activeSelf && pickup.gameObject.activeSelf, Is.True);
            Assert.That(game.Track.LastPowerUps, Is.Empty);
            ClearTrack(game);
            PlaceCube(game, 0, CubeColor.Blue, 2); PlaceCube(game, 1, CubeColor.Green, 4);
            Assert.That(game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, true, false, true,
                out collected, out used), Is.False);
            Assert.That(used, Is.False, "Timed landing protection must not consume a regular shield charge.");
            ClearTrack(game);
            PlaceCube(game, 0, CubeColor.Blue, 2);
            Assert.That(game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, false, false, false,
                out collected, out used), Is.True);
        }

        [UnityTest]
        public IEnumerator SweptTakeoffProtectsLaterContactsButNotAnEarlierFatalHit()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            ClearTrack(game);
            PowerUpPickup pickup = game.Track.Segments[1].PowerUps[0];
            pickup.Configure(PowerUpType.FighterPlane, game.Track.PowerUpMaterial(PowerUpType.FighterPlane));
            pickup.transform.position = new Vector3(0, 1.35f, 3); pickup.gameObject.SetActive(true);
            PlaceCube(game, 0, CubeColor.Blue, 8);
            Assert.That(game.Track.Advance(10, 0, 0, 0, Vector2.one * 0.575f, 0, out int collected), Is.False);
            Assert.That(game.Track.LastPowerUps, Does.Contain(PowerUpType.FighterPlane));
            Assert.That(collected, Is.Zero);
            ClearTrack(game);
            PlaceCube(game, 0, CubeColor.Blue, 2);
            pickup.transform.position = new Vector3(0, 1.35f, 8); pickup.gameObject.SetActive(true);
            Assert.That(game.Track.Advance(10, 0, 0, 0, Vector2.one * 0.575f, 0, out collected), Is.True);
            Assert.That(game.Track.LastPowerUps, Is.Empty);
        }

        [UnityTest]
        public IEnumerator FlightAutomaticallyLiftsOnlyCurrentLaneCoinsUpToThePlaneInEveryLane()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.enabled = false;
            foreach (CubeColor color in System.Enum.GetValues(typeof(CubeColor)))
                for (int lane = 0; lane < 3; lane++)
                {
                    game.Track.Reset(42, color); ClearTrack(game);
                    float x = (lane - 1) * game.Track.LaneWidth;
                    Vector3 plane = new Vector3(x, 3.775f, 0);
                    RunnerCube coin = PlaceCube(game, 0, color, 3);
                    coin.transform.position = new Vector3(x, 0.95f, 3);
                    coin.Configure(color, game.Track.ColorMaterial(color), true);
                    RunnerCube adjacent = PlaceCube(game, 1, color, 3);
                    adjacent.transform.position = new Vector3(((lane + 1) % 3 - 1) * game.Track.LaneWidth, 0.95f, 3);
                    adjacent.Configure(color, game.Track.ColorMaterial(color), true);
                    RunnerCube distant = PlaceCube(game, 2, color, 12);
                    distant.transform.position = new Vector3(x, 0.95f, 12);
                    distant.Configure(color, game.Track.ColorMaterial(color), true);
                    Physics.SyncTransforms();
                    Bounds hitbox = coin.Collider.bounds;
                    game.Track.Advance(0, x, x, 0, Vector2.one * 0.575f, 0, false, true, false, false,
                        false, 8, plane, 0.025f, out int collected, out _);
                    Assert.That(collected, Is.Zero, "Coins must rise toward the plane before collection.");
                    Assert.That(coin.CoinVisual.position.y, Is.GreaterThan(0.95f));
                    Assert.That(coin.Collider.bounds, Is.EqualTo(hitbox), "Only the visual moves, not the pooled gameplay slot.");
                    game.Track.Advance(0, x, x, 0, Vector2.one * 0.575f, 0, false, true, false, false,
                        false, 8, plane, 0.4f, out collected, out _);
                    Assert.That(collected, Is.EqualTo(1)); Assert.That(coin.gameObject.activeSelf, Is.False);
                    Assert.That(adjacent.gameObject.activeSelf && distant.gameObject.activeSelf, Is.True);
                    Assert.That(adjacent.CoinVisual.localPosition.x, Is.Zero);
                    game.Track.Advance(0, x, x, 0, Vector2.one * 0.575f, 0, false, true, false, false,
                        false, 8, plane, 0.1f, out collected, out _);
                    Assert.That(collected, Is.Zero, "A flight coin is collected only once.");
                }
        }

        [UnityTest]
        public IEnumerator AutomaticFlightAttractionFollowsLaneChanges()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.enabled = false; ClearTrack(game);
            RunnerCube center = PlaceCube(game, 0, CubeColor.Red, 6);
            RunnerCube right = PlaceCube(game, 1, CubeColor.Red, 3);
            right.transform.position = new Vector3(game.Track.LaneWidth, 0.95f, 3);
            Vector3 plane = new Vector3(game.Track.LaneWidth, 3.775f, 0);
            game.Track.Advance(0, 0, game.Track.LaneWidth, 0, Vector2.one * 0.575f, 0, false, true, false, false,
                false, 8, plane, 0.05f, out _, out _);
            game.Track.Advance(0, game.Track.LaneWidth, game.Track.LaneWidth, 0, Vector2.one * 0.575f, 0, false, true, false, false,
                false, 8, plane, 0.4f, out int collected, out _);
            Assert.That(collected, Is.EqualTo(1)); Assert.That(right.gameObject.activeSelf, Is.False);
            Assert.That(center.gameObject.activeSelf, Is.True, "Changing lanes must not keep attracting the previous lane.");
        }

        [UnityTest]
        public IEnumerator FlightCoinScoringStacksWithMagnetAndTwoXAndFreezesOnPause()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); ClearTrack(game);
            Apply(game, PowerUpType.FighterPlane); Apply(game, PowerUpType.DoublePoints);
            for (float elapsed = 0; elapsed < 0.45f; elapsed += Time.deltaTime) yield return null;
            Assert.That(game.Player.position.y, Is.GreaterThan(3));
            RunnerCube center = PlaceCube(game, 0, CubeColor.Red, 5);
            RunnerCube side = PlaceCube(game, 1, CubeColor.Red, 5);
            side.transform.position = new Vector3(game.Track.LaneWidth, 0.95f, 5);
            RunnerCube hazard = PlaceCube(game, 2, CubeColor.Blue, 4);
            PowerUpPickup bonus = game.Track.Segments[1].PowerUps[0];
            bonus.Configure(PowerUpType.Shield, game.Track.PowerUpMaterial(PowerUpType.Shield));
            bonus.transform.position = new Vector3(0, 1.35f, 3); bonus.gameObject.SetActive(true);
            for (float elapsed = 0; elapsed < 0.2f; elapsed += Time.deltaTime) yield return null;
            Assert.That(game.Score, Is.EqualTo(2)); Assert.That(center.gameObject.activeSelf, Is.False);
            Assert.That(side.gameObject.activeSelf, Is.True);
            Apply(game, PowerUpType.Magnet);
            yield return null;
            game.TogglePause();
            Vector3 frozenCoin = side.CoinVisual.position;
            float magnetTime = game.MagnetRemaining;
            yield return null; yield return null;
            Assert.That(side.CoinVisual.position, Is.EqualTo(frozenCoin));
            Assert.That(game.MagnetRemaining, Is.EqualTo(magnetTime));
            game.TogglePause();
            for (float elapsed = 0; elapsed < 0.3f; elapsed += Time.deltaTime) yield return null;
            Assert.That(game.Score, Is.EqualTo(4)); Assert.That(side.gameObject.activeSelf, Is.False);
            Assert.That(hazard.gameObject.activeSelf && bonus.gameObject.activeSelf, Is.True);
            Assert.That(game.Shields, Is.Zero); Assert.That(game.Flying, Is.True);
            typeof(CubeDashGame).GetMethod("UpdateMagnetTimer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { 10f });
            Assert.That(game.MagnetActive, Is.False);
            center = PlaceCube(game, 0, CubeColor.Red, 5);
            side = PlaceCube(game, 1, CubeColor.Red, 5);
            side.transform.position = new Vector3(game.Track.LaneWidth, 0.95f, 5);
            for (float elapsed = 0; elapsed < 0.3f; elapsed += Time.deltaTime) yield return null;
            Assert.That(game.Score, Is.EqualTo(6), "The plane keeps its lane-only attraction after Magnet expires.");
            Assert.That(side.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator FlightAttractionBeginsAtSweptTakeoffButNeverScoresAfterAnEarlierCrash()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.enabled = false; ClearTrack(game);
            PowerUpPickup plane = game.Track.Segments[1].PowerUps[0];
            plane.Configure(PowerUpType.FighterPlane, game.Track.PowerUpMaterial(PowerUpType.FighterPlane));
            plane.transform.position = new Vector3(0, 1.35f, 2); plane.gameObject.SetActive(true);
            PlaceCube(game, 0, CubeColor.Red, 7); PlaceCube(game, 1, CubeColor.Blue, 6);
            Assert.That(game.Track.Advance(12, 0, 0, 0, Vector2.one * 0.575f, 0, false, false, false, false,
                false, 8, new Vector3(0, 3.775f, 0), 1, out int collected, out _), Is.False);
            Assert.That(collected, Is.EqualTo(1));
            Assert.That(game.Track.LastPowerUps, Does.Contain(PowerUpType.FighterPlane));
            ClearTrack(game);
            plane.transform.position = new Vector3(0, 1.35f, 8); plane.gameObject.SetActive(true);
            PlaceCube(game, 0, CubeColor.Blue, 1.3f); PlaceCube(game, 1, CubeColor.Red, 9);
            Assert.That(game.Track.Advance(12, 0, 0, 0, Vector2.one * 0.575f, 0, false, false, false, false,
                false, 8, new Vector3(0, 3.775f, 0), 1, out collected, out _), Is.True);
            Assert.That(collected, Is.Zero); Assert.That(game.Track.LastPowerUps, Is.Empty);
        }

        private static void Apply(CubeDashGame game, PowerUpType type)
            => typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { type });
        private static void TickTimers(CubeDashGame game, float dt)
            => typeof(CubeDashGame).GetMethod("UpdateFlightTimers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { dt });
        private static void ClearTrack(CubeDashGame game)
        {
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
        }
        private static RunnerCube PlaceCube(CubeDashGame game, int index, CubeColor color, float z)
        {
            RunnerCube cube = game.Track.Segments[1].Cubes[index];
            cube.Configure(color, game.Track.ColorMaterial(color)); cube.transform.position = new Vector3(0, 0.575f, z);
            cube.gameObject.SetActive(true); return cube;
        }
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
