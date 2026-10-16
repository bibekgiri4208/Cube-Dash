using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class VehicleShieldTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void AllThreeVehiclesHaveDistinctBakedAngularFieldsWithoutChangingMenuOrCubeShield()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuSkinsRevamp.PlayerPath);
            var fields = player.GetComponentsInChildren<VehicleShield>(true);
            Assert.That(fields.Length, Is.EqualTo(3));
            Assert.That(fields.Select(field => field.GetComponent<MeshFilter>().sharedMesh).Distinct().Count(), Is.EqualTo(3));
            foreach (var field in fields)
            {
                Assert.That(field.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(field.GetComponent<Collider>(), Is.Null);
                Assert.That(field.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                var mesh = field.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(AssetDatabase.GetAssetPath(mesh), Does.StartWith(VehicleShieldAuthoring.MeshFolder));
                Assert.That(mesh.vertexCount, Is.LessThan(150));
                Assert.That(mesh.bounds.size.z, Is.GreaterThan(mesh.bounds.size.y * 1.5f), "An elongated fitted field, not a ball.");
                var material = field.GetComponent<Renderer>().sharedMaterial;
                Assert.That(AssetDatabase.GetAssetPath(material), Is.EqualTo(VehicleShieldAuthoring.MaterialPath));
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                Assert.That(File.ReadAllText(AssetDatabase.GetAssetPath(material.shader)), Does.Not.Contain("_Time"), "Pause must freeze shader motion.");
            }
            Assert.That(player.transform.Find("Landing Shield").localScale, Is.EqualTo(Vector3.one * 1.65f));
            foreach (string path in new[] { "Assets/Prefab/CubeDash/PlayerCubeMenuPreview.prefab", MainMenuSkinsRevamp.MonsterPath,
                MainMenuSkinsRevamp.MilitaryPath, "Assets/Prefab/CubeDash/Supercar.prefab" })
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<VehicleShield>(true), Is.Empty, path);
        }

        [UnityTest]
        public IEnumerator EveryVehicleOnEveryMapUsesArmorInsteadOfBubbleAndShieldHitsPausePowerUpsAndRetryStillWork()
        {
            yield return new EnterPlayMode(); using var economy = new EconomyTestScope();
            GameEconomy.AddCoins(300);
            foreach (var skin in new[] { PlayerSkin.Supercar, PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan }) GameEconomy.TryBuy(skin);
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + map + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false;
                int objects = game.Player.GetComponentsInChildren<Transform>(true).Length;
                var collider = game.Player.GetComponent<BoxCollider>(); Vector3 size = collider.size, center = collider.center, rootScale = game.Player.localScale;
                foreach (var skin in new[] { PlayerSkin.Supercar, PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan })
                {
                    GameEconomy.Equip(skin); game.StartRun(); Clear(game);
                    var presentation = game.SupercarPresentation; var field = presentation.Shield;
                    Assert.That(field, Is.Not.Null, map + " " + skin); Assert.That(field.Visible, Is.False);
                    var mesh = field.GetComponent<MeshFilter>().sharedMesh; var material = field.FieldRenderer.sharedMaterial;
                    Invoke(game, "ApplyPowerUp", PowerUpType.Shield); Invoke(game, "Update");
                    Assert.That(game.Shields, Is.EqualTo(1)); Assert.That(field.Visible, Is.True);
                    Assert.That(field.EffectAge, Is.GreaterThan(0), "Actual gameplay Update must advance the armor field.");
                    Assert.That(game.FlightPresentation.ShieldVisible, Is.False, "Never draw the old ball over a car.");
                    presentation.TickShield(0.2f, true);
                    if (map == "Desert") Capture(game, skin);
                    game.TogglePause(); float age = field.EffectAge;
                    Invoke(game, "Update"); Invoke(game, "Update");
                    Assert.That(field.EffectAge, Is.EqualTo(age)); Assert.That(field.Visible, Is.True);
                    game.TogglePause();
                    var hazard = game.Track.Segments[1].Cubes[0];
                    hazard.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue), false);
                    hazard.transform.position = game.Player.position + Vector3.forward * 0.2f; hazard.gameObject.SetActive(true);
                    Invoke(game, "Update");
                    Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running), "A shield still absorbs exactly one hit.");
                    Assert.That(game.Shields, Is.Zero); Assert.That(field.Visible, Is.False);
                    foreach (var type in new[] { PowerUpType.Truck, PowerUpType.FighterPlane })
                    {
                        Invoke(game, "ApplyPowerUp", type); Assert.That(field.Visible, Is.False);
                        Assert.That(game.FlightPresentation.ShieldVisible, Is.False);
                        Invoke(game, type == PowerUpType.Truck ? "UpdateTruckTimers" : "UpdateFlightTimers", 30f);
                        Assert.That(game.LandingShieldRemaining, Is.GreaterThan(0)); Assert.That(field.Visible, Is.True);
                        Assert.That(game.FlightPresentation.ShieldVisible, Is.False);
                        Invoke(game, "UpdateFlightTimers", 3.1f); Invoke(game, "Update");
                        Assert.That(field.Visible, Is.False, "Timed protection expiration clears the field.");
                    }
                    Invoke(game, "ApplyPowerUp", PowerUpType.Shield);
                    GameEconomy.Equip(PlayerSkin.Cube); Invoke(game, "RefreshEquippedSkin");
                    game.FlightPresentation.Tick(0.1f, 0, true);
                    Assert.That(field.Visible, Is.False); Assert.That(game.FlightPresentation.ShieldVisible, Is.True, "The cube keeps its original bubble.");
                    GameEconomy.Equip(skin); Invoke(game, "RefreshEquippedSkin");
                    Assert.That(game.FlightPresentation.ShieldVisible, Is.False); Assert.That(field.Visible, Is.True);
                    Invoke(game, "Crash"); Assert.That(field.Visible, Is.False);
                    game.StartRun(); Assert.That(field.Visible, Is.False);
                    Assert.That(field.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh)); Assert.That(field.FieldRenderer.sharedMaterial, Is.SameAs(material));
                    Assert.That(game.Player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                    Assert.That(collider.size, Is.EqualTo(size)); Assert.That(collider.center, Is.EqualTo(center)); Assert.That(game.Player.localScale, Is.EqualTo(rootScale));
                }
            }
        }

        private static void Clear(CubeDashGame game)
        {
            foreach (var segment in game.Track.Segments)
            { foreach (var cube in segment.Cubes) cube.gameObject.SetActive(false); foreach (var pickup in segment.PowerUps) pickup.gameObject.SetActive(false); }
        }
        private static void Invoke(CubeDashGame game, string method, params object[] args)
            => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);
        private static void Capture(CubeDashGame game, PlayerSkin skin)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_VEHICLE_SHIELD_PREVIEW");
            if (string.IsNullOrEmpty(prefix) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var camera = game.GameCamera; var oldTarget = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24); var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(prefix + "-" + skin + ".png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(target); Object.DestroyImmediate(texture);
            }
        }
    }
}
