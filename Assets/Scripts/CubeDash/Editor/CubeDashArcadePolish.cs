using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Non-destructive polish of the existing level; all feedback assets are saved.</summary>
    public static class CubeDashArcadePolish
    {
        private static readonly Color Navy = new Color(0.035f, 0.075f, 0.13f, 0.94f);
        private static readonly Color White = new Color(0.94f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.64f, 0.77f, 0.83f);
        private static readonly Color Coral = new Color(1f, 0.31f, 0.23f);
        private static Sprite rounded;

        [MenuItem("Tools/Cube Dash/Apply Arcade Polish")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("The authored Cube Dash level is required.");
            UpgradeLighting(game);
            UpgradePlayer(game);
            UpgradeTrail();
            AuthorAudio(game);
            rounded = RoundedPanel();
            UpgradeHud(Object.FindAnyObjectByType<RunnerHud>());
            CubeDashDesktopControls.ApplyToScene(Object.FindAnyObjectByType<RunnerHud>());
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Arcade polish saved: selective bloom, ambient bounce/fill, luminous ribbon, arcade pickup audio and animated HUD.");
        }

        private static void UpgradePlayer(CubeDashGame game)
        {
            const string path = "Assets/Prefab/CubeDash/PlayerCube.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visual = root.transform.Find("Cube Visual");
                if (visual == null)
                {
                    visual = new GameObject("Cube Visual", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    visual.SetParent(root.transform, false);
                    visual.GetComponent<MeshFilter>().sharedMesh = root.GetComponent<MeshFilter>().sharedMesh;
                    visual.GetComponent<Renderer>().sharedMaterial = root.GetComponent<Renderer>().sharedMaterial;
                }
                root.GetComponent<Renderer>().enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Set(game, "playerVisual", game.Player.Find("Cube Visual"));
        }

        private static void UpgradeLighting(CubeDashGame game)
        {
            foreach (string colorName in new[] { "Red", "Blue", "Green" })
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDash" + colorName + ".mat");
                material.SetFloat("_Smoothness", 0.38f);
                material.SetFloat("_SpecularHighlights", 1);
                material.SetFloat("_EnvironmentReflections", 1);
                material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", material.color * 0.75f);
                // URP requires an emissive flag to preserve _EMISSION on reimport. The cubes
                // are not lightmap-static, so this does not bake recycled geometry into a map.
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                EditorUtility.SetDirty(material);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.80f, 0.91f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.70f, 0.73f);
            RenderSettings.ambientGroundColor = new Color(0.43f, 0.47f, 0.49f);
            RenderSettings.reflectionIntensity = 0.7f;
            Light key = GameObject.Find("Directional Light").GetComponent<Light>();
            key.color = new Color(1f, 0.91f, 0.80f);
            key.intensity = 1.85f;
            key.bounceIntensity = 1.2f;
            key.shadows = LightShadows.Soft;
            Transform fillRoot = GameObject.Find("Sky Bounce Fill")?.transform;
            if (fillRoot == null) fillRoot = new GameObject("Sky Bounce Fill").transform;
            Light fill = fillRoot.GetComponent<Light>();
            if (fill == null) fill = fillRoot.gameObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.61f, 0.78f, 1f);
            fill.intensity = 0.28f;
            fill.shadows = LightShadows.None;
            fillRoot.rotation = Quaternion.Euler(32, 145, 0);
            game.GameCamera.allowHDR = true;
            game.GameCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/CubeDashVisuals.asset");
            if (profile.TryGet(out Bloom bloom))
            {
                bloom.active = true;
                bloom.threshold.Override(1.05f);
                bloom.intensity.Override(0.38f);
                bloom.scatter.Override(0.68f);
                bloom.highQualityFiltering.Override(true);
                bloom.clamp.Override(12f);
                EditorUtility.SetDirty(bloom);
            }
            if (profile.TryGet(out ColorAdjustments color))
            {
                color.contrast.Override(6);
                color.saturation.Override(5);
                color.postExposure.Override(0.05f);
                EditorUtility.SetDirty(color);
            }
            if (profile.TryGet(out Vignette vignette))
            {
                vignette.intensity.Override(0.12f);
                vignette.smoothness.Override(0.55f);
                EditorUtility.SetDirty(vignette);
            }
            EditorUtility.SetDirty(profile);
        }

        private static void UpgradeTrail()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashWake.mat");
            material.SetColor("_Tint", new Color(1.45f, 0.38f, 0.30f, 0.6f));
            EditorUtility.SetDirty(material);
            const string path = "Assets/Prefab/CubeDash/PlayerTrail.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CubeWake wake = root.GetComponent<CubeWake>();
                for (int i = 0; i < wake.Pieces.Length; i++)
                {
                    float t = 1f - (i + 0.5f) / wake.Pieces.Length;
                    wake.Pieces[i].localPosition = new Vector3(0, 0.024f, -0.7f - i * 0.25f);
                    wake.Pieces[i].localScale = new Vector3(0.85f * Mathf.Pow(t, 0.7f), 0.012f, 0.265f);
                    Renderer renderer = wake.Pieces[i].GetComponent<Renderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AuthorAudio(CubeDashGame game)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
            const string path = "Assets/Audio/ArcadeCollect.wav";
            // Original short rising chiptune arpeggio, with click-free envelopes and a soft sine body.
            const int sampleRate = 44100;
            const float duration = 0.24f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] notes = { 659.255f, 987.767f, 1318.51f, 1975.53f };
            using (var writer = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
                writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float time = i / (float)sampleRate;
                    float value = 0;
                    for (int note = 0; note < notes.Length; note++)
                    {
                        float age = time - note * 0.037f;
                        if (age < 0) continue;
                        float envelope = Mathf.Min(1, age / 0.003f) * Mathf.Exp(-age * 34f);
                        float phase = age * notes[note] * Mathf.PI * 2;
                        float tone = Mathf.Sin(phase) + Mathf.Sin(phase * 3) * 0.22f + Mathf.Sin(phase * 5) * 0.08f;
                        value += tone * envelope * 0.28f;
                    }
                    value *= Mathf.Clamp01((duration - time) / 0.015f);
                    writer.Write((short)(Mathf.Clamp(value, -0.95f, 0.95f) * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
            AudioSource source = game.GetComponent<AudioSource>();
            if (source == null) source = game.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.priority = 32;
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Set(game, "collectionAudio", source);
            Set(game, "collectionSound", source.clip);
        }

        private static Sprite RoundedPanel()
        {
            const string path = "Assets/2D Images/ArcadePanel.png";
            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    Vector2 p = new Vector2(Mathf.Abs(x - 31.5f), Mathf.Abs(y - 31.5f));
                    Vector2 corner = new Vector2(Mathf.Max(p.x - 17.5f, 0), Mathf.Max(p.y - 17.5f, 0));
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(14f - corner.magnitude)));
                }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(16, 16, 16, 16);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void UpgradeHud(RunnerHud hud)
        {
            Transform safe = hud.transform.Find("Safe Area");
            foreach (Text text in safe.GetComponentsInChildren<Text>(true)) text.color = White;
            foreach (Button button in safe.GetComponentsInChildren<Button>(true))
            {
                Panel(button.GetComponent<Image>(), Navy);
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.93f, 0.88f);
                colors.pressedColor = new Color(0.72f, 0.78f, 0.85f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                if (button.GetComponent<ArcadeButton>() == null) button.gameObject.AddComponent<ArcadeButton>();
            }
            RectTransform hudPanel = GetRect("Score Panel", safe);
            Place(hudPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(235, 124));
            Panel(Ensure<Image>(hudPanel), Navy);
            hudPanel.SetAsFirstSibling();
            Text brand = safe.Find("Brand").GetComponent<Text>();
            brand.text = "CUBE / DASH"; brand.fontSize = 15; brand.color = Muted;
            Place(brand.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(38, -42), new Vector2(200, 25));
            Text score = safe.Find("Distance").GetComponent<Text>();
            score.text = "0 POINTS";
            score.fontSize = 32; score.fontStyle = FontStyle.Bold;
            Place(score.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(38, -80), new Vector2(205, 48));
            Text best = safe.Find("Best").GetComponent<Text>();
            best.text = "BEST  0";
            best.fontSize = 14; best.color = Muted;
            Place(best.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, -120), new Vector2(190, 24));
            Text status = safe.Find("Speed").GetComponent<Text>();
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            status.text = "COLLECT " + game.PlayerCubeColor.ToString().ToUpperInvariant() + "  ·  0 m";
            status.color = game.Track.ColorMaterial(game.PlayerCubeColor).color;
            status.fontSize = 17; status.alignment = TextAnchor.MiddleCenter;
            Place(status.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(285, 34));
            RectTransform statusPanel = GetRect("Color Objective Panel", safe);
            Place(statusPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(310, 46));
            Panel(Ensure<Image>(statusPanel), Navy); statusPanel.SetAsFirstSibling();
            Text feedback = Ensure<Text>(GetRect("Pickup Feedback", safe));
            feedback.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            feedback.fontSize = 24; feedback.fontStyle = FontStyle.Bold;
            feedback.color = new Color(1f, 0.78f, 0.4f); feedback.raycastTarget = false;
            Place(feedback.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(194, -70), new Vector2(64, 36));
            feedback.gameObject.SetActive(false); Set(hud, "collectionFeedback", feedback);
            Transform controls = safe.Find("Lane Controls");
            controls.Find("Controls Hint").GetComponent<Text>().color = new Color(0.12f, 0.24f, 0.30f);
            controls.Find("Left").GetComponentInChildren<Text>().fontSize = 30;
            controls.Find("Right").GetComponentInChildren<Text>().fontSize = 30;

            Transform overlay = safe.Find("Menu Overlay");
            Ensure<CanvasGroup>(overlay);
            overlay.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.16f);
            RectTransform card = (RectTransform)overlay.Find("Menu Card");
            Place(card, new Vector2(0.5f, 0.60f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540, 360));
            Panel(card.GetComponent<Image>(), Navy);
            card.Find("Accent").gameObject.SetActive(true);
            card.Find("Accent").GetComponent<Image>().color = Coral;
            Text eyebrow = card.Find("Eyebrow").GetComponent<Text>();
            eyebrow.text = "COLOR MATCH / ENDLESS ARCADE"; eyebrow.color = Muted; eyebrow.fontSize = 13;
            Place(eyebrow.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -32), new Vector2(380, 26));
            Text heading = card.Find("Heading").GetComponent<Text>();
            heading.resizeTextMaxSize = 48;
            Place(heading.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -86), new Vector2(500, 76));
            Text description = card.Find("Description").GetComponent<Text>();
            description.fontSize = 18; description.color = Muted;
            Place(description.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(470, 84));
            RectTransform primary = (RectTransform)card.Find("Primary Action");
            Place(primary, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 104), new Vector2(280, 54));
            Panel(primary.GetComponent<Image>(), Coral);
            primary.GetComponentInChildren<Text>().text = "TAP TO START";
            card.Find("Restart").GetComponentInChildren<Text>().color = Muted;
            Place((RectTransform)card.Find("Restart"), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 54), new Vector2(280, 34));
            card.Find("Restart").gameObject.SetActive(false);
            Text footer = card.Find("Keyboard Hint").GetComponent<Text>();
            footer.color = Muted; footer.fontSize = 12;
            Place(footer.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 21), new Vector2(450, 24));

            Transform end = safe.Find("Game Over Overlay");
            Ensure<CanvasGroup>(end);
            end.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.28f);
            Transform composition = end.Find("Abstract Composition");
            Panel(Ensure<Image>(composition), Navy);
            ((RectTransform)composition).sizeDelta = new Vector2(340, 370);
            composition.Find("Score Caption").GetComponent<Text>().color = Muted;
            composition.Find("Tap Hint").GetComponent<Text>().color = Muted;
            Panel(composition.Find("Retry").GetComponent<Image>(), Coral);
            end.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
        }

        private static void Panel(Image image, Color color)
        {
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            // Decorative surfaces must not intercept the existing button events.
            image.raycastTarget = image.GetComponent<Button>() != null;
        }

        private static T Ensure<T>(Transform transform) where T : Component
        {
            T component = transform.GetComponent<T>();
            return component != null ? component : transform.gameObject.AddComponent<T>();
        }
        private static RectTransform GetRect(string name, Transform parent)
        {
            RectTransform rect = parent.Find(name) as RectTransform;
            if (rect != null) return rect;
            rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private static void Set(Object target, string name, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
