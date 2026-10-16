using System.Collections;
using System.Collections.Generic;
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
    public sealed class VehicleSkinTrailTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void CompactGameplayScaleIsSeparateFromMenuScaleAndOnlyGameplayHasLampTrails()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuSkinsRevamp.PlayerPath);
            foreach (var model in player.GetComponent<PlayerSupercar>().AdditionalSkins)
            {
                Assert.That(model.root.localScale, Is.EqualTo(Vector3.one * 0.3025f));
                var trails = model.root.GetComponentsInChildren<SupercarLightTrail>(true);
                Assert.That(trails.Length, Is.EqualTo(1));
                Assert.That(trails[0].GetComponent<Renderer>().enabled, Is.False);
                Assert.That(AssetDatabase.GetAssetPath(trails[0].GetComponent<Renderer>().sharedMaterial), Is.EqualTo(VehicleSkinFinish.MaterialPath));
                Color tint = trails[0].GetComponent<Renderer>().sharedMaterial.GetColor("_Tint");
                Assert.That(tint.r, Is.GreaterThan(2)); Assert.That(tint.g / tint.r, Is.InRange(0.07f, 0.11f));
                Assert.That(model.root.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var shop = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MenuSkinShop>(true)).Single();
                foreach (var card in shop.Cards.Where(card => card.Skin != PlayerSkin.Supercar))
                {
                    Assert.That(card.Preview.transform.localScale, Is.EqualTo(Vector3.one * 0.55f));
                    Assert.That(card.Preview.GetComponentsInChildren<SupercarLightTrail>(true), Is.Empty);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(PlayerSkin.MonsterTruck)]
        [TestCase(PlayerSkin.MilitaryVan)]
        public void RibbonAnchorsLieOnTheActualRearFacingEmissiveLensTriangles(PlayerSkin skin)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuSkinsRevamp.PlayerPath);
            var model = player.GetComponent<PlayerSupercar>().AdditionalSkins.Single(model => model.skin == skin);
            var trail = model.root.GetComponentInChildren<SupercarLightTrail>(true);
            var data = new SerializedObject(trail);
            Assert.That(data.FindProperty("car").objectReferenceValue, Is.SameAs(model.root));
            var mesh = model.root.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices; int[] triangles = mesh.GetTriangles(1);
            foreach (string field in new[] { "leftLamp", "rightLamp" })
            {
                Vector3 anchor = data.FindProperty(field).vector3Value;
                // The lens triangle must face backward, and its projected X/Y bounds must contain the anchor.
                bool matches = false;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    if (Vector3.Cross(b - a, c - a).normalized.z > -0.8f) continue;
                    if (Mathf.Abs((a.z + b.z + c.z) / 3 - anchor.z) > 0.025f) continue;
                    var bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
                    if (anchor.x >= bounds.min.x - 0.01f && anchor.x <= bounds.max.x + 0.01f &&
                        anchor.y >= bounds.min.y - 0.01f && anchor.y <= bounds.max.y + 0.01f) { matches = true; break; }
                }
                Assert.That(matches, Is.True, field + " must originate at an actual rear lens, not a guessed bumper location.");
            }
        }

        [UnityTest]
        public IEnumerator BothSkinsHaveCompactWorldFootprintsOnEveryMapAfterRetryAndPowerUps()
        {
            yield return new EnterPlayMode(); using var economy = new EconomyTestScope();
            GameEconomy.AddCoins(200); GameEconomy.TryBuy(PlayerSkin.MonsterTruck); GameEconomy.TryBuy(PlayerSkin.MilitaryVan);
            var measurements = new List<string>(); var oversized = new List<string>();
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + map + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false;
                var collider = game.Player.GetComponent<BoxCollider>(); Vector3 size = collider.size, center = collider.center;
                foreach (var skin in new[] { PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan })
                {
                    GameEconomy.Equip(skin); game.StartRun();
                    var presentation = game.SupercarPresentation;
                    presentation.Tick(0.4f, 0, 0); // Past the equip pop; stationary wheels, actual scene camera/FOV.
                    Bounds bounds = ModelWorldBounds(presentation.Car);
                    string label = map + " " + skin + " world footprint=" + bounds.size.ToString("F3");
                    measurements.Add(label);
                    if (bounds.size.x > 1.4f || bounds.size.y > 1.25f || bounds.size.z > 2.3f) oversized.Add(label);
                    Assert.That(bounds.min.y, Is.InRange(-0.09f, 0.06f), "Resized tires must remain on the road: " + label);
                    CaptureGameplayCamera(game, map, skin);
                    foreach (var type in new[] { PowerUpType.Truck, PowerUpType.FighterPlane })
                    {
                        Invoke(game, "ApplyPowerUp", type);
                        Invoke(game, type == PowerUpType.Truck ? "UpdateTruckTimers" : "UpdateFlightTimers", 30f);
                        presentation.Tick(0.4f, 0, 0);
                        Assert.That(Vector3.Distance(ModelWorldBounds(presentation.Car).size, bounds.size), Is.LessThan(0.002f), label);
                    }
                    game.StartRun(); presentation.Tick(0.4f, 0, 0);
                    Assert.That(Vector3.Distance(ModelWorldBounds(presentation.Car).size, bounds.size), Is.LessThan(0.002f), "Retry must not enlarge " + label);
                    Assert.That(collider.size, Is.EqualTo(size)); Assert.That(collider.center, Is.EqualTo(center));
                }
            }
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_GAMEPLAY_SIZE_PREVIEW");
            if (!string.IsNullOrEmpty(prefix)) File.WriteAllLines(prefix + "-measurements.txt", measurements);
            Assert.That(oversized, Is.Empty, "Check actual world dimensions, not just the model's localScale:\n" + string.Join("\n", oversized));
        }

        private static Bounds ModelWorldBounds(Transform model)
        {
            // Road bending pads Renderer.bounds by hundreds of units for culling. Measure the
            // actual mesh vertices, not those culling bounds or the light ribbon's trailing mesh.
            Bounds bounds = default; bool first = true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<SupercarLightTrail>() != null || filter.GetComponent<VehicleShield>() != null) continue;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(vertex);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            Assert.That(first, Is.False, "Vehicle geometry is required to measure the real world footprint.");
            return bounds;
        }

        private static void CaptureGameplayCamera(CubeDashGame game, string map, PlayerSkin skin)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_GAMEPLAY_SIZE_PREVIEW");
            if (string.IsNullOrEmpty(prefix) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = game.GameCamera; var oldTarget = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24); var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(prefix + "-" + map + "-" + skin + ".png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(target); Object.DestroyImmediate(texture);
            }
        }

        [UnityTest]
        public IEnumerator BothSkinsUseRearLightRibbonsNotCubeWakeAndHandlePausePowerUpsRetryAndOriginShift()
        {
            yield return new EnterPlayMode(); using var economy = new EconomyTestScope();
            GameEconomy.AddCoins(200); GameEconomy.TryBuy(PlayerSkin.MonsterTruck); GameEconomy.TryBuy(PlayerSkin.MilitaryVan);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Desert.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false;
            foreach (var skin in new[] { PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan })
            {
                GameEconomy.Equip(skin); Invoke(game, "RefreshEquippedSkin"); game.StartRun(); Clear(game);
                var trail = game.SupercarPresentation.LightTrail; Assert.That(trail, Is.Not.Null);
                Assert.That(game.SupercarPresentation.Car.localScale, Is.EqualTo(Vector3.one * 0.3025f));
                int count = game.Player.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 0; i < 40; i++) { game.Player.position += new Vector3(0.015f, 0, 0.2f); Invoke(game, "Update"); }
                Assert.That(trail.TrailRenderer.enabled, Is.True, "The actual game Update must tick both new trails.");
                AssertWake(false);
                for (int side = 0; side < 2; side++)
                {
                    Assert.That(Vector3.Distance(Point(trail, side, 0), trail.LampPosition(side)), Is.LessThan(0.002f));
                    Assert.That(Point(trail, side, 64).z, Is.LessThan(trail.LampPosition(side).z - 1.0f));
                }
                var mesh = trail.TrailMesh; var beforePause = mesh.vertices;
                game.TogglePause(); for (int i = 0; i < 3; i++) Invoke(game, "Update");
                Assert.That(mesh.vertices, Is.EqualTo(beforePause)); game.TogglePause();
                Capture(game, skin);
                Vector3 tail = Point(trail, 0, 64);
                game.Player.position += Vector3.back * 4096; trail.ShiftWorldOrigin(4096);
                trail.Tick(game.Speed, game.GameCamera.transform.position + Vector3.back * 4096);
                Assert.That(Vector3.Distance(Point(trail, 0, 64), tail + Vector3.back * 4096), Is.LessThan(0.003f));
                foreach (var type in new[] { PowerUpType.Truck, PowerUpType.FighterPlane })
                {
                    Invoke(game, "ApplyPowerUp", type); Assert.That(trail.TrailRenderer.enabled, Is.False); AssertWake(false);
                    Invoke(game, type == PowerUpType.Truck ? "UpdateTruckTimers" : "UpdateFlightTimers", 30f);
                    Assert.That(game.VehicleDriving, Is.True); Assert.That(trail.TrailRenderer.enabled, Is.False);
                }
                game.StartRun(); Assert.That(trail.TrailRenderer.enabled, Is.False); AssertWake(false);
                Assert.That(trail.TrailMesh, Is.SameAs(mesh));
                Assert.That(game.Player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                GameEconomy.Equip(PlayerSkin.Cube); Invoke(game, "RefreshEquippedSkin"); AssertWake(true);
                Assert.That(trail.TrailRenderer.enabled, Is.False);
            }
        }

        private static void Capture(CubeDashGame game, PlayerSkin skin)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_VEHICLE_TRAIL_PREVIEW");
            if (string.IsNullOrEmpty(prefix) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = game.GameCamera; Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            var canvas = game.GetComponentInChildren<Canvas>(true);
            if (canvas == null) canvas = Object.FindAnyObjectByType<RunnerHud>().GetComponentInChildren<Canvas>(true);
            bool canvasActive = canvas.gameObject.activeSelf; canvas.gameObject.SetActive(false);
            var target = new RenderTexture(1600, 900, 24); var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var oldTarget = camera.targetTexture; var active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                camera.transform.position = game.Player.position + new Vector3(3.5f, 2.5f, -7);
                camera.transform.LookAt(game.Player.position + Vector3.back * 0.8f);
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(prefix + "-" + skin + ".png", texture.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation); camera.targetTexture = oldTarget; RenderTexture.active = active;
                ShaderUtil.allowAsyncCompilation = async; canvas.gameObject.SetActive(canvasActive);
                Object.DestroyImmediate(target); Object.DestroyImmediate(texture);
            }
        }
        private static Vector3 Point(SupercarLightTrail trail, int side, int row)
        {
            int index = (side * 65 + row) * 2; var vertices = trail.TrailMesh.vertices;
            return trail.transform.TransformPoint((vertices[index] + vertices[index + 1]) * 0.5f);
        }
        private static void Clear(CubeDashGame game)
        {
            foreach (var segment in game.Track.Segments)
            { foreach (var cube in segment.Cubes) cube.gameObject.SetActive(false); foreach (var pickup in segment.PowerUps) pickup.gameObject.SetActive(false); }
        }
        private static void AssertWake(bool visible) => Assert.That(Object.FindAnyObjectByType<CubeWake>().Pieces.All(piece => piece.GetComponent<Renderer>().enabled == visible), Is.True);
        private static void Invoke(CubeDashGame game, string method, params object[] args)
            => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);
    }
}
