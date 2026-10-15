using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class SupercarLightTrailTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void GameplayPrefabHasTwinLightEffectButNeitherMenuPreviewHasIt()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/PlayerCube.prefab");
            var trail = player.GetComponent<PlayerSupercar>().Car.GetComponentInChildren<SupercarLightTrail>(true);
            Assert.That(trail, Is.Not.Null);
            Assert.That(trail.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("CubeDash/Supercar Light Trail"));
            Color tint = trail.GetComponent<Renderer>().sharedMaterial.GetColor("_Tint");
            Assert.That(tint.r, Is.GreaterThan(2)); Assert.That(tint.g, Is.LessThan(0.05f));
            Assert.That(trail.GetComponent<Collider>(), Is.Null);
            foreach (string path in new[] { "Assets/Prefab/CubeDash/Supercar.prefab", "Assets/Prefab/CubeDash/PlayerCubeMenuPreview.prefab" })
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<SupercarLightTrail>(true), Is.Empty, path);
        }

        [UnityTest]
        public IEnumerator TwinRibbonsStartAtRearLampsFollowTurnsAndReplaceCubeWakeOnEveryMap()
        {
            yield return new EnterPlayMode(); using var economy = new EconomyTestScope();
            GameEconomy.AddCoins(100); GameEconomy.TryBuySupercar(); GameEconomy.EquipSupercar(true);
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + map + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false; game.StartRun();
                var trail = game.SupercarPresentation.LightTrail; Assert.That(trail, Is.Not.Null, map);
                Vector3 origin = game.Player.position;
                Simulate(game, 40);
                Assert.That(trail.TrailRenderer.enabled, Is.True, map);
                AssertCubeWake(false);
                for (int side = 0; side < 2; side++)
                {
                    Assert.That(Vector3.Distance(WorldPoint(trail, side, 0), trail.LampPosition(side)), Is.LessThan(0.001f));
                    Vector3 tail = WorldPoint(trail, side, 64);
                    Assert.That(tail.z, Is.LessThan(trail.LampPosition(side).z - 2.5f));
                    Assert.That(tail.y, Is.GreaterThan(origin.y - 0.5f), "Light streaks leave the lamps, not the road surface.");
                    Assert.That(tail.x, Is.LessThan(trail.LampPosition(side).x - 0.1f), "The ribbon retains the driven lane-change path.");
                }
                float lampSeparation = game.SupercarPresentation.Car.TransformVector(Vector3.right * 1.36f).magnitude;
                Assert.That(Vector3.Distance(WorldPoint(trail, 0, 0), WorldPoint(trail, 1, 0)), Is.EqualTo(lampSeparation).Within(0.001f),
                    "Lamp spacing follows each map's authored player scale.");
                Assert.That(trail.TrailMesh.vertexCount, Is.EqualTo(260));
            }
        }

        [UnityTest]
        public IEnumerator PauseFreezesTrailsPowerUpsHideThemAndRetryAndUnequipClearThemWithoutAllocatingMoreObjects()
        {
            yield return new EnterPlayMode(); using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false; EconomyTestScope.Equip(game); game.StartRun();
            var trail = game.SupercarPresentation.LightTrail; Simulate(game, 40);
            var mesh = trail.TrailMesh; Vector3[] vertices = mesh.vertices;
            int objects = game.Player.GetComponentsInChildren<Transform>(true).Length;
            game.TogglePause();
            for (int i = 0; i < 5; i++) Invoke(game, "Update");
            Assert.That(mesh.vertices, Is.EqualTo(vertices)); Assert.That(trail.TrailRenderer.enabled, Is.True);
            game.TogglePause();
            foreach (PowerUpType type in new[] { PowerUpType.Truck, PowerUpType.FighterPlane })
            {
                Invoke(game, "ApplyPowerUp", type);
                Assert.That(trail.gameObject.activeInHierarchy, Is.False); Assert.That(trail.TrailRenderer.enabled, Is.False); AssertCubeWake(false);
                Invoke(game, type == PowerUpType.Truck ? "UpdateTruckTimers" : "UpdateFlightTimers", 11f);
                Assert.That(trail.gameObject.activeInHierarchy, Is.True); Assert.That(trail.TrailRenderer.enabled, Is.False);
                Simulate(game, 40); Assert.That(trail.TrailRenderer.enabled, Is.True);
            }
            Invoke(game, "Crash"); Assert.That(trail.TrailRenderer.enabled, Is.False);
            game.StartRun(); Assert.That(trail.TrailRenderer.enabled, Is.False); AssertCubeWake(false);
            Simulate(game, 40); Assert.That(trail.TrailRenderer.enabled, Is.True);
            EconomyTestScope.Equip(game, false); Assert.That(trail.TrailRenderer.enabled, Is.False); AssertCubeWake(true);
            for (int i = 0; i < 10; i++) { EconomyTestScope.Equip(game); game.StartRun(); Simulate(game, 4); EconomyTestScope.Equip(game, false); }
            Assert.That(game.Player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
            Assert.That(trail.TrailMesh, Is.SameAs(mesh));
        }

        [UnityTest]
        public IEnumerator WorldOriginShiftsKeepTheRibbonsContinuousAndTeleportsCannotDrawLongBeams()
        {
            yield return new EnterPlayMode(); using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false; EconomyTestScope.Equip(game); game.StartRun();
            var trail = game.SupercarPresentation.LightTrail; Simulate(game, 40);
            Vector3 tail = WorldPoint(trail, 0, 64);
            game.Player.position += Vector3.back * 4096; trail.ShiftWorldOrigin(4096);
            trail.Tick(24, game.GameCamera.transform.position + Vector3.back * 4096);
            Assert.That(Vector3.Distance(WorldPoint(trail, 0, 64), tail + Vector3.back * 4096), Is.LessThan(0.003f));
            Assert.That(trail.TrailMesh.bounds.size.z, Is.LessThan(10));
            game.Player.position += Vector3.forward * 200;
            trail.Tick(24, game.GameCamera.transform.position);
            Assert.That(trail.TrailRenderer.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator LightTrailRenderPreview()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("GPU required.");
            yield return new EnterPlayMode(); using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false; EconomyTestScope.Equip(game); game.StartRun();
            Vector3 origin = game.Player.position, camera = game.GameCamera.transform.position;
            Simulate(game, 40);
            game.GameCamera.transform.position = camera + game.Player.position - origin;
            game.SupercarPresentation.LightTrail.Tick(24, game.GameCamera.transform.position);
            var canvas = Object.FindAnyObjectByType<RunnerHud>().GetComponentInChildren<Canvas>(true);
            if (canvas != null) canvas.gameObject.SetActive(false);
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_LIGHT_TRAIL_PREVIEW");
            if (!string.IsNullOrEmpty(prefix))
            {
                Capture(game.GameCamera, prefix + ".png");
                game.GameCamera.transform.position = game.Player.position + new Vector3(3, 4, -9);
                game.GameCamera.transform.LookAt(game.Player.position + Vector3.forward * 1.5f);
                game.SupercarPresentation.LightTrail.Tick(24, game.GameCamera.transform.position);
                Capture(game.GameCamera, prefix + "-overview.png");
            }
            Assert.That(game.SupercarPresentation.LightTrail.TrailRenderer.enabled, Is.True);
        }

        private static void Simulate(CubeDashGame game, int frames)
        {
            var trail = game.SupercarPresentation.LightTrail;
            trail.Tick(24, game.GameCamera.transform.position);
            for (int i = 0; i < frames; i++)
            {
                game.Player.position += new Vector3(0.015f + i * 0.0005f, 0, 0.2f);
                trail.Tick(24, game.GameCamera.transform.position);
            }
        }
        private static Vector3 WorldPoint(SupercarLightTrail trail, int side, int row)
        {
            int index = (side * 65 + row) * 2; Vector3[] vertices = trail.TrailMesh.vertices;
            return trail.transform.TransformPoint((vertices[index] + vertices[index + 1]) * 0.5f);
        }
        private static void AssertCubeWake(bool visible)
            => Assert.That(Object.FindAnyObjectByType<CubeWake>().Pieces.All(piece => piece.GetComponent<Renderer>().enabled == visible), Is.True);
        private static void Invoke(CubeDashGame game, string method, params object[] args)
            => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);
        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1672, 940, 24); var image = new Texture2D(1672, 940, TextureFormat.RGB24, false);
            var previous = camera.targetTexture; var active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1672, 940), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
