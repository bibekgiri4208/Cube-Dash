using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    public static partial class CubeDashEnvironments
    {
        private const string CityModels = Prefabs + "/City Models";
        private static Material[] cityPalette;

        [MenuItem("Tools/Cube Dash/Improve City Neighborhoods")]
        public static void ImproveCityFromCommandLine()
        {
            Folder(CityModels); InitializeModels();
            try
            {
                string[] names = { "Warm Brick", "Cream Stucco", "Blue Stucco", "Roof Tile", "Window Glass", "Stone Trim",
                    "Street Asphalt", "Shop Teal", "Awning Red", "Brass Metal", "Park Green", "Dark Timber" };
                Color[] colors = { new Color(0.65f, 0.32f, 0.23f), new Color(0.85f, 0.76f, 0.58f), new Color(0.39f, 0.59f, 0.65f),
                    new Color(0.46f, 0.19f, 0.13f), new Color(0.08f, 0.23f, 0.29f), new Color(0.83f, 0.85f, 0.77f),
                    new Color(0.19f, 0.23f, 0.25f), new Color(0.05f, 0.42f, 0.40f), new Color(0.78f, 0.24f, 0.18f),
                    new Color(0.46f, 0.42f, 0.30f), new Color(0.16f, 0.40f, 0.22f), new Color(0.23f, 0.16f, 0.12f) };
                cityPalette = new Material[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    cityPalette[i] = Material("City " + names[i], colors[i]);
                    Surface(cityPalette[i], i == 3 ? 4 : i == 10 ? 2 : 0, i == 0 ? 1.5f : 0.8f);
                    cityPalette[i].SetFloat("_Smoothness", i == 4 ? 0.5f : 0.15f);
                }
                var models = new Dictionary<string, Mesh>();
                foreach (string name in new[] { "Detached House", "Town House", "Market Shop", "Corner Cafe", "Bakery Shop",
                    "Balcony Apartments", "Terraced Office", "Clock Hall", "Street Furniture", "Bus Shelter" })
                {
                    Mesh mesh = SaveMesh(BuildCityModel(name), "City " + name); models[name] = mesh;
                    SaveCityModelPrefab(name, mesh);
                }
                Mesh[] layouts = new Mesh[3];
                for (int layout = 0; layout < layouts.Length; layout++) layouts[layout] = SaveMesh(BuildCityLayout(layout, models), "City District Layout " + (layout + 1));
                Mesh[] flyovers = new Mesh[3];
                for (int profile = 0; profile < 3; profile++) flyovers[profile] = SaveMesh(BuildCityFlyover(profile), "City Flyover " + new[] { "Level", "Entrance", "Exit" }[profile]);
                SaveCityModelPrefab("Flyover", flyovers[0]);

                const string path = "Assets/Prefab/CubeDash/TrackSegment.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform city = root.GetComponent<SegmentEnvironment>().Biomes[0].transform;
                    // Replace the known repeated generated tower instances, not their reusable source assets.
                    var towers = new List<Transform>();
                    foreach (Transform child in city) if (child.name.Contains("Skyscraper")) towers.Add(child);
                    for (int i = 0; i < towers.Count; i++)
                    {
                        if (i >= 4) { Object.DestroyImmediate(towers[i].gameObject); continue; }
                        towers[i].localPosition = new Vector3((i < 2 ? -1 : 1) * (i % 2 == 0 ? 65 : 79), -9, i % 2 == 0 ? 5 : 37);
                        towers[i].localScale = new Vector3(1, i % 2 == 0 ? 0.86f : 1.03f, 1);
                    }
                    Transform details = city.Find("Landscape Details");
                    if (details == null) details = Part(city, "Landscape Details", layouts[0], cityPalette, Vector3.zero, Vector3.one).transform;
                    details.GetComponent<MeshFilter>().sharedMesh = layouts[0]; details.GetComponent<Renderer>().sharedMaterials = cityPalette;
                    BiomeScenery scenery = city.GetComponent<BiomeScenery>(); if (scenery == null) scenery = city.gameObject.AddComponent<BiomeScenery>();
                    Set(scenery, "details", details.GetComponent<MeshFilter>()); SetArray(scenery, "layouts", layouts);
                    Transform deck = city.Find("City Flyover");
                    if (deck == null) deck = Part(city, "City Flyover", flyovers[0], cityPalette, Vector3.zero, Vector3.one).transform;
                    deck.GetComponent<MeshFilter>().sharedMesh = flyovers[0]; deck.GetComponent<Renderer>().sharedMaterials = cityPalette;
                    CityFlyover flyover = city.GetComponent<CityFlyover>(); if (flyover == null) flyover = city.gameObject.AddComponent<CityFlyover>();
                    Set(flyover, "deck", deck.GetComponent<MeshFilter>()); SetArray(flyover, "profiles", flyovers);
                    Mesh streets = SaveMesh(BuildCityStreets(), "City Streets and Sidewalks");
                    Transform street = city.Find("City Streets");
                    if (street == null) street = Part(city, "City Streets", streets, cityPalette, Vector3.zero, Vector3.one, false).transform;
                    street.GetComponent<MeshFilter>().sharedMesh = streets; street.GetComponent<Renderer>().sharedMaterials = cityPalette;
                    // Reconnect the remaining skyline towers without regenerating other biome assets.
                    var boundary = new SerializedObject(root.GetComponent<BiomeBoundaryBlend>());
                    var refs = boundary.FindProperty("cityBuildings"); refs.arraySize = 4;
                    var scales = boundary.FindProperty("cityBuildingScales"); scales.arraySize = 4;
                    for (int i = 0; i < 4; i++) { refs.GetArrayElementAtIndex(i).objectReferenceValue = towers[i]; scales.GetArrayElementAtIndex(i).vector3Value = towers[i].localScale; }
                    boundary.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.SaveAssets();
                CubeDashSceneryRise.ApplyFromCommandLine();
                Debug.Log("City-only neighborhoods saved: eleven reusable models, three varied streetscapes, sparse distant skyline and connected side flyover with end ramps. Other biomes, gameplay, ambience and far spawn distance preserved.");
            }
            finally { ReleaseModels(); cityPalette = null; }
        }

        private static void SaveCityModelPrefab(string name, Mesh mesh)
        {
            GameObject model = new GameObject("City " + name);
            try
            {
                Part(model.transform, "Model", mesh, cityPalette, Vector3.zero, Vector3.one);
                PrefabUtility.SaveAsPrefabAsset(model, CityModels + "/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(model); }
        }

        private static void CityBox(Geometry g, int material, float x, float y, float z, float w, float h, float d)
            => g.Add(cube, cityPalette[material], new Vector3(x, y, z), new Vector3(w, h, d));

        private static void CityWindow(Geometry g, float x, float y, float z, float w = 1.6f, float h = 1.7f)
        {
            CityBox(g, 5, x, y, z, w + 0.28f, h + 0.28f, 0.18f);
            CityBox(g, 4, x, y, z - 0.12f, w, h, 0.12f);
            CityBox(g, 5, x, y, z - 0.20f, 0.08f, h, 0.08f);
            CityBox(g, 5, x, y, z - 0.20f, w, 0.08f, 0.08f);
            CityBox(g, 5, x, y - h * 0.5f - 0.16f, z - 0.14f, w + 0.42f, 0.16f, 0.40f);
        }

        private static Mesh BuildCityModel(string name)
        {
            Geometry g = new Geometry(cityPalette);
            if (name == "Street Furniture")
            {
                CityBox(g, 5, 0, 0.2f, 0, 2.4f, 0.4f, 2.4f);
                g.Add(cylinder, cityPalette[9], new Vector3(0, 6.1f, 0), new Vector3(0.22f, 6, 0.22f));
                CityBox(g, 9, 0.55f, 12, 0, 1.3f, 0.22f, 0.25f);
                CityBox(g, 5, 1.05f, 11.8f, 0, 0.9f, 0.22f, 0.6f);
                CityBox(g, 11, 0, 1.0f, 2.4f, 2.3f, 0.20f, 0.8f);
                CityBox(g, 11, 0, 1.55f, 2.8f, 2.3f, 0.65f, 0.16f);
                for (int side = -1; side <= 1; side += 2) CityBox(g, 9, side * 0.85f, 0.5f, 2.4f, 0.13f, 0.9f, 0.65f);
            }
            else if (name == "Bus Shelter")
            {
                CityBox(g, 5, 0, 0.15f, 0, 6, 0.3f, 3);
                for (int side = -1; side <= 1; side += 2) CityBox(g, 9, side * 2.6f, 2.1f, 0.9f, 0.14f, 4.2f, 0.14f);
                CityBox(g, 4, 0, 2.2f, 1, 5.3f, 3.5f, 0.12f);
                CityBox(g, 7, 0, 4.3f, 0, 6.2f, 0.3f, 3.2f);
                CityBox(g, 11, 0, 1.1f, 0.5f, 4.4f, 0.2f, 0.8f);
                CityBox(g, 9, 3.4f, 3.8f, 0, 0.15f, 7.6f, 0.15f);
                CityBox(g, 7, 3.4f, 6.8f, 0, 1.2f, 1.3f, 0.14f);
            }
            else if (name == "Detached House" || name == "Town House")
            {
                bool town = name == "Town House"; float h = town ? 10 : 7, w = town ? 7 : 9;
                CityBox(g, 5, 0, 0.25f, 0, w + 0.8f, 0.5f, 8.8f);
                CityBox(g, town ? 0 : 2, 0, h * 0.5f, 0, w, h, 8);
                CityBox(g, 5, 0, h - 0.15f, 0, w + 0.4f, 0.3f, 8.4f);
                g.Add(roof, cityPalette[3], new Vector3(0, h, 0), new Vector3(w + 1.2f, 2.8f, 9.2f));
                for (int row = 0; row < (town ? 3 : 2); row++)
                    for (int side = -1; side <= 1; side += 2) CityWindow(g, side * w * 0.29f, 2 + row * 2.9f, -4.07f, 1.7f, 1.65f);
                CityBox(g, 11, 0, 1.8f, -4.15f, 1.6f, 3.2f, 0.2f);
                CityBox(g, 9, 0.5f, 1.6f, -4.3f, 0.13f, 0.13f, 0.12f);
                CityBox(g, 5, 0, 3.6f, -4.7f, 2.6f, 0.25f, 1.9f);
                CityBox(g, 5, 0, 0.35f, -4.8f, 2.8f, 0.7f, 1.7f);
                CityBox(g, 0, w * 0.28f, h + 1.5f, 1.4f, 0.8f, 3, 0.9f);
                CityBox(g, 5, w * 0.28f, h + 3.05f, 1.4f, 1.1f, 0.2f, 1.2f);
                for (int tile = 0; tile < 7; tile++) CityBox(g, 3, 0, h + 0.05f, -4.5f + tile * 1.5f, w + 1.2f, 0.08f, 0.09f);
                for (int post = 0; post < 6; post++) CityBox(g, 5, -w * 0.5f - 0.8f, 0.9f, -3.6f + post * 1.4f, 0.12f, 1.8f, 0.12f);
                CityBox(g, 5, -w * 0.5f - 0.8f, 1.3f, -0.1f, 0.12f, 0.14f, 7.2f);
            }
            else if (name == "Market Shop" || name == "Corner Cafe" || name == "Bakery Shop")
            {
                bool cafe = name == "Corner Cafe"; float h = cafe ? 9 : 7;
                CityBox(g, cafe ? 0 : 1, 0, h * 0.5f, 0, 10, h, 8);
                CityBox(g, 5, 0, h, 0, 10.5f, 0.5f, 8.5f);
                for (int side = -1; side <= 1; side += 2)
                {
                    CityWindow(g, side * 3, 2.35f, -4.1f, 3.3f, 3.4f);
                    if (cafe) CityWindow(g, side * 3, 7.3f, -4.1f, 1.8f, 1.8f);
                }
                CityBox(g, 4, 0, 2, -4.2f, 1.6f, 3.7f, 0.2f);
                CityBox(g, 5, 0, 2, -4.35f, 0.07f, 3.7f, 0.09f);
                CityBox(g, 7, 0, 5.7f, -4.15f, 9.5f, 1.5f, 0.25f);
                CitySign(g, cafe ? "CAFE" : name == "Market Shop" ? "MARKET" : "BAKERY", 0, 5.7f, -4.34f, 0.14f);
                for (int stripe = 0; stripe < 14; stripe++)
                {
                    g.Add(cube, cityPalette[stripe % 2 == 0 ? 8 : 5], new Vector3(-4.55f + stripe * 0.7f, 4.75f, -4.9f),
                        new Vector3(0.69f, 0.15f, 1.8f), Quaternion.Euler(-12, 0, 0));
                    CityBox(g, stripe % 2 == 0 ? 8 : 5, -4.55f + stripe * 0.7f, 4.38f, -5.75f, 0.69f, 0.45f, 0.1f);
                }
                CityBox(g, 9, -3.4f, h + 0.5f, 2, 2.4f, 1.0f, 1.8f);
                if (cafe)
                {
                    g.Add(cylinder, cityPalette[11], new Vector3(3, 1.25f, -6.2f), new Vector3(1.8f, 0.12f, 1.8f));
                    g.Add(cylinder, cityPalette[9], new Vector3(3, 0.65f, -6.2f), new Vector3(0.15f, 0.65f, 0.15f));
                    for (int side = -1; side <= 1; side += 2) CityBox(g, 11, 3 + side * 1.25f, 0.7f, -6.2f, 0.8f, 1.4f, 0.8f);
                }
            }
            else if (name == "Balcony Apartments" || name == "Terraced Office")
            {
                bool office = name == "Terraced Office"; int floors = office ? 6 : 5; float h = floors * 3.3f;
                CityBox(g, office ? 2 : 0, 0, h * 0.5f, 0, 12, h, 10);
                for (int floor = 0; floor < floors; floor++)
                {
                    float y = 2 + floor * 3.3f;
                    for (int column = -1; column <= 1; column++)
                    {
                        CityWindow(g, column * 3.5f, y, -5.1f, 2.1f, 2.0f);
                        if (!office && floor > 0)
                        {
                            CityBox(g, 5, column * 3.5f, y - 1.2f, -5.8f, 2.8f, 0.2f, 1.7f);
                            CityBox(g, 9, column * 3.5f, y - 0.1f, -6.6f, 2.8f, 0.12f, 0.12f);
                            for (int rail = -2; rail <= 2; rail++) CityBox(g, 9, column * 3.5f + rail * 0.6f, y - 0.65f, -6.6f, 0.07f, 1.0f, 0.07f);
                        }
                    }
                    CityBox(g, 5, 0, (floor + 1) * 3.3f - 0.1f, 0, 12.3f, 0.2f, 10.3f);
                    for (int side = -1; side <= 1; side += 2)
                        for (int col = -1; col <= 1; col++)
                            CityBox(g, 4, side * 6.07f, y, col * 3.2f, 0.14f, 1.9f, 2.1f);
                }
                CityBox(g, 5, 0, h + 0.3f, 0, 12.5f, 0.6f, 10.5f);
                if (office)
                {
                    CityBox(g, 2, 1, h + 1.8f, 1, 7.8f, 3.6f, 6.5f);
                    CityBox(g, 4, 1, h + 2, -2.3f, 6.5f, 2.1f, 0.15f);
                    CityBox(g, 5, 1, h + 3.8f, 1, 8.2f, 0.4f, 6.9f);
                }
                else
                {
                    for (int side = -1; side <= 1; side += 2) CityBox(g, 10, side * 4, h + 0.7f, -3, 2.5f, 1.1f, 1.6f);
                    CityBox(g, 9, 0, h + 0.8f, 2.5f, 3.2f, 1.2f, 2.2f);
                }
                CityBox(g, 4, 0, 1.8f, -5.17f, 2.1f, 3.5f, 0.2f);
                CityBox(g, 7, 0, 3.8f, -6, 4, 0.25f, 2.2f);
            }
            else // Civic landmark: arched-looking column porch and a clock tower, not another glass box.
            {
                CityBox(g, 1, 0, 4, 0, 14, 8, 9);
                g.Add(roof, cityPalette[3], new Vector3(0, 8, 0), new Vector3(15, 3, 10));
                for (int column = -2; column <= 2; column++)
                {
                    g.Add(cylinder, cityPalette[5], new Vector3(column * 2.7f, 3.6f, -5.3f), new Vector3(0.6f, 3.5f, 0.6f));
                    CityWindow(g, column * 2.7f, 4.7f, -4.6f, 1.7f, 2.8f);
                }
                CityBox(g, 5, 0, 7.4f, -5.3f, 14.5f, 0.6f, 2.6f);
                CityBox(g, 5, 0, 0.35f, -5.3f, 15, 0.7f, 2.8f);
                CityBox(g, 0, 0, 11.5f, 1, 4.1f, 9, 4.1f);
                CityBox(g, 5, 0, 15.9f, 1, 4.5f, 0.4f, 4.5f);
                g.Add(roof, cityPalette[7], new Vector3(0, 16.1f, 1), new Vector3(4.8f, 2.7f, 4.8f));
                g.Add(cylinder, cityPalette[5], new Vector3(0, 13.4f, -1.12f), new Vector3(2.7f, 0.08f, 2.7f), Quaternion.Euler(90, 0, 0));
                CityBox(g, 11, 0, 13.85f, -1.24f, 0.1f, 0.9f, 0.08f);
                CityBox(g, 11, 0.45f, 13.4f, -1.24f, 0.9f, 0.1f, 0.08f);
            }
            Mesh result = g.Bake(); SetCityAnchor(result, Vector2.zero); return result;
        }

        private static readonly Dictionary<char, string> CityLetters = new Dictionary<char, string>
        {
            { 'A', "01110/10001/10001/11111/10001/10001/10001" }, { 'B', "11110/10001/10001/11110/10001/10001/11110" },
            { 'C', "01111/10000/10000/10000/10000/10000/01111" }, { 'E', "11111/10000/10000/11110/10000/10000/11111" },
            { 'F', "11111/10000/10000/11110/10000/10000/10000" }, { 'K', "10001/10010/10100/11000/10100/10010/10001" },
            { 'M', "10001/11011/10101/10101/10001/10001/10001" }, { 'R', "11110/10001/10001/11110/10100/10010/10001" },
            { 'T', "11111/00100/00100/00100/00100/00100/00100" }, { 'Y', "10001/10001/01010/00100/00100/00100/00100" }
        };

        private static void CitySign(Geometry g, string text, float x, float y, float z, float size)
        {
            float start = x - (text.Length * 6 - 1) * size * 0.5f;
            for (int letter = 0; letter < text.Length; letter++)
            {
                string[] rows = CityLetters[text[letter]].Split('/');
                for (int row = 0; row < 7; row++) for (int col = 0; col < 5; col++)
                    if (rows[row][col] == '1') CityBox(g, 5, start + (letter * 6 + col + 0.5f) * size,
                        y + (3 - row) * size, z, size * 0.9f, size * 0.9f, 0.07f);
            }
        }

        private static void SetCityAnchor(Mesh mesh, Vector2 anchor)
        {
            var anchors = new List<Vector2>(mesh.vertexCount);
            for (int i = 0; i < mesh.vertexCount; i++) anchors.Add(anchor);
            mesh.SetUVs(2, anchors);
        }

        private static void AddCityModel(Geometry g, Mesh source, Vector3 position, float yaw = 0)
        {
            Vector3[] sourceVertices = source.vertices, sourceNormals = source.normals;
            for (int surface = 0; surface < cityPalette.Length; surface++)
            {
                int[] indices = source.GetTriangles(surface);
                if (indices.Length == 0) continue;
                var remap = new Dictionary<int, int>(); var vertices = new List<Vector3>(); var normals = new List<Vector3>();
                for (int i = 0; i < indices.Length; i++)
                {
                    int old = indices[i];
                    if (!remap.TryGetValue(old, out int mapped))
                    {
                        mapped = vertices.Count; remap[old] = mapped;
                        vertices.Add(sourceVertices[old]); normals.Add(sourceNormals[old]);
                    }
                    indices[i] = mapped;
                }
                Mesh part = Own(new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 });
                part.SetVertices(vertices); part.SetNormals(normals); part.SetTriangles(indices, 0);
                SetCityAnchor(part, new Vector2(position.x, position.z));
                g.Add(part, cityPalette[surface], position, Vector3.one, yaw);
            }
        }

        private static Mesh BuildCityLayout(int layout, Dictionary<string, Mesh> models)
        {
            Geometry g = new Geometry(cityPalette);
            string[][] streetRows = {
                new[] { "Detached House", "Market Shop", "Town House", "Corner Cafe", "Town House", "Bakery Shop" },
                new[] { "Bakery Shop", "Town House", "Corner Cafe", "Detached House", "Market Shop", "Town House" },
                new[] { "Town House", "Corner Cafe", "Market Shop", "Bakery Shop", "Detached House", "Town House" }
            };
            for (int side = -1; side <= 1; side += 2)
            {
                float yaw = side == 1 ? 90 : -90;
                for (int slot = 0; slot < 3; slot++)
                {
                    AddCityModel(g, models[streetRows[layout][slot + (side == 1 ? 3 : 0)]], new Vector3(side * 18, -9, 7 + slot * 14), yaw);
                    AddCityModel(g, models["Street Furniture"], new Vector3(side * 11.5f, -9, 4 + slot * 14), yaw);
                }
                AddCityModel(g, models[layout == 2 && side < 0 ? "Clock Hall" : "Balcony Apartments"], new Vector3(side * 46, -9, 10), yaw);
                AddCityModel(g, models[layout == 1 && side > 0 ? "Clock Hall" : "Terraced Office"], new Vector3(side * 47, -9, 31), yaw);
                AddCityModel(g, models["Bus Shelter"], new Vector3(side * 11.6f, -9, 24), yaw);
            }
            return g.Bake();
        }

        private static Mesh BuildCityStreets()
        {
            Geometry g = new Geometry(cityPalette);
            for (int side = -1; side <= 1; side += 2)
            {
                CityBox(g, 6, side * 8.3f, -8.97f, 21, 4.6f, 0.05f, 42);
                CityBox(g, 5, side * 12, -8.8f, 21, 2.5f, 0.35f, 42);
                CityBox(g, 6, side * 32, -8.97f, 21, 13, 0.05f, 42);
                CityBox(g, 5, side * 40.5f, -8.8f, 21, 3.5f, 0.35f, 42);
                for (int dash = 0; dash < 7; dash++) CityBox(g, 5, side * 8.3f, -8.925f, 3 + dash * 6, 0.13f, 0.02f, 2.6f);
            }
            return g.Bake();
        }

        private static Mesh BuildCityFlyover(int profile)
        {
            Geometry g = new Geometry(cityPalette);
            float Height(float z) => profile == 1 ? Mathf.Clamp01(z / 42) * 9 : profile == 2 ? (1 - Mathf.Clamp01(z / 42)) * 9 : 9;
            // Parallel to the runner, with joined ramp profiles. Nothing crosses flight or ground lanes.
            for (int slice = 0; slice < 14; slice++)
            {
                float z = slice * 3 + 1.5f, y = Height(z), slope = profile == 1 ? -Mathf.Atan(9f / 42) * Mathf.Rad2Deg : profile == 2 ? Mathf.Atan(9f / 42) * Mathf.Rad2Deg : 0;
                Quaternion rotation = Quaternion.Euler(slope, 0, 0);
                g.Add(cube, cityPalette[5], new Vector3(32, -9 + y - 0.42f, z), new Vector3(8, 0.75f, 3.07f), rotation);
                g.Add(cube, cityPalette[6], new Vector3(32, -9 + y, z), new Vector3(7.4f, 0.08f, 3.07f), rotation);
                for (int side = -1; side <= 1; side += 2)
                {
                    g.Add(cube, cityPalette[5], new Vector3(32 + side * 3.85f, -9 + y + 0.55f, z), new Vector3(0.23f, 1.2f, 3.07f), rotation);
                    g.Add(cube, cityPalette[9], new Vector3(32 + side * 3.85f, -9 + y + 1.2f, z), new Vector3(0.28f, 0.12f, 3.07f), rotation);
                }
                if (slice % 2 == 0) g.Add(cube, cityPalette[5], new Vector3(32, -9 + y + 0.06f, z), new Vector3(0.13f, 0.025f, 1.9f), rotation);
            }
            foreach (float z in new[] { 7f, 21, 35 })
            {
                float height = Height(z) - 0.8f;
                if (height <= 0) continue;
                g.Add(cylinder, cityPalette[5], new Vector3(32, -9 + height * 0.5f, z), new Vector3(1.1f, height * 0.5f, 1.1f));
                CityBox(g, 5, 32, -9 + height, z, 6.8f, 0.45f, 1.4f);
                CityBox(g, 5, 32, -8.8f, z, 3, 0.4f, 2.8f);
            }
            Mesh mesh = g.Bake(); SetCityAnchor(mesh, new Vector2(32, 21)); return mesh;
        }
    }
}
