using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CubeDash.Tests
{
    public sealed class ShadowQualityTests
    {
        [Test]
        public void PcShadowsCoverTheVisibleTrackAndKeepThePlayerInTheSharpestCascade()
        {
            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Settings/PC_RPAsset.asset");
            Assert.That(asset.mainLightShadowmapResolution, Is.EqualTo(4096));
            Assert.That(asset.shadowDistance, Is.EqualTo(160f));
            Assert.That(asset.shadowCascadeCount, Is.EqualTo(4));
            Assert.That(asset.supportsSoftShadows, Is.True);
            Assert.That(asset.cascade4Split, Is.EqualTo(new Vector3(0.075f, 0.2f, 0.45f)));
            Assert.That(asset.cascade4Split.x * asset.shadowDistance, Is.GreaterThanOrEqualTo(12f));
            Assert.That(asset.shadowNormalBias, Is.EqualTo(0.25f));
        }

        [Test]
        public void MobileShadowsUseALighterTwoCascadeSoftShadowPreset()
        {
            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Settings/Mobile_RPAsset.asset");
            Assert.That(asset.mainLightShadowmapResolution, Is.EqualTo(2048));
            Assert.That(asset.shadowDistance, Is.EqualTo(90f));
            Assert.That(asset.shadowCascadeCount, Is.EqualTo(2));
            Assert.That(asset.supportsSoftShadows, Is.True);
            Assert.That(asset.cascade2Split, Is.EqualTo(0.18f));
            Assert.That(asset.shadowNormalBias, Is.EqualTo(0.25f));
        }

        [Test]
        public void SoftShadowFilteringIsHighOnPcAndMediumOnMobile()
        {
            UniversalRenderPipelineAsset pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Settings/PC_RPAsset.asset");
            UniversalRenderPipelineAsset mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Settings/Mobile_RPAsset.asset");
            Assert.That(SoftShadowTier(pc), Is.EqualTo((int)SoftShadowQuality.High));
            Assert.That(SoftShadowTier(mobile), Is.EqualTo((int)SoftShadowQuality.Medium));
        }

        private static int SoftShadowTier(Object asset)
        {
            var serialized = new UnityEditor.SerializedObject(asset);
            return serialized.FindProperty("m_SoftShadowQuality").intValue;
        }

        [Test]
        public void MainCameraAndFogRenderTheDistantSkylineBeyondTheShadowDistance()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                Camera camera = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Camera found = root.GetComponentInChildren<Camera>(true);
                    if (found != null) camera = found;
                }
                Assert.That(camera, Is.Not.Null);
                Assert.That(camera.farClipPlane, Is.GreaterThanOrEqualTo(600f));

                // RenderSettings belongs to the active scene, which a preview scene cannot become,
                // so verify the saved fog block directly in the authored scene file.
                string yaml = System.IO.File.ReadAllText("Assets/Scenes/Level.unity");
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(yaml, @"m_Fog: 1\r?\n  m_FogColor:"), Is.True);
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(yaml, @"m_FogMode: 1\r?\n"), Is.True, "Linear fog expected.");
                var fogEndMatch = System.Text.RegularExpressions.Regex.Match(yaml, @"m_LinearFogEnd: ([\d.]+)");
                Assert.That(fogEndMatch.Success, Is.True);
                float fogEnd = float.Parse(fogEndMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                // Fog fades out beyond the 160 m shadow distance but before the camera clips geometry.
                Assert.That(fogEnd, Is.GreaterThanOrEqualTo(260f));
                Assert.That(fogEnd, Is.GreaterThanOrEqualTo(160f));
                Assert.That(fogEnd, Is.LessThan(camera.farClipPlane));
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
