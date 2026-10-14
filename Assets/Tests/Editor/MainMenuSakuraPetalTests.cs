using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Tests
{
    public sealed class MainMenuSakuraPetalTests
    {
        [Test]
        public void ForegroundTreesHaveLoopingPrewarmedTexturedPetalsWithGentleWindAndNoShadows()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                Transform effect = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>())
                    .Single(item => item.name == MainMenuSakuraPetals.RootName);
                ParticleSystem[] emitters = FallingEmitters(effect);
                Assert.That(emitters.Length, Is.EqualTo(2));
                Texture2D leaf = AssetDatabase.LoadAssetAtPath<Texture2D>(MainMenuSakuraPetals.TexturePath);
                foreach (ParticleSystem emitter in emitters)
                {
                    var main = emitter.main;
                    Assert.That(main.loop && main.prewarm && main.playOnAwake && main.useUnscaledTime, Is.True);
                    Assert.That(main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                    Assert.That(main.maxParticles, Is.LessThanOrEqualTo(180));
                    Assert.That(main.startLifetime.constantMin, Is.GreaterThan(10));
                    Assert.That(main.gravityModifier.constant, Is.Zero);
                    Assert.That(emitter.emission.rateOverTime.constant, Is.InRange(4, 8));
                    Assert.That(emitter.velocityOverLifetime.y.constantMax, Is.LessThan(0));
                    Assert.That(emitter.noise.enabled && emitter.rotationOverLifetime.enabled && emitter.colorOverLifetime.enabled, Is.True);
                    Assert.That(emitter.colorOverLifetime.color.gradient.alphaKeys.First().alpha, Is.Zero);
                    Assert.That(emitter.colorOverLifetime.color.gradient.alphaKeys.Last().alpha, Is.Zero);
                    var renderer = emitter.GetComponent<ParticleSystemRenderer>();
                    Assert.That(renderer.sharedMaterial.mainTexture, Is.SameAs(leaf));
                    Assert.That(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), Is.False);
                    Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                    Assert.That(renderer.receiveShadows, Is.False);
                }
                Assert.That(emitters.Select(item => item.transform.position.x), Has.Some.LessThan(0));
                Assert.That(emitters.Select(item => item.transform.position.x), Has.Some.GreaterThan(0));
                Assert.That(emitters.All(item => item.transform.position.z < 10), Is.True, "Keep emission near the two front trees, not distant sakura.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void PetalsAreAlreadyVisibleAfterPrewarmAndContinueFalling()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                foreach (ParticleSystem emitter in FallingEmitters(Effect(scene)))
                {
                    emitter.Simulate(32, true, true, true);
                    var buffer = new ParticleSystem.Particle[emitter.main.maxParticles];
                    int count = emitter.GetParticles(buffer);
                    Assert.That(count, Is.GreaterThan(40), "Populate the air immediately instead of waiting for the first petals to fall.");
                    var before = buffer.Take(count).Where(petal => petal.remainingLifetime > 3).ToDictionary(petal => petal.randomSeed, petal => petal.position);
                    emitter.Simulate(2, true, false, true);
                    count = emitter.GetParticles(buffer);
                    float fall = 0; int matched = 0;
                    foreach (var petal in buffer.Take(count))
                        if (before.TryGetValue(petal.randomSeed, out Vector3 position)) { fall += petal.position.y - position.y; matched++; }
                    Assert.That(matched, Is.GreaterThan(20));
                    Assert.That(fall / matched, Is.LessThan(-0.1f), "Flutter should not turn falling petals into rising confetti.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void AddingPetalsAgainPreservesAuthoredSettingsAndDoesNotDuplicateTheEffect()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                ParticleSystem[] before = Effect(scene).GetComponentsInChildren<ParticleSystem>();
                Assert.That(before.Length, Is.EqualTo(4));
                var emission = before[0].emission; emission.rateOverTime = 3.5f;
                MainMenuSakuraPetals.ApplyToScene(scene);
                ParticleSystem[] after = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ParticleSystem>()).ToArray();
                Assert.That(after, Is.EquivalentTo(before));
                Assert.That(before[0].emission.rateOverTime.constant, Is.EqualTo(3.5f));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void FallingPetalsReachThePavementAndSettleFlatWithoutSlidingOrSinking()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                Transform effect = Effect(scene);
                Transform plane = effect.Find(MainMenuSakuraPetals.GroundName);
                Assert.That(plane, Is.Not.Null);
                foreach (ParticleSystem falling in FallingEmitters(effect))
                {
                    Assert.That(falling.collision.enabled, Is.True);
                    Assert.That(falling.collision.GetPlane(0), Is.SameAs(plane));
                    Assert.That(falling.collision.bounce.constant, Is.Zero);
                    Assert.That(falling.subEmitters.GetSubEmitterType(0), Is.EqualTo(ParticleSystemSubEmitterType.Collision));
                    Assert.That(falling.subEmitters.GetSubEmitterProperties(0) & ParticleSystemSubEmitterProperties.InheritColor, Is.EqualTo(ParticleSystemSubEmitterProperties.InheritNothing),
                        "A particle killed by ground collision has zero lifetime opacity; do not inherit its transparent color.");
                    var settled = falling.subEmitters.GetSubEmitterSystem(0);
                    Assert.That(settled.GetComponent<ParticleSystemRenderer>().renderMode, Is.EqualTo(ParticleSystemRenderMode.Mesh));
                    Assert.That(settled.main.startRotationX.constant, Is.EqualTo(Mathf.PI * 0.5f));
                    var main = falling.main; main.prewarm = false;
                    falling.Simulate(0.1f, true, true, true);
                    Assert.That(settled.particleCount, Is.Zero, "Only an actual landing should create a ground petal.");
                    main.prewarm = true;
                    falling.Simulate(40, true, true, true);
                    var buffer = new ParticleSystem.Particle[settled.main.maxParticles];
                    int count = settled.GetParticles(buffer);
                    Assert.That(count, Is.GreaterThan(2), "Some petals must actually reach the pavement rather than disappear in the air.");
                    Assert.That(buffer.Take(count).Count(petal => petal.GetCurrentColor(settled).a > 100), Is.GreaterThan(2),
                        "Particle count alone is not enough: ground petals must actually have visible opacity.");
                    foreach (var petal in buffer.Take(count))
                    {
                        Assert.That(petal.position.y, Is.EqualTo(plane.position.y).Within(0.015f));
                        Assert.That(petal.velocity.sqrMagnitude, Is.LessThan(0.0001f));
                    }
                    var before = buffer.Take(count).Where(petal => petal.remainingLifetime > 2).ToDictionary(petal => petal.randomSeed, petal => petal.position);
                    falling.Simulate(1, true, false, true);
                    count = settled.GetParticles(buffer);
                    int matched = 0;
                    foreach (var petal in buffer.Take(count))
                        if (before.TryGetValue(petal.randomSeed, out Vector3 position))
                        {
                            Assert.That(Vector3.Distance(petal.position, position), Is.LessThan(0.001f)); matched++;
                        }
                    Assert.That(matched, Is.GreaterThan(0));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void LegacyTransparentGroundPetalsAreRepairedWithoutDuplicatingTheEffect()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var effect = Effect(scene);
                foreach (var falling in FallingEmitters(effect))
                {
                    var children = falling.subEmitters;
                    children.SetSubEmitterProperties(0, ParticleSystemSubEmitterProperties.InheritColor | ParticleSystemSubEmitterProperties.InheritSize);
                    var main = children.GetSubEmitterSystem(0).main; main.startColor = Color.white;
                }
                MainMenuSakuraPetals.ApplyToScene(scene);
                Assert.That(effect.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(4));
                foreach (var falling in FallingEmitters(effect))
                {
                    Assert.That(falling.subEmitters.GetSubEmitterProperties(0), Is.EqualTo(ParticleSystemSubEmitterProperties.InheritSize));
                    var settled = falling.subEmitters.GetSubEmitterSystem(0);
                    Assert.That(settled.main.startColor.colorMin, Is.EqualTo(falling.main.startColor.colorMin));
                    Assert.That(settled.main.startColor.colorMax, Is.EqualTo(falling.main.startColor.colorMax));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void RestingPetalScatterIsOpaqueFlatAndVisibleFromTheMenuCameraImmediately()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                Transform resting = Effect(scene).Find(MainMenuSakuraPetals.RestingName);
                Assert.That(resting, Is.Not.Null);
                Mesh mesh = resting.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.EqualTo(96 * 4));
                Assert.That(mesh.colors.All(color => color.a > 0.9f), Is.True);
                Assert.That(mesh.vertices.All(point => resting.TransformPoint(point).y > 0.02f), Is.True);
                Assert.That(mesh.bounds.size.y, Is.LessThan(0.001f), "Rest the scatter flat, not facing the camera like confetti.");
                var renderer = resting.GetComponent<MeshRenderer>();
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.sharedMaterial.mainTexture, Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture2D>(MainMenuSakuraPetals.TexturePath)));
                var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single(item => item.CompareTag("MainCamera"));
                int visible = Enumerable.Range(0, 96).Count(i =>
                {
                    Vector3 center = (mesh.vertices[i * 4] + mesh.vertices[i * 4 + 2]) * 0.5f;
                    Vector3 viewport = camera.WorldToViewportPoint(resting.TransformPoint(center));
                    return viewport.z > 0 && viewport.x > 0.3f && viewport.x < 1 && viewport.y > 0 && viewport.y < 0.4f;
                });
                Assert.That(visible, Is.GreaterThan(25), "Keep many resting petals on visible pavement, clear of the left-hand menu UI.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Transform Effect(UnityEngine.SceneManagement.Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>()).Single(item => item.name == MainMenuSakuraPetals.RootName);

        private static ParticleSystem[] FallingEmitters(Transform effect) => effect.Cast<Transform>()
            .Select(child => child.GetComponent<ParticleSystem>()).Where(item => item != null).ToArray();
    }
}
