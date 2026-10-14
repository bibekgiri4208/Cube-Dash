using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>City-only, editor-baked streets. No procedural generation or additional colliders at runtime.</summary>
    public static class JapaneseCityBuilder
    {
        public const string ScenePath = "Assets/Scenes/City.unity";
        public const string ArtPath = "Assets/Maps/Japanese City";
        public const string StreetsName = "Japanese Streetscape";
        private static readonly string[] Signs = { "ラーメン", "東京", "ホテル", "寿司", "喫茶", "さくら", "本屋", "うどん" };
        private static readonly string[] Names = { "Ivory plaster", "Butter plaster", "Terracotta plaster", "Blue concrete",
            "Timber", "Roof tiles", "Window blue", "Window light", "Stone paving", "Paving joints", "Red lacquer",
            "Teal awning", "Paper white", "Iron", "Bark", "Sakura rose", "Sakura blush", "Sakura light", "Leaf green", "Lantern gold", "Cobalt signs", "Lettering" };
        private static readonly string[] Colors = { "DDCBB0", "E9CE83", "BA7760", "90AEC4", "54453E", "79463F", "345776",
            "6D9ABC", "D8CEC1", "ACAAA8", "D83E37", "318C8A", "FFF1DA", "49515C", "604638", "D76698", "F48BB5", "FFBED7", "528144", "FFC06B", "24569D", "FFF5E6" };
        private static Mesh cube, cylinder, blossom;
        private static Font font;

        [MenuItem("Tools/Cube Dash/Upgrade City to Japanese Sakura Street")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath);
            SceneManager.SetActiveScene(scene);
            EndlessTrack track = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndlessTrack>(true)).Single();
            if (!track.Environment.HasFixedBiome || track.Environment.FixedBiome != EnvironmentBiome.City)
                throw new InvalidOperationException("Only the standalone City map can be upgraded.");
            Transform[] roots = track.Segments.Select(segment => segment.Environment.Biomes[0].transform).ToArray();
            bool[] authored = roots.Select(root => root.Find(StreetsName) != null).ToArray();
            if (authored.All(value => value)) { Debug.Log("Japanese City is already saved. Manual art and scene edits preserved."); return; }
            if (authored.Any(value => value) || AssetDatabase.IsValidFolder(ArtPath))
                throw new InvalidOperationException("Partial Japanese City assets need inspection before authoring.");
            Shader signShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JapaneseCityLettering.shader");
            Shader skyShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JapaneseCitySky.shader");
            if (signShader == null || skyShader == null || ShaderUtil.ShaderHasError(signShader) || ShaderUtil.ShaderHasError(skyShader))
                throw new InvalidOperationException("Japanese City shaders must compile before authoring.");
            AssetDatabase.CreateFolder("Assets/Maps", "Japanese City");
            AssetDatabase.CreateFolder(ArtPath, "Materials"); AssetDatabase.CreateFolder(ArtPath, "Layouts");
            cube = Primitive(PrimitiveType.Cube); cylinder = Primitive(PrimitiveType.Cylinder);
            blossom = BlossomMesh();
            try
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "MS Gothic", "Noto Sans CJK JP" }, 96);
                if (font == null) throw new InvalidOperationException("Install a Japanese font (Yu Gothic or Noto Sans CJK JP) to author signage.");
                string characters = string.Concat(Signs) + "入口自動販売機";
                font.RequestCharactersInTexture(characters, 96, FontStyle.Bold);
                foreach (char c in characters.Distinct())
                    if (!font.GetCharacterInfo(c, out _, 96, FontStyle.Bold)) throw new InvalidOperationException("Missing Japanese glyph: " + c);
                Texture source = font.material.mainTexture;
                var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                var atlas = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                try
                {
                    Graphics.Blit(source, target); RenderTexture.active = target;
                    atlas.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); atlas.Apply();
                    File.WriteAllBytes(ArtPath + "/Japanese Glyphs.png", atlas.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(atlas); }
                AssetDatabase.ImportAsset(ArtPath + "/Japanese Glyphs.png");
                var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath + "/Japanese Glyphs.png");
                importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = true; importer.SaveAndReimport();
                Material[] palette = new Material[Names.Length];
                for (int i = 0; i < palette.Length; i++)
                {
                    ColorUtility.TryParseHtmlString("#" + Colors[i], out Color color);
                    var material = new Material(i == 21 ? signShader : Shader.Find("CubeDash/Environment Surface")) { name = "Japanese City " + Names[i], enableInstancing = true };
                    material.SetColor("_BaseColor", color);
                    if (i == 21) material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "/Japanese Glyphs.png"));
                    else
                    {
                        material.SetColor("_SecondaryColor", Color.Lerp(color, color * 0.86f, 0.45f));
                        material.SetFloat("_DetailType", i == 4 || i == 14 ? 4 : 0);
                        material.SetFloat("_DetailScale", 0.65f); material.SetFloat("_MicroNormalStrength", 0.015f);
                        material.SetFloat("_Smoothness", i == 6 || i == 7 ? 0.28f : 0.08f);
                    }
                    AssetDatabase.CreateAsset(material, ArtPath + "/Materials/" + Names[i] + ".mat"); palette[i] = material;
                }
                Mesh[] layouts = new Mesh[BiomeRules.SceneryLayoutCount];
                for (int i = 0; i < layouts.Length; i++)
                {
                    layouts[i] = BuildLayout(i); layouts[i].name = "Japanese Sakura Street " + (i + 1);
                    AssetDatabase.CreateAsset(layouts[i], ArtPath + "/Layouts/" + layouts[i].name + ".asset");
                }
                for (int i = 0; i < roots.Length; i++)
                {
                    Transform root = roots[i];
                    // Archive existing scene-local scenery, rather than delete it or change shared source assets.
                    foreach (Transform child in root) child.gameObject.SetActive(false);
                    var streets = new GameObject(StreetsName, typeof(MeshFilter), typeof(MeshRenderer)); streets.transform.SetParent(root, false);
                    streets.transform.localPosition = Vector3.down * root.localPosition.y;
                    MeshFilter filter = streets.GetComponent<MeshFilter>(); MeshRenderer renderer = streets.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = palette;
                    var scenery = root.GetComponent<BiomeScenery>(); var data = new SerializedObject(scenery);
                    data.FindProperty("details").objectReferenceValue = filter;
                    foreach (string field in new[] { "layouts", "entranceLayouts", "exitLayouts" }) Assign(data.FindProperty(field), layouts);
                    data.FindProperty("variety").objectReferenceValue = null; data.FindProperty("varietyLayouts").arraySize = 0;
                    data.ApplyModifiedPropertiesWithoutUndo(); scenery.SelectLayout(i - 1, track.Environment.SegmentsPerBiome, true);
                    var flyover = root.GetComponent<CityFlyover>();
                    if (flyover != null) { var f = new SerializedObject(flyover); f.FindProperty("deck").objectReferenceValue = null; f.FindProperty("profiles").arraySize = 0; f.ApplyModifiedPropertiesWithoutUndo(); }
                    var horizon = root.GetComponent<HorizonBackdrop>();
                    if (horizon != null) { var h = new SerializedObject(horizon); h.FindProperty("behind").objectReferenceValue = null; h.FindProperty("ahead").objectReferenceValue = null; h.ApplyModifiedPropertiesWithoutUndo(); }
                    var boundary = track.Segments[i].GetComponent<BiomeBoundaryBlend>();
                    if (boundary != null) { var b = new SerializedObject(boundary); b.FindProperty("cityBuildings").arraySize = 0; b.FindProperty("cityBuildingScales").arraySize = 0; b.ApplyModifiedPropertiesWithoutUndo(); }
                    var rise = track.Segments[i].GetComponent<SceneryRise>();
                    if (rise != null)
                    {
                        var r = new SerializedObject(rise); r.FindProperty("buildings").arraySize = 0; r.FindProperty("buildingHomes").arraySize = 0;
                        Assign(r.FindProperty("scenery"), new Object[] { renderer }); r.ApplyModifiedPropertiesWithoutUndo(); rise.Settle();
                    }
                }
                SetAtmosphere(track.Environment, skyShader);
                // Keep the distant streets crisp and avoid a dark cinematic frame.
                // Override only these effects, never the shared graphics profile or camera.
                var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Japanese City Clear Day";
                AssetDatabase.CreateAsset(profile, ArtPath + "/Clear Day.asset");
                var depth = profile.Add<DepthOfField>(true); depth.mode.Override(DepthOfFieldMode.Off);
                var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.08f);
                AssetDatabase.AddObjectToAsset(depth, profile); AssetDatabase.AddObjectToAsset(vignette, profile);
                var volume = new GameObject("Japanese City Clear Day", typeof(Volume)).GetComponent<Volume>();
                volume.isGlobal = true; volume.priority = 100; volume.sharedProfile = profile;
                EditorUtility.SetDirty(profile);
                JapaneseCityLighting.ApplyToScene(scene);
                AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Japanese City.");
                JapaneseCitySidewalks.ApplyFromCommandLine();
                Debug.Log("Japanese City saved: eight pooled sakura streets, authentic Japanese signs, shopfronts, utility wires, lanterns, vending machines, shelters and a clear blue sky. Gameplay, road, camera, HUD and other maps preserved.");
            }
            finally { Object.DestroyImmediate(blossom); if (font != null) Object.DestroyImmediate(font); font = null; }
        }

        private static void SetAtmosphere(EnvironmentDirector director, Shader shader)
        {
            var data = new SerializedObject(director); var a = data.FindProperty("atmospheres").GetArrayElementAtIndex(0);
            void ColorField(string name, string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); a.FindPropertyRelative(name).colorValue = c; }
            ColorField("Horizon", "88BDF5"); ColorField("Zenith", "176BE3"); ColorField("Ground", "A1B3C3");
            ColorField("Sun", "FFF0DC"); ColorField("AmbientSky", "B8D5F7"); ColorField("AmbientEquator", "B5BDCE"); ColorField("AmbientGround", "777582");
            a.FindPropertyRelative("SunIntensity").floatValue = 1.35f; data.ApplyModifiedPropertiesWithoutUndo();
            Material sky = new Material(shader) { name = "Japanese City Clear Sky" };
            sky.SetColor("_HorizonColor", a.FindPropertyRelative("Horizon").colorValue);
            sky.SetColor("_ZenithColor", a.FindPropertyRelative("Zenith").colorValue);
            sky.SetColor("_GroundColor", a.FindPropertyRelative("Ground").colorValue);
            // Retain the original asset path/GUID; the material no longer paints Fuji or clouds.
            AssetDatabase.CreateAsset(sky, ArtPath + "/Fuji Sky.mat"); RenderSettings.skybox = sky;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 75; RenderSettings.fogEndDistance = 340;
            RenderSettings.ambientMode = AmbientMode.Trilight; director.ApplyDistance(0, 0);
            var sun = data.FindProperty("sunlight").objectReferenceValue as Light;
            if (sun != null) { sun.transform.rotation = Quaternion.Euler(48, -32, 0); sun.shadowStrength = 0.7f; }
        }

        private static void Assign(SerializedProperty array, Object[] values)
        { array.arraySize = values.Length; for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
        private static Mesh Primitive(PrimitiveType type)
        { var go = GameObject.CreatePrimitive(type); Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(go); return mesh; }

        private static Mesh BlossomMesh()
        {
            // Eighty flat faces: enough silhouette detail without hundreds of tiny triangles per flower mass.
            float t = (1 + Mathf.Sqrt(5)) / 2;
            Vector3[] points = { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
            int[] faces = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var vertices = new List<Vector3>();
            void Face(Vector3 a, Vector3 b, Vector3 c)
            { foreach (Vector3 v in new[] { a, b, c }) vertices.Add(v.normalized * (0.5f + 0.025f * Mathf.Sin(v.normalized.x * 11 + v.normalized.z * 7))); }
            for (int i = 0; i < faces.Length; i += 3)
            {
                Vector3 a = points[faces[i]].normalized, b = points[faces[i + 1]].normalized, c = points[faces[i + 2]].normalized;
                Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                Face(a, ab, ca); Face(b, bc, ab); Face(c, ca, bc); Face(ab, bc, ca);
            }
            var mesh = new Mesh { name = "Faceted sakura cluster" }; mesh.SetVertices(vertices);
            mesh.SetTriangles(Enumerable.Range(0, vertices.Count).ToArray(), 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh BuildLayout(int layout)
        {
            var g = new StreetMesh(Names.Length); var random = new System.Random(7271 + layout * 193);
            for (int side = -1; side <= 1; side += 2)
            {
                g.Anchor = new Vector2(side * 12, 21);
                g.Box(9, side * 43, -0.28f, 21, 74, 0.5f, 42);
                g.Box(8, side * 8.4f, 0.01f, 21, 5.2f, 0.34f, 42);
                g.Box(12, side * 5.87f, 0.08f, 21, 0.18f, 0.45f, 42);
                for (int row = 0; row < 28; row++)
                    for (int col = 0; col < 3; col++)
                        g.Box(8, side * (6.65f + col * 1.65f), 0.19f, row * 1.5f + 0.75f, 1.58f, 0.045f, 1.43f);
                for (int slot = 0; slot < 4; slot++)
                {
                    int style = (layout * 3 + slot + (side > 0 ? 2 : 0)) % 8;
                    float z = 5.25f + slot * 10.5f;
                    Shop(g, side, z, style, 8 + (style % 3) * 1.6f);
                    if (slot % 2 == 0) Tree(g, new Vector3(side * 9.3f, 0.23f, z + 2.8f), random, 0.90f + (layout % 3) * 0.06f);
                    if (slot % 2 == 1) Vending(g, side, z - 3.5f, style);
                }
                // A second, offset row of restrained midrises gives depth rather than an identical wall of towers.
                for (int slot = 0; slot < 3; slot++)
                    if ((layout + slot + (side > 0 ? 1 : 0)) % 3 != 0)
                        Midrise(g, side * (24 + (layout + slot) % 3 * 3), 7 + slot * 14, 14 + ((layout + slot * 3) % 5) * 3.1f, layout + slot);
                for (int pole = 0; pole < 2; pole++) Pole(g, side, 3 + pole * 21);
                for (int hedge = 0; hedge < 7; hedge++)
                {
                    float z = hedge * 6 + 2; g.Anchor = new Vector2(side * 10.2f, z);
                    g.Box(4, side * 10.2f, 0.45f, z, 0.85f, 0.55f, 1.2f);
                    g.Add(blossom, 18, new Vector3(side * 10.2f, 1.1f, z), new Vector3(1.1f, 1.2f, 1.5f));
                }
                if ((layout + (side > 0 ? 1 : 0)) % 3 == 0) Shelter(g, side, 17);
                for (int petal = 0; petal < 45; petal++)
                {
                    float x = side * (6.2f + (float)random.NextDouble() * 4.2f), z = (float)random.NextDouble() * 42;
                    g.Anchor = new Vector2(x, z);
                    g.Add(blossom, 16 + petal % 2, new Vector3(x, 0.24f + (petal % 6 == 0 ? 2 + (float)random.NextDouble() * 7 : 0), z),
                        new Vector3(0.13f, 0.025f, 0.22f), Quaternion.Euler(petal % 6 == 0 ? 40 : 0, petal * 137, 0));
                }
            }
            if (layout == 5) Footbridge(g);
            return g.Finish();
        }

        private static void Shop(StreetMesh g, int side, float z, int style, float height)
        {
            Vector3 home = new Vector3(side * 14.2f, 0.2f, z); g.Anchor = new Vector2(home.x, z);
            g.Space = Matrix4x4.TRS(home, Quaternion.Euler(0, side * 90, 0), Vector3.one);
            int wall = style % 4;
            g.Box(wall, 0, height / 2, 0, 10.25f, height, 7);
            g.Box(4, 0, 0.22f, -3.6f, 10.5f, 0.45f, 0.3f);
            for (int edge = -1; edge <= 1; edge += 2) g.Box(4, edge * 4.92f, height / 2, -3.56f, 0.24f, height, 0.25f);
            g.Box(4, 0, 3.6f, -3.58f, 10.5f, 0.28f, 0.3f);
            g.Box(4, 0, height - 0.2f, -3.6f, 10.6f, 0.27f, 0.35f);
            for (int bay = -1; bay <= 1; bay++)
            {
                float x = bay * 3.1f;
                g.Box(4, x, 1.85f, -3.6f, 2.95f, 3.05f, 0.22f);
                g.Box(style % 3 == 0 ? 19 : 6, x, 1.9f, -3.75f, 2.65f, 2.75f, 0.1f);
                g.Box(4, x, 1.9f, -3.85f, 0.09f, 2.75f, 0.08f);
                g.Box(4, x, 1.45f, -3.85f, 2.65f, 0.1f, 0.08f);
                for (float y = 5.8f; y < height - 1; y += 3)
                {
                    g.Box(8, x, y, -3.6f, 2.5f, 2.2f, 0.22f);
                    g.Box(6, x, y, -3.75f, 2.22f, 1.95f, 0.13f);
                    g.Box(7, x - 0.55f, y + 0.25f, -3.84f, 0.9f, 1.3f, 0.025f);
                    g.Box(4, x, y, -3.87f, 0.1f, 2, 0.08f);
                    g.Box(8, x, y - 1.2f, -3.9f, 2.7f, 0.17f, 0.55f);
                }
            }
            bool red = style % 2 == 0;
            for (int stripe = 0; stripe < 16; stripe++)
            {
                int color = red ? 10 : stripe % 2 == 0 ? 11 : 12;
                g.Add(cube, color, new Vector3(-4.8f + stripe * 0.64f, 3.95f, -4.35f), new Vector3(0.65f, 0.17f, 1.8f), Quaternion.Euler(18, 0, 0));
                g.Box(color, -4.8f + stripe * 0.64f, 3.5f, -5.18f, 0.65f, 0.45f, 0.09f);
            }
            g.Box(red ? 10 : 11, 0, 4.8f, -3.7f, 7.7f, 1.15f, 0.24f);
            Text(g, Signs[style], new Vector3(0, 4.8f, -3.85f), 0.72f, false);
            // Shallow pitched roof, overhanging eaves and individual tile seams.
            if (style % 3 != 1)
            {
                for (int slope = -1; slope <= 1; slope += 2)
                    g.Add(cube, 5, new Vector3(0, height + 0.66f, slope * 1.95f), new Vector3(10.95f, 0.26f, 4.45f), Quaternion.Euler(-slope * 18, 0, 0));
                g.Box(5, 0, height + 1.35f, 0, 11.1f, 0.23f, 0.32f);
                for (int tile = 0; tile < 22; tile++)
                    g.Add(cube, 5, new Vector3(-5.3f + tile * 0.5f, height + 0.8f, -1.98f), new Vector3(0.065f, 0.08f, 4.45f), Quaternion.Euler(18, 0, 0));
            }
            else { g.Box(8, 0, height + 0.05f, 0, 10.7f, 0.35f, 7.5f); g.Box(3, 2, height + 0.8f, 1, 2.2f, 1.5f, 2); }
            g.Box(8, 3.5f, height - 1.6f, -3.9f, 1.25f, 0.9f, 0.7f);
            for (int vent = 0; vent < 5; vent++) g.Box(13, 3.5f, height - 1.9f + vent * 0.14f, -4.27f, 0.95f, 0.045f, 0.02f);
            // Ribbed paper lantern and dark end caps.
            g.Add(cylinder, 10, new Vector3(-4.2f, 2.6f, -4.4f), new Vector3(0.65f, 0.65f, 0.65f));
            for (int rib = 0; rib < 8; rib++) g.Add(cylinder, 19, new Vector3(-4.2f, 2.04f + rib * 0.16f, -4.4f), new Vector3(0.67f, 0.014f, 0.67f));
            g.Box(4, -4.2f, 3.35f, -4.4f, 0.5f, 0.12f, 0.5f);
            g.Space = Matrix4x4.identity;
            // Projecting blade signs face the approaching camera, not away down the street.
            float bx = side * 9.6f;
            g.Box(13, bx, 7.3f, z - 3.3f, 1.65f, 5.2f, 0.28f);
            g.Box(style % 3 == 0 ? 20 : style % 3 == 1 ? 10 : 4, bx, 7.3f, z - 3.47f, 1.43f, 4.95f, 0.08f);
            Text(g, Signs[style], new Vector3(bx, 7.3f, z - 3.53f), 0.97f, true);
        }

        private static void Midrise(StreetMesh g, float x, float z, float height, int style)
        {
            g.Anchor = new Vector2(x, z); g.Space = Matrix4x4.TRS(new Vector3(x, 0.2f, z), Quaternion.Euler(0, Mathf.Sign(x) * 90, 0), Vector3.one);
            g.Box(style % 2 == 0 ? 3 : 0, 0, height / 2, 0, 12.5f, height, 9.5f);
            for (int floor = 0; floor < height / 3; floor++)
            {
                float y = 1.65f + floor * 3;
                g.Box(8, 0, y + 1.3f, 0, 12.8f, 0.2f, 9.8f);
                for (int bay = 0; bay < 4; bay++)
                {
                    g.Box(6, -4.6f + bay * 3.05f, y, -4.82f, 2.65f, 2.1f, 0.12f);
                    g.Box(7, -5.1f + bay * 3.05f, y + 0.2f, -4.9f, 1.2f, 1.6f, 0.025f);
                }
                for (int bay = 0; bay < 3; bay++) g.Box(6, -6.31f, y, -3 + bay * 3, 0.12f, 2.1f, 2.4f);
                if (style % 3 == 0 && floor > 0)
                {
                    g.Box(8, 0, y - 1.1f, -5.3f, 12.8f, 0.2f, 1.4f);
                    g.Box(13, 0, y - 0.2f, -5.96f, 12.6f, 0.08f, 0.08f);
                    for (int rail = 0; rail < 16; rail++) g.Box(13, -6 + rail * 0.8f, y - 0.65f, -5.96f, 0.045f, 0.9f, 0.05f);
                }
            }
            g.Box(8, 0, height + 0.15f, 0, 13, 0.4f, 10);
            g.Box(3, 2, height + 1.2f, 0, 4, 2.4f, 4);
            g.Box(13, 2, height + 3, 0, 0.08f, 2.3f, 0.08f);
            g.Space = Matrix4x4.identity;
        }

        private static void Tree(StreetMesh g, Vector3 home, System.Random random, float scale)
        {
            g.Anchor = new Vector2(home.x, home.z); g.Space = Matrix4x4.TRS(home, Quaternion.Euler(0, (float)random.NextDouble() * 360, 0), Vector3.one * scale);
            g.Box(9, 0, 0, 0, 1.7f, 0.12f, 1.7f);
            g.Stem(14, Vector3.zero, new Vector3(0.12f, 3.5f, 0), 0.24f);
            for (int branch = 0; branch < 7; branch++)
            {
                float angle = branch * 137.5f * Mathf.Deg2Rad;
                Vector3 start = new Vector3(0.08f, 1.9f + branch * 0.19f, 0);
                Vector3 tip = new Vector3(Mathf.Cos(angle) * 2.05f, 4.4f + branch % 3 * 0.7f, Mathf.Sin(angle) * 2.05f);
                Vector3 fork = Vector3.Lerp(start, tip, 0.57f);
                g.Stem(14, start, fork, 0.14f); g.Stem(14, fork, tip, 0.085f);
                for (int cluster = 0; cluster < 7; cluster++)
                {
                    Vector3 crown = tip + new Vector3(((float)random.NextDouble() - 0.5f) * 1.7f, ((float)random.NextDouble() - 0.25f) * 1.3f, ((float)random.NextDouble() - 0.5f) * 1.7f);
                    float size = 0.8f + (float)random.NextDouble() * 0.45f;
                    g.Add(blossom, 15 + (branch + cluster) % 3, crown, new Vector3(1.4f, 0.95f, 1.35f) * size, Quaternion.Euler(cluster * 11, branch * 41, cluster * 19));
                }
            }
            g.Space = Matrix4x4.identity;
        }

        private static void Pole(StreetMesh g, int side, float z)
        {
            float x = side * 7.05f; g.Anchor = new Vector2(x, z);
            g.Stem(9, new Vector3(x, 0.2f, z), new Vector3(x, 11.7f, z), 0.115f);
            for (int cross = 0; cross < 2; cross++)
            {
                g.Box(4, x, 10.6f + cross * 0.7f, z, 2.35f, 0.14f, 0.16f);
                for (int wire = 0; wire < 3; wire++)
                {
                    float wx = x - 0.9f + wire * 0.9f, wy = 10.8f + cross * 0.7f;
                    g.Box(8, wx, wy, z, 0.16f, 0.28f, 0.16f);
                    for (int step = 0; step < 8; step++)
                    {
                        float a = step / 8f, b = (step + 1) / 8f;
                        g.Stem(13, new Vector3(wx, wy - Mathf.Sin(a * Mathf.PI) * 0.6f, z + a * 21),
                            new Vector3(wx, wy - Mathf.Sin(b * Mathf.PI) * 0.6f, z + b * 21), 0.018f);
                    }
                }
            }
            g.Box(13, x - side * 0.65f, 8.4f, z, 1.45f, 0.12f, 0.17f);
            g.Box(8, x - side * 1.35f, 8.28f, z, 0.85f, 0.22f, 0.6f);
            g.Box(19, x - side * 1.35f, 8.15f, z, 0.65f, 0.035f, 0.45f);
        }

        private static void Vending(StreetMesh g, int side, float z, int style)
        {
            g.Anchor = new Vector2(side * 10, z); g.Space = Matrix4x4.TRS(new Vector3(side * 10.1f, 0.22f, z), Quaternion.Euler(0, side * 65, 0), Vector3.one);
            g.Box(style % 2 == 0 ? 20 : 10, 0, 1.15f, 0, 1.25f, 2.3f, 0.9f);
            g.Box(12, -0.08f, 1.58f, -0.47f, 1, 1.2f, 0.04f);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 5; col++)
                {
                    g.Box((col + row) % 3 == 0 ? 10 : (col + row) % 3 == 1 ? 20 : 19, -0.45f + col * 0.18f, 1.16f + row * 0.32f, -0.51f, 0.11f, 0.23f, 0.06f);
                    g.Box(13, -0.45f + col * 0.18f, 1 + row * 0.32f, -0.51f, 0.12f, 0.035f, 0.03f);
                }
            g.Box(13, 0, 0.47f, -0.48f, 0.8f, 0.24f, 0.06f); g.Box(19, 0.45f, 0.85f, -0.51f, 0.11f, 0.22f, 0.035f);
            g.Space = Matrix4x4.identity;
        }

        private static void Shelter(StreetMesh g, int side, float z)
        {
            float x = side * 7.8f; g.Anchor = new Vector2(x, z);
            for (int end = -1; end <= 1; end += 2) g.Box(13, x, 1.8f, z + end * 2, 0.09f, 3.3f, 0.09f);
            g.Box(13, x, 3.5f, z, 2, 0.18f, 4.5f);
            g.Box(7, x + side * 0.06f, 1.9f, z, 0.04f, 2.6f, 3.9f);
            g.Box(4, x - side * 0.4f, 0.85f, z, 0.8f, 0.18f, 3.3f);
            g.Box(12, x, 2.1f, z - 1.98f, 1.2f, 1.9f, 0.07f);
            g.Box(11, x, 2.2f, z - 2.03f, 0.8f, 1.1f, 0.02f);
        }

        private static void Footbridge(StreetMesh g)
        {
            g.Anchor = new Vector2(12, 29);
            for (int side = -1; side <= 1; side += 2)
            {
                g.Box(8, side * 11.7f, 4.1f, 29, 0.8f, 8.2f, 1.1f);
                g.Box(20, side * 11.7f, 7.3f, 28.4f, 0.32f, 1.1f, 0.08f);
            }
            g.Box(8, 0, 8.5f, 29, 25, 0.65f, 2.6f);
            for (int side = -1; side <= 1; side += 2)
            {
                g.Box(13, 0, 9.65f, 29 + side * 1.2f, 25, 0.09f, 0.08f);
                for (int post = 0; post < 26; post++) g.Box(13, -12.3f + post * 0.98f, 9.25f, 29 + side * 1.2f, 0.045f, 0.8f, 0.05f);
            }
        }

        private static void Text(StreetMesh g, string text, Vector3 center, float size, bool vertical)
        {
            for (int i = 0; i < text.Length; i++)
            {
                font.GetCharacterInfo(text[i], out CharacterInfo glyph, 96, FontStyle.Bold);
                Vector3 origin = center + (vertical ? Vector3.up * ((text.Length - 1) * 0.5f - i) * size * 1.12f
                    : Vector3.right * (i - (text.Length - 1) * 0.5f) * size * 1.06f);
                float scale = size / 96f;
                float middleX = (glyph.minX + glyph.maxX) * 0.5f, middleY = (glyph.minY + glyph.maxY) * 0.5f;
                g.Quad(21, origin + new Vector3((glyph.minX - middleX) * scale, (glyph.minY - middleY) * scale, 0),
                    origin + new Vector3((glyph.minX - middleX) * scale, (glyph.maxY - middleY) * scale, 0),
                    origin + new Vector3((glyph.maxX - middleX) * scale, (glyph.maxY - middleY) * scale, 0),
                    origin + new Vector3((glyph.maxX - middleX) * scale, (glyph.minY - middleY) * scale, 0),
                    glyph.uvBottomLeft, glyph.uvTopLeft, glyph.uvTopRight, glyph.uvBottomRight);
            }
        }

        private sealed class StreetMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>(), anchors = new List<Vector2>();
            private readonly List<int>[] triangles;
            public Matrix4x4 Space = Matrix4x4.identity;
            public Vector2 Anchor;
            public StreetMesh(int count) { triangles = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray(); }
            public void Box(int surface, float x, float y, float z, float w, float h, float d)
                => Add(cube, surface, new Vector3(x, y, z), new Vector3(w, h, d));
            public void Stem(int surface, Vector3 start, Vector3 end, float radius)
                => Add(cylinder, surface, (start + end) * 0.5f, new Vector3(radius * 2, Vector3.Distance(start, end) / 2, radius * 2), Quaternion.FromToRotation(Vector3.up, end - start));
            public void Add(Mesh mesh, int surface, Vector3 position, Vector3 scale, Quaternion rotation = default)
            {
                if (rotation.Equals(default(Quaternion))) rotation = Quaternion.identity;
                Matrix4x4 m = Space * Matrix4x4.TRS(position, rotation, scale), n = m.inverse.transpose;
                int first = vertices.Count; Vector3[] v = mesh.vertices, normal = mesh.normals; Vector2[] tex = mesh.uv;
                for (int i = 0; i < v.Length; i++)
                { vertices.Add(m.MultiplyPoint3x4(v[i])); normals.Add(n.MultiplyVector(normal[i]).normalized); uv.Add(tex.Length > i ? tex[i] : Vector2.zero); anchors.Add(Anchor); }
                foreach (int index in mesh.triangles) triangles[surface].Add(first + index);
            }
            public void Quad(int surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int first = vertices.Count;
                foreach (Vector3 v in new[] { a, b, c, d }) { vertices.Add(Space.MultiplyPoint3x4(v)); normals.Add(Space.MultiplyVector(Vector3.back)); anchors.Add(Anchor); }
                uv.AddRange(new[] { ua, ub, uc, ud }); triangles[surface].AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            public Mesh Finish()
            {
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 }; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(2, anchors);
                mesh.subMeshCount = triangles.Length; for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
                mesh.RecalculateBounds();
                Bounds bounds = mesh.bounds; bounds.Encapsulate(bounds.min + Vector3.down * 80); mesh.bounds = bounds;
                return mesh;
            }
        }
    }
}
