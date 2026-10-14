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
                ParticleSystem[] emitters = effect.GetComponentsInChildren<ParticleSystem>();
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
                foreach (ParticleSystem emitter in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ParticleSystem>()))
                {
                    emitter.Simulate(22, true, true, true);
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
                ParticleSystem[] before = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ParticleSystem>()).ToArray();
                Assert.That(before.Length, Is.EqualTo(2));
                var emission = before[0].emission; emission.rateOverTime = 3.5f;
                MainMenuSakuraPetals.ApplyToScene(scene);
                ParticleSystem[] after = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ParticleSystem>()).ToArray();
                Assert.That(after, Is.EquivalentTo(before));
                Assert.That(before[0].emission.rateOverTime.constant, Is.EqualTo(3.5f));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
