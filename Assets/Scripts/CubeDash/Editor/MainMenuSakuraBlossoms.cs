using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDash.Editor
{
    /// <summary>Menu-only blossom shading. Never edits the downloaded material, scene lighting or tree geometry.</summary>
    public static class MainMenuSakuraBlossoms
    {
        public const string ArtPath = "Assets/MainMenu/Sakura/";
        public const string MaterialPath = ArtPath + "Menu Sakura Blossoms.mat";
        public const string ShaderPath = ArtPath + "Menu Sakura Blossoms.shadergraph";
        private const string SourceMaterialPath = "Assets/Idyllic Fantasy Nature/Materials/Trees/Tree_Blossoms.mat";
        private const string SourceShaderPath = "Assets/Idyllic Fantasy Nature/Shader/Vegetation.shadergraph";

        [MenuItem("Tools/Cube Dash/Brighten Main Menu Sakura Blossoms")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before tuning blossoms.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the menu blossoms.");
        }

        public static void ApplyAndCaptureFromCommandLine()
        {
            ApplyFromCommandLine(); MainMenuSceneBuilder.CaptureFromCommandLine();
        }

        public static void ApplyToScene(Scene scene)
        {
            MainMenuController menu = scene.GetRootGameObjects().Select(root => root.GetComponent<MainMenuController>()).FirstOrDefault(item => item != null);
            Transform trees = menu != null ? menu.transform.Find("Parking Lot Diorama/" + MainMenuJapaneseGarden.RootName + "/" + MainMenuAssetStoreTrees.RootName) : null;
            if (trees == null) throw new InvalidOperationException("The imported menu trees are required.");
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            if (source == null) throw new InvalidOperationException("The downloaded blossom material was not found.");
            Material blossoms = PrepareMaterial(source);
            int changed = 0;
            foreach (MeshRenderer renderer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if (!renderer.gameObject.activeInHierarchy) continue;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);
                if (!path.StartsWith(MainMenuAssetStoreTrees.PrefabPath + "BlossomTree_", StringComparison.Ordinal)) continue;
                Material[] materials = renderer.sharedMaterials;
                if (!materials.Contains(source)) continue; // Preserve existing menu material edits and custom overrides.
                Undo.RecordObject(renderer, "Brighten menu sakura blossoms");
                for (int slot = 0; slot < materials.Length; slot++)
                    if (materials[slot] == source) materials[slot] = blossoms;
                renderer.sharedMaterials = materials; changed++;
            }
            if (changed != 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Applied bright menu-only sakura shading to {changed} blossom LOD renderers. Trunks, geometry, other foliage, source assets and scene lighting were preserved.");
        }

        private static Material PrepareMaterial(Material source)
        {
            if (!AssetDatabase.IsValidFolder(ArtPath.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/MainMenu", "Sakura");
            if (!File.Exists(ShaderPath))
            {
                File.WriteAllText(ShaderPath, CreateMenuGraph(File.ReadAllText(SourceShaderPath)));
                AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
            }
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("The menu blossom shader could not compile.");
            var material = new Material(source) { name = "Menu Sakura Blossoms", shader = shader };
            material.SetColor("_Top_Color", new Color(1.18f, 0.38f, 0.62f, 1));
            material.SetColor("_Bottom_Color", new Color(1.00f, 0.24f, 0.42f, 1));
            material.SetFloat("_Menu_Blossom_Glow", 0.95f);
            AssetDatabase.CreateAsset(material, MaterialPath); AssetDatabase.SaveAssets(); return material;
        }

        private static string CreateMenuGraph(string source)
        {
            // Clone serialized node templates rather than requiring access to Shader Graph's internal editor API.
            var objects = new Dictionary<string, string>();
            foreach (Match block in Regex.Matches(source, @"(?ms)^\{.*?^\}"))
            {
                Match id = Regex.Match(block.Value, "\"m_ObjectId\": \"([a-f0-9]+)\"");
                objects.Add(id.Groups[1].Value, block.Value);
            }
            const string graphId = "4c963e03341a4ce7a8fc46437d0ea8ae", wind = "41c34984d2a2429fbc529be9726edfa6";
            const string glow = "3f96235d05b44d518e0b2eaa97fba994", propertyNode = "d7f8135feb914ec198f187aa9dfbb0cd", propertySlot = "bd2e1e6e0c0e4b84877258641d8e6208";
            const string multiply = "7b24c78a38554d42abc5ed65c795a7cd", a = "9a07df5bdb1a46d6b15c4e996c4ce83a", b = "1599d99c197147d896a40ed1a46b5157", output = "5338ba382ee44bb5bfec2e26adcd8f60";
            string oldPropertyNode = objects.Values.Single(block => block.Contains("\"m_Type\": \"UnityEditor.ShaderGraph.PropertyNode\"") && block.Contains(wind));
            string oldNodeId = Regex.Match(oldPropertyNode, "\"m_ObjectId\": \"([a-f0-9]+)\"").Groups[1].Value;
            string oldSlotId = Regex.Match(oldPropertyNode, @"""m_Slots"":\s*\[\s*\{\s*""m_Id"": ""([a-f0-9]+)""").Groups[1].Value;
            string glowProperty = objects[wind].Replace(wind, glow).Replace("a1a2007f-367d-42f7-9988-f7a50fc0d4c3", "a3b625bf-0468-4c26-8471-e1b32041a36a")
                .Replace("Wind Strength", "Menu Blossom Glow").Replace("_Wind_Strength", "_Menu_Blossom_Glow");
            glowProperty = Regex.Replace(glowProperty, @"""m_Value"": [^,]+,", "\"m_Value\": 0.95,");
            glowProperty = glowProperty.Replace("\"y\": 1.0", "\"y\": 2.0");
            string multiplyTemplate = objects["117a3293c8724edbbbfb3e0e208d0dd8"];
            string[] slots = Regex.Matches(multiplyTemplate, @"""m_Id"": ""([a-f0-9]+)""").Cast<Match>().Select(match => match.Groups[1].Value).Where(id => id.Length != 0).ToArray();
            if (slots.Length != 3) throw new InvalidOperationException("The package's emission node layout has changed; inspect it before cloning.");
            string newMultiply = multiplyTemplate.Replace("117a3293c8724edbbbfb3e0e208d0dd8", multiply).Replace(slots[0], a).Replace(slots[1], b).Replace(slots[2], output);
            string graph = objects[graphId].Replace("\"m_Path\": \"Idyllic Fantasy Nature\"", "\"m_Path\": \"Cube Dash/Main Menu\"");
            var emissionEdge = new Regex(@"""m_OutputSlot"":\s*\{\s*""m_Node"":\s*\{\s*""m_Id"": ""c35c890039964b6d84d86513b7a90a46""\s*\},\s*""m_SlotId"": 3");
            if (emissionEdge.Matches(graph).Count != 1) throw new InvalidOperationException("The package's emission connection has changed.");
            graph = emissionEdge.Replace(graph, match => match.Value.Replace("c35c890039964b6d84d86513b7a90a46", multiply).Replace("\"m_SlotId\": 3", "\"m_SlotId\": 2"));
            graph = Insert(graph, "m_Properties", "{\"m_Id\": \"" + glow + "\"},");
            graph = Insert(graph, "m_Nodes", "{\"m_Id\": \"" + propertyNode + "\"}, {\"m_Id\": \"" + multiply + "\"},");
            graph = Insert(graph, "m_Edges", Edge("c35c890039964b6d84d86513b7a90a46", 3, multiply, 0) + "," + Edge(propertyNode, 0, multiply, 1) + ",");
            objects[graphId] = graph;
            // Smooth the package's stepped shadow gradients and lift only the blossom shader's shade floors.
            source = Regex.Replace(source, @"(?ms)^\{.*?^\}", match =>
            {
                string block = match.Value;
                if (block.Contains("\"m_ObjectId\": \"" + graphId + "\"")) return graph;
                if (block.Contains("0.25471699237823489")) block = block.Replace("0.25471699237823489", "0.50").Replace("\"m_Mode\": 1", "\"m_Mode\": 0");
                if (block.Contains("0.38679248094558718")) block = block.Replace("0.38679248094558718", "0.55").Replace("\"m_Mode\": 1", "\"m_Mode\": 0");
                if (block.Contains("0.698113203048706")) block = block.Replace("0.698113203048706", "0.75").Replace("\"m_Mode\": 1", "\"m_Mode\": 0");
                if (block.Contains("\"m_ObjectId\": \"ef05abe4ea734d36825a640dd302ea51\"") || block.Contains("\"m_ObjectId\": \"270cad7b942946d9a7747355177c9d46\""))
                    block = block.Replace("\"m_ColorMode\": 0", "\"m_ColorMode\": 1");
                return block;
            });
            return source + "\n\n" + string.Join("\n\n", new[] { glowProperty, oldPropertyNode.Replace(oldNodeId, propertyNode).Replace(wind, glow).Replace(oldSlotId, propertySlot),
                objects[oldSlotId].Replace(oldSlotId, propertySlot).Replace("Wind Strength", "Menu Blossom Glow"), newMultiply, objects[slots[0]].Replace(slots[0], a), objects[slots[1]].Replace(slots[1], b), objects[slots[2]].Replace(slots[2], output) }) + "\n";
        }

        private static string Insert(string graph, string field, string entries) => new Regex("\"" + field + "\": \\[").Replace(graph, match => match.Value + "\n        " + entries, 1);
        private static string Edge(string from, int fromSlot, string to, int toSlot) => "{\"m_OutputSlot\": {\"m_Node\": {\"m_Id\": \"" + from + "\"}, \"m_SlotId\": " + fromSlot + "}, \"m_InputSlot\": {\"m_Node\": {\"m_Id\": \"" + to + "\"}, \"m_SlotId\": " + toSlot + "}}";
    }
}
