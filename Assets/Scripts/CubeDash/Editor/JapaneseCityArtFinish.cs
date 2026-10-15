using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>City-only surface art and cleanup. Saved assets, no runtime generation or shared graphics edits.</summary>
    public static class JapaneseCityArtFinish
    {
        public const string TexturePath = JapaneseCityBuilder.ArtPath + "/City Surface Grain.png";

        [MenuItem("Tools/Cube Dash/Finish City Materials Lighting and Remove Frozen Petals")]
        public static void ApplyFromMenu() => JapaneseCityLighting.ApplyFromMenu();
        public static void ApplyFromCommandLine() => JapaneseCityLighting.ApplyFromCommandLine();

        internal static void ApplyToScene(Scene scene)
        {
            if (scene.path != JapaneseCityBuilder.ScenePath) throw new InvalidOperationException("Only standalone City may be finished.");
            Texture2D texture = SurfaceTexture();
            var palette = new Dictionary<string, string>
            {
                { "Ivory plaster", "DED3BA" }, { "Butter plaster", "CBBB91" }, { "Terracotta plaster", "B98670" },
                { "Blue concrete", "82969C" }, { "Timber", "624838" }, { "Roof tiles", "45505A" },
                { "Window blue", "244352" }, { "Window light", "7CABB6" }, { "Red lacquer", "A63132" },
                { "Teal awning", "377F7C" }, { "Paper white", "EDE1C8" }, { "Cobalt signs", "325D79" },
                { "Iron", "39444B" }, { "Lantern gold", "EFBF72" }, { "Leaf green", "416952" },
                { "Stone paving", "B6B2A5" }, { "Paving joints", "6E726E" },
                { "Sidewalk sandstone", "C3B69D" }, { "Sidewalk pale limestone", "D4C9B0" },
                { "Sidewalk honey stone", "BDAA8E" }, { "Sidewalk recessed joints", "817D70" },
                { "Sidewalk precast curb", "B6B6AC" }, { "Sidewalk planting soil", "494237" },
                { "Sidewalk drainage stone", "535B58" }, { "Reference Asphalt", "4A494D" },
                { "Reference Road Shoulder", "48494B" }, { "Reference Ivory Markings", "E6DFCA" },
                { "Reference Barrier Ivory", "D6D2BC" }, { "Reference Barrier Mint", "6EAAA0" },
                { "Reference Rail Steel", "6C7B81" }, { "Reference Pedestrian Iron", "38494C" }
            };
            foreach (string folder in new[] { "/Materials", "/Reference Finish" })
            {
                if (!AssetDatabase.IsValidFolder(JapaneseCityBuilder.ArtPath + folder)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { JapaneseCityBuilder.ArtPath + folder }))
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (material.shader.name != "CubeDash/Japanese City Surface") continue;
                    string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                    if (palette.TryGetValue(name, out string hex)) material.SetColor("_BaseColor", Hex(hex));
                    material.SetTexture("_SurfaceMap", texture);
                    material.SetFloat("_DetailStrength", 0.65f); material.SetFloat("_ReliefStrength", 0.015f);
                    material.SetFloat("_OcclusionStrength", 0.95f);
                    material.SetColor("_ShadeColor", new Color(0.79f, 0.82f, 0.86f));
                    if (name == "Roof tiles") Style(material, 8, 0.28f, 0.05f);
                    if (name == "Reference Asphalt" || name == "Reference Road Shoulder")
                    {
                        Style(material, 9, 0.10f, 0); material.SetFloat("_ReliefStrength", 0.012f);
                        material.SetColor("_ShadeColor", Color.white);
                    }
                    if (name.StartsWith("Reference Barrier", StringComparison.Ordinal) || name == "Reference Ivory Markings")
                    { Style(material, 10, 0.23f, 0); material.SetColor("_ShadeColor", Color.white); }
                    if (name.Contains("Rail Steel") || name.Contains("Pedestrian Iron")) Style(material, 6, 0.48f, 0.55f);
                    if (name == "Teal awning" || name == "Paper white") Style(material, 11, 0.10f, 0);
                    if (name == "Red lacquer") Style(material, 10, 0.31f, 0);
                    if (name == "Lantern gold") material.SetFloat("_EmissionStrength", 0.65f);
                    EditorUtility.SetDirty(material);
                }
            }
            foreach (string path in new[] { JapaneseCityLowPolySakura.BarkPath, JapaneseCityLowPolySakura.CanopyPath })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                material.SetTexture("_SurfaceMap", texture);
                material.SetFloat("_BarkDetail", path == JapaneseCityLowPolySakura.BarkPath ? 1 : 0);
                material.SetColor("_BaseColor", path == JapaneseCityLowPolySakura.CanopyPath ? new Color(1, 0.94f, 0.96f) : Color.white);
                EditorUtility.SetDirty(material);
            }
            var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
            int removed = 0;
            foreach (Mesh mesh in track.Segments[0].Environment.Biomes[0].GetComponent<BiomeScenery>().Layouts)
                removed += RemoveAirbornePetals(mesh);
            Debug.Log("City finish: mineral/wood/asphalt textures, coordinated warm plaster/indigo/teal palette. Removed " + removed + " airborne petal triangles; grounded scatter and tree canopies retained.");
        }

        public static int RemoveAirbornePetals(Mesh mesh)
        {
            if (!AssetDatabase.GetAssetPath(mesh).StartsWith(JapaneseCityBuilder.ArtPath + "/Layouts/", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to edit a non-City layout.");
            Vector3[] vertices = mesh.vertices; int removed = 0;
            // These slots contain loose petals only, not the standalone sakura canopies. Keep grounded petals.
            foreach (int surface in new[] { 16, 17 })
            {
                int[] old = mesh.GetTriangles(surface); var keep = new List<int>(old.Length);
                for (int i = 0; i < old.Length; i += 3)
                {
                    if (Mathf.Max(vertices[old[i]].y, vertices[old[i + 1]].y, vertices[old[i + 2]].y) > 0.5f) removed++;
                    else { keep.Add(old[i]); keep.Add(old[i + 1]); keep.Add(old[i + 2]); }
                }
                if (keep.Count != old.Length) mesh.SetTriangles(keep, surface, false);
            }
            // Preserve original vertex attributes, reveal bounds, submesh slots and mesh GUIDs exactly.
            if (removed > 0) EditorUtility.SetDirty(mesh);
            return removed;
        }

        private static void Style(Material material, int type, float smoothness, float metallic)
        { material.SetFloat("_SurfaceType", type); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", metallic); }
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }

        private static Texture2D SurfaceTexture()
        {
            var saved = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (saved != null) return saved;
            if (File.Exists(TexturePath)) throw new InvalidOperationException("Import or inspect the existing City surface texture before authoring.");
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false, true);
            var pixels = new Color32[size * size];
            float Hash(int x, int y, int period)
            {
                uint n = (uint)(((x % period + period) % period) * 1973 + ((y % period + period) % period) * 9277 + 89173);
                n = (n ^ (n >> 13)) * 1274126177u; return (n & 65535) / 65535f;
            }
            float Noise(float x, float y, int period)
            {
                int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                float fx = x - ix, fy = y - iy; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, period), Hash(ix + 1, iy, period), fx),
                    Mathf.Lerp(Hash(ix, iy + 1, period), Hash(ix + 1, iy + 1, period), fx), fy);
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float fine = Noise(u * 32, v * 32, 32) * 0.85f + Hash(x, y, size) * 0.15f;
                    float mineral = Noise(u * 8, v * 8, 8) * 0.65f + Noise(u * 23, v * 23, 23) * 0.35f;
                    // Stretch this periodic channel in the shader, so the packed texture itself tiles on both axes.
                    float fibres = Noise(u * 64, v * 64, 64) * 0.7f + Noise(u * 107, v * 107, 107) * 0.3f;
                    pixels[y * size + x] = new Color(fine, mineral, fibres, 1);
                }
            try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(TexturePath, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.sRGBTexture = false; importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
    }
}
