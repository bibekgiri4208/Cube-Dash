using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Authors an editable parking-lot diorama and reference-layout UI in the user's Main Menu scene.</summary>
    public static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main Menu.unity";
        public const string RootName = "Cube Dash Main Menu";
        private const string Art = "Assets/MainMenu/";
        private static readonly Dictionary<string, Material> palette = new Dictionary<string, Material>();
        private static Mesh cone, foliage, cloud, leaf, cube;
        private static Font displayFont, graffitiFont;
        private static Transform world;

        [MenuItem("Tools/Cube Dash/Build Reference Main Menu")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildFromCommandLine();
        }

        public static void BuildFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before authoring the menu.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject generated = null;
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == RootName) generated = root;
            if (generated != null)
            {
                if (generated.GetComponent<MainMenuController>() == null) throw new InvalidOperationException("A user object uses the generated menu's name; refusing to replace it.");
                Object.DestroyImmediate(generated);
            }
            Folder(Art + "Materials"); Folder(Art + "Meshes");
            displayFont = AssetDatabase.LoadAssetAtPath<Font>(Art + "Fonts/Anton-Regular.ttf");
            graffitiFont = AssetDatabase.LoadAssetAtPath<Font>(Art + "Fonts/PermanentMarker-Regular.ttf");
            if (displayFont == null || graffitiFont == null) throw new InvalidOperationException("The bundled menu fonts are required.");
            InitializeArt();
            GameObject rootObject = new GameObject(RootName);
            MainMenuController controller = rootObject.AddComponent<MainMenuController>();
            world = Group("Parking Lot Diorama", rootObject.transform);
            BuildGround(); BuildWall(); BuildBackdrop(); BuildStreetProps();
            Renderer hero = BuildHero(); Set(controller, "hero", hero);
            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null) camera = new GameObject("Main Camera", typeof(AudioListener)).AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 3.6f, -10);
            camera.transform.rotation = Quaternion.Euler(8.5f, 0, 0);
            camera.fieldOfView = 50; camera.nearClipPlane = 0.1f; camera.farClipPlane = 180;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High; data.dithering = true; data.stopNaN = true;
            ConfigureLighting(rootObject.transform);
            BuildUI(rootObject.transform, controller, camera);
            ConfigureBuildScenes();
            MainMenuJapaneseGarden.ApplyToScene(scene);
            if (AssetDatabase.IsValidFolder(MainMenuAssetStoreTrees.PrefabPath.TrimEnd('/')))
            {
                MainMenuAssetStoreTrees.ApplyToScene(scene);
                MainMenuAssetStoreGroundCover.ApplyToScene(scene);
                MainMenuSakuraBlossoms.ApplyToScene(scene);
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(MainMenuSakuraPetals.TexturePath) != null) MainMenuSakuraPetals.ApplyToScene(scene);
            }
            MainMenuCameraMotion.ApplyToScene(scene);
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(MainMenuMusic.ClipPath) != null) MainMenuMusic.ApplyToScene(scene);
            MainMenuPanelTheme.ApplyToScene(scene);
            MainMenuMapSelection.ApplyToScene(scene);
            MainMenuQuitConfirmation.ApplyToScene(scene);
            MainMenuUiAnimations.ApplyToScene(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Main Menu.");
            // Unity writes trailing blanks for empty serialized strings; keep generated diffs clean.
            File.WriteAllText(ScenePath, Regex.Replace(File.ReadAllText(ScenePath), @"[ \t]+(?=\r?$)", string.Empty, RegexOptions.Multiline));
            Debug.Log("Reference main menu authored: parking-lot diorama, graffiti, hero cube, condensed ribbon UI, audio/display options and empty skin collections. Main Menu is first in the build scene list; Level is preserved.");
        }

        private static void InitializeArt()
        {
            palette.Clear();
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            cone = MeshAsset("Faceted tapered shape", Frustum());
            foliage = MeshAsset("Faceted foliage", EnvironmentModelMeshes.Foliage(7, true));
            cloud = MeshAsset("Cloud lobe", EnvironmentModelMeshes.Foliage(12));
            leaf = MeshAsset("Weed blade", EnvironmentModelMeshes.BroadLeaf());
            Surface("Asphalt", new Color(0.22f,0.235f,0.25f), new Color(0.36f,0.32f,0.27f), 0);
            Surface("Aged concrete", new Color(0.64f,0.64f,0.62f), new Color(0.50f,0.49f,0.46f), 1);
            Surface("Light concrete", new Color(0.79f,0.79f,0.74f), new Color(0.63f,0.62f,0.56f), 1);
            Surface("Worn curb yellow", new Color(0.77f,0.53f,0.12f), new Color(0.65f,0.55f,0.35f), 1);
            Lit("Paint white", new Color(0.88f,0.86f,0.77f));
            Lit("Ink black", new Color(0.008f,0.008f,0.012f));
            Lit("Safety yellow", new Color(1,0.66f,0.07f));
            Lit("Cone orange", new Color(0.96f,0.18f,0.035f));
            Lit("Lamp frame", new Color(0.10f,0.095f,0.065f));
            Lit("Warm lamp", new Color(1,0.81f,0.43f), 2.5f);
            Lit("Steel", new Color(0.35f,0.38f,0.39f), 0, 0.48f, 0.4f);
            Lit("Wood", new Color(0.36f,0.20f,0.105f));
            Lit("Wood light", new Color(0.53f,0.33f,0.17f));
            Lit("Pine dark", new Color(0.12f,0.37f,0.14f));
            Lit("Pine light", new Color(0.35f,0.62f,0.12f));
            Lit("Leaves dark", new Color(0.18f,0.41f,0.075f));
            Lit("Leaves green", new Color(0.34f,0.57f,0.075f));
            Lit("Leaves sunlit", new Color(0.55f,0.71f,0.14f));
            Lit("Garage walls", new Color(0.46f,0.50f,0.51f));
            Lit("Office walls", new Color(0.66f,0.68f,0.63f));
            Lit("Window blue", new Color(0.16f,0.37f,0.49f), 0, 0.5f, 0.1f);
            Lit("Sedan blue", new Color(0.15f,0.30f,0.38f), 0, 0.42f, 0.12f);
            Lit("Sedan glass", new Color(0.06f,0.13f,0.18f), 0, 0.6f, 0.1f);
            Lit("Sedan trim", new Color(0.70f,0.69f,0.61f), 0, 0.4f, 0.4f);
            Lit("Parking blue", new Color(0.035f,0.19f,0.7f));
            Lit("Hero red", new Color(0.95f,0.018f,0.09f), 0, 0.26f);
            Lit("Cloud white", new Color(0.97f,0.975f,1), 0.3f, 0);
            Lit("Distant mountain", new Color(0.25f,0.48f,0.57f));
        }

        private static void BuildGround()
        {
            Transform ground = Group("Weathered asphalt and parking bays", world);
            Box("Asphalt", ground, new Vector3(0,-0.15f,8), new Vector3(50,0.3f,60), "Asphalt");
            for (int bay = 0; bay < 7; bay++)
            {
                float x = -12.3f + bay * 4.2f;
                Box("Parking bay stripe", ground, new Vector3(x,0.012f,1.2f), new Vector3(0.09f,0.016f,10.2f), "Paint white");
                if (bay < 6)
                {
                    Box("Parking wheel stop", ground, new Vector3(x + 2.1f,0.115f,6.45f), new Vector3(2.45f,0.20f,0.28f), "Light concrete");
                    for (int side = -1; side <= 1; side += 2)
                        Box("Stop marker", ground, new Vector3(x + 2.1f + side * 1.02f,0.218f,6.45f), new Vector3(0.16f,0.013f,0.29f), "Worn curb yellow");
                }
            }
            Box("Rear sidewalk", ground, new Vector3(0,0.095f,7.75f), new Vector3(42,0.19f,1.4f), "Aged concrete");
            for (int i = 0; i < 61; i++)
            {
                float x = -21 + i * 0.7f;
                Box("Individual curb stone", ground, new Vector3(x,0.17f,7.08f), new Vector3(0.67f,0.30f,0.3f), "Light concrete");
                if (i % 6 == 2) Box("Chipped yellow curb paint", ground, new Vector3(x,0.325f,7.08f), new Vector3(0.27f,0.014f,0.31f), "Worn curb yellow");
            }
            var random = new System.Random(41);
            for (int i = 0; i < 40; i++)
            {
                float x = Mathf.Lerp(-14,15,(float)random.NextDouble()), z = Mathf.Lerp(-5,7,(float)random.NextDouble());
                if (Vector2.Distance(new Vector2(x,z),new Vector2(3,-0.9f)) < 1.6f) continue;
                Transform blade = Part("Asphalt crack weed", ground, leaf, M(i % 2 == 0 ? "Leaves green" : "Leaves sunlit"),
                    new Vector3(x,0.02f,z), new Vector3(0.12f,0.2f,0.12f));
                blade.rotation = Quaternion.Euler(-15,random.Next(360),0);
            }
        }

        private static void BuildWall()
        {
            Transform wall = Group("Concrete wall, graffiti and warm sconces", world);
            Box("Continuous wall", wall, new Vector3(6.1f,2.32f,8.55f), new Vector3(28.8f,4.25f,0.45f), "Aged concrete");
            Box("Wall coping", wall, new Vector3(6.1f,4.49f,8.5f), new Vector3(29,0.18f,0.72f), "Light concrete");
            float[] pillars = { -8.3f,-3.9f,4.4f,11.4f,18.8f };
            foreach (float x in pillars)
            {
                Box("Wall pier", wall, new Vector3(x,2.39f,8.30f), new Vector3(0.58f,4.4f,0.76f), "Aged concrete");
                Box("Pier cap", wall, new Vector3(x,4.65f,8.30f), new Vector3(0.83f,0.18f,0.94f), "Light concrete");
                if (x > 15) continue;
                Box("Sconce frame", wall, new Vector3(x,2.85f,7.875f), new Vector3(0.28f,0.81f,0.18f), "Lamp frame");
                Box("Warm sconce glass", wall, new Vector3(x,2.85f,7.77f), new Vector3(0.19f,0.64f,0.07f), "Warm lamp");
                var light = new GameObject("Warm wall pool").AddComponent<Light>();
                light.transform.SetParent(wall,false); light.transform.localPosition = new Vector3(x,2.85f,7.4f);
                light.type = LightType.Point; light.color = new Color(1,0.63f,0.20f); light.intensity = 0.7f; light.range = 2.1f; light.shadows = LightShadows.None;
            }
            WorldText("CUBE DASH wall tag", wall, "CUBE\nDASH", new Vector3(0.1f,2.57f,8.295f), 0.165f, graffitiFont, Color.black, 12);
            Stroke("Graffiti underline", wall, new[] { new Vector3(-1.5f,1.11f,8.27f),new Vector3(0.2f,1.37f,8.27f),new Vector3(1.75f,1.70f,8.27f) },0.062f,"Ink black");
            Stroke("Graffiti underline echo", wall, new[] { new Vector3(-1.27f,0.93f,8.26f),new Vector3(0.6f,1.19f,8.26f),new Vector3(1.94f,1.47f,8.26f) },0.045f,"Ink black");
            Vector3 crown = new Vector3(-1.28f,3.8f,8.27f);
            Stroke("Little graffiti crown", wall, Offset(crown,new[] { new Vector2(-0.35f,-0.17f),new Vector2(-0.43f,0.29f),new Vector2(-0.12f,0.05f),new Vector2(0,0.45f),new Vector2(0.13f,0.09f),new Vector2(0.43f,0.42f),new Vector2(0.36f,-0.09f),new Vector2(-0.35f,-0.17f) }),0.045f,"Ink black");
            for (int i = -1; i <= 1; i++) Dot("Crown dot",wall,crown + new Vector3(i * 0.21f,0,0),0.06f,"Ink black");
            BuildDoodle(wall, new Vector3(7.9f,2.55f,8.26f));
            Transform fence = Group("Left chain-link fence",world);
            for (int i = 0; i < 4; i++) Box("Fence upright",fence,new Vector3(-20 + i * 3.9f,1.95f,8.45f),new Vector3(0.095f,3.8f,0.1f),"Steel");
            for (int side = 0; side < 2; side++) Box("Fence rail",fence,new Vector3(-14.0f,0.3f + side * 3.55f,8.45f),new Vector3(12.2f,0.09f,0.095f),"Steel");
            for (float x = -23; x < -4; x += 0.36f)
            {
                FenceDiagonal(fence,x,1); FenceDiagonal(fence,x,-1);
            }
            for (int i = 0; i < 19; i++) Bush(wall, new Vector3(-14.6f + i * 1.8f,0.25f,7.62f), 0.58f + (i % 3) * 0.12f, i + 81);
        }

        private static void FenceDiagonal(Transform parent, float x, int direction)
        {
            float z = 8.47f, y0 = 0.3f, y1 = 3.83f, x0 = x, x1 = x + direction * 2.4f;
            const float left = -20, right = -8.3f;
            float start = 0, end = 1, dx = x1 - x0;
            if (direction > 0) { start = Mathf.Max(0,(left - x0) / dx); end = Mathf.Min(1,(right - x0) / dx); }
            else { start = Mathf.Max(0,(right - x0) / dx); end = Mathf.Min(1,(left - x0) / dx); }
            if (start >= end) return;
            Stroke("Diamond fence wire",parent,new[] { new Vector3(Mathf.Lerp(x0,x1,start),Mathf.Lerp(y0,y1,start),z),new Vector3(Mathf.Lerp(x0,x1,end),Mathf.Lerp(y0,y1,end),z) },0.012f,"Steel");
        }

        private static void BuildDoodle(Transform parent, Vector3 center)
        {
            Vector3 P(float x,float y) => center + Quaternion.Euler(0,0,-13) * new Vector3(x,y,0);
            Stroke("Cube face graffiti outline",parent,new[] { P(-0.86f,-0.76f),P(-0.86f,0.76f),P(0.86f,0.76f),P(0.86f,-0.76f),P(-0.86f,-0.76f) },0.075f,"Ink black");
            foreach (float x in new[] { -0.37f,0.39f })
            {
                Stroke("X eye",parent,new[] { P(x - 0.15f,0.43f),P(x + 0.15f,0.1f) },0.065f,"Ink black");
                Stroke("X eye",parent,new[] { P(x + 0.15f,0.43f),P(x - 0.15f,0.1f) },0.065f,"Ink black");
            }
            Stroke("Doodle mouth",parent,new[] { P(-0.35f,-0.16f),P(0.44f,-0.16f) },0.075f,"Ink black");
            Stroke("Doodle tongue",parent,new[] { P(-0.08f,-0.17f),P(-0.06f,-0.51f),P(0.03f,-0.58f),P(0.19f,-0.58f),P(0.27f,-0.46f),P(0.25f,-0.16f) },0.064f,"Ink black");
            Stroke("Tongue seam",parent,new[] { P(0.1f,-0.2f),P(0.1f,-0.4f) },0.047f,"Ink black");
            foreach (Vector2 p in new[] { new Vector2(-1.3f,0.7f),new Vector2(1.2f,0.9f),new Vector2(1.36f,-0.48f),new Vector2(-1.30f,-0.72f) })
                Stroke("Graffiti spark",parent,new[] { P(p.x,p.y),P(p.x + Mathf.Sign(p.x) * 0.22f,p.y + 0.14f) },0.075f,"Ink black");
        }

        private static void BuildBackdrop()
        {
            Transform backdrop = Group("Layered neighborhood and evergreen canopy",world);
            Building(backdrop,"Left parking garage",new Vector3(-12.3f,0,14.5f),new Vector3(7.8f,7.9f,5.8f),true);
            Building(backdrop,"Central water-tower office",new Vector3(1.7f,0,26),new Vector3(4.8f,8.7f,5),false);
            Building(backdrop,"Right neighborhood office",new Vector3(14.7f,0,23),new Vector3(6,7.7f,5),false);
            Building(backdrop,"Distant apartment block",new Vector3(10,0,38),new Vector3(5.3f,7.2f,5),false);
            Building(backdrop,"Distant left rooftops",new Vector3(-5,0,36),new Vector3(5,6.6f,4),false);
            Transform tank = Group("Rooftop water tank and safety railing",backdrop);
            tank.localPosition = new Vector3(1.9f,8.9f,26);
            for (int i = 0; i < 4; i++) Box("Tank leg",tank,new Vector3(i % 2 * 0.9f - 0.45f,0.6f,i / 2 * 0.9f - 0.45f),new Vector3(0.10f,1.2f,0.1f),"Steel");
            Part("Water tank",tank,cone,M("Office walls"),new Vector3(0,1.9f,0),new Vector3(1.4f,1.6f,1.4f));
            Part("Tank pointed roof",tank,cone,M("Garage walls"),new Vector3(0,3.00f,0),new Vector3(1.7f,0.6f,1.7f));
            // Small flat tank barrel replaces the cone's taper with a regular box silhouette.
            Box("Tank square body",tank,new Vector3(0,1.9f,0),new Vector3(1.15f,1.65f,1.15f),"Office walls");
            for (int i = 0; i < 8; i++)
            {
                Box("Roof railing upright",tank,new Vector3(-2 + i * 0.57f,0.55f,-2.1f),new Vector3(0.065f,1.1f,0.065f),"Steel");
                Box("Roof railing upright",tank,new Vector3(2.0f,0.55f,-2.1f + i * 0.57f),new Vector3(0.065f,1.1f,0.065f),"Steel");
            }
            Box("Roof rail",tank,new Vector3(0,1.08f,-2.1f),new Vector3(4.2f,0.065f,0.065f),"Steel");
            Box("Roof rail",tank,new Vector3(2.0f,1.08f,0),new Vector3(0.065f,0.065f,4.2f),"Steel");
            float[] treeX = { -24,-19,-14,-9,-5,5.8f,10.8f,18.3f,23,28,-30 };
            for (int i = 0; i < treeX.Length; i++) Pine(backdrop,new Vector3(treeX[i],0,19 + i % 3 * 4),9 + i % 4 * 1.0f,i);
            for (int i = 0; i < 32; i++) Bush(backdrop,new Vector3(-27 + i * 1.8f,3.6f,13 + i % 4 * 2),2.7f + i % 3 * 0.45f,i + 5);
            for (int i = 0; i < 9; i++)
            {
                Transform hill = Part("Distant blue-green hillside",backdrop,foliage,M("Distant mountain"),new Vector3(-70 + i * 19,4,80 + i % 2 * 10),new Vector3(29,15 + i % 3 * 3,20));
                hill.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Transform clouds = Group("Puffy white clouds",world);
            var random = new System.Random(72);
            for (int i = 0; i < 9; i++)
            {
                Vector3 center = new Vector3(-65 + i * 16,16 + i % 3 * 6,65 + i % 2 * 15);
                for (int j = 0; j < 7; j++)
                {
                    Transform lobe = Part("Soft cloud lobe",clouds,cloud,M("Cloud white"),center + new Vector3(j * 1.8f,(float)random.NextDouble() * 2.3f,j % 2),new Vector3(5.5f,3.4f + j % 3,3.6f));
                    lobe.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        private static void Building(Transform parent,string name,Vector3 position,Vector3 size,bool garage)
        {
            Transform building = Group(name,parent); building.localPosition = position;
            float w = size.x, h = size.y, d = size.z;
            Box("Building shell",building,new Vector3(0,h * 0.5f,0),size,garage ? "Garage walls" : "Office walls");
            Box("Flat roof lip",building,new Vector3(0,h,0),new Vector3(w + 0.65f,0.3f,d + 0.65f),"Light concrete");
            Box("Roof shadow band",building,new Vector3(0,h - 0.4f,-d * 0.5f - 0.12f),new Vector3(w + 0.3f,0.18f,0.3f),"Aged concrete");
            if (garage)
            {
                Box("Wooden garage door",building,new Vector3(-w * 0.22f,2.8f,-d * 0.5f - 0.04f),new Vector3(2.8f,4.6f,0.1f),"Wood light");
                for (int i = 0; i < 8; i++) Box("Garage door slat",building,new Vector3(-w * 0.22f,0.8f + i * 0.53f,-d * 0.5f - 0.11f),new Vector3(2.8f,0.055f,0.04f),"Wood");
                Window(building,2.0f,4.55f,-d * 0.5f - 0.10f,2.7f,3.2f);
                Box("Orange service door",building,new Vector3(2.0f,1.4f,-d * 0.5f - 0.2f),new Vector3(2.3f,2.5f,0.12f),"Wood light");
            }
            else
            {
                for (int row = 0; row < 2; row++) for (int col = -1; col <= 1; col += 2)
                    Window(building,col * w * 0.24f,4.4f + row * 2.45f,-d * 0.5f - 0.08f,w * 0.25f,1.3f);
                for (int row = 0; row < 2; row++)
                {
                    Box("Side window frame",building,new Vector3(w * 0.5f + 0.06f,4.4f + row * 2.45f,-0.6f),new Vector3(0.15f,1.6f,1.3f),"Aged concrete");
                    Box("Side window glass",building,new Vector3(w * 0.5f + 0.15f,4.4f + row * 2.45f,-0.6f),new Vector3(0.05f,1.3f,1.0f),"Window blue");
                }
                Box("Roof chimney",building,new Vector3(w * 0.25f,h + 0.5f,0.5f),new Vector3(0.8f,1.0f,0.8f),"Aged concrete");
            }
        }

        private static void Window(Transform parent,float x,float y,float z,float w,float h)
        {
            Box("Window frame",parent,new Vector3(x,y,z),new Vector3(w + 0.17f,h + 0.17f,0.1f),"Light concrete");
            Box("Blue glazing",parent,new Vector3(x,y,z - 0.065f),new Vector3(w,h,0.04f),"Window blue");
            Box("Window mullion",parent,new Vector3(x,y,z - 0.1f),new Vector3(0.08f,h,0.04f),"Aged concrete");
            Box("Window mullion",parent,new Vector3(x,y,z - 0.1f),new Vector3(w,0.08f,0.04f),"Aged concrete");
        }

        private static void Pine(Transform parent,Vector3 position,float height,int seed)
        {
            Transform tree = Group("Layered low-poly evergreen",parent); tree.localPosition = position;
            Box("Pine trunk",tree,new Vector3(0,height * 0.32f,0),new Vector3(0.33f,height * 0.64f,0.33f),"Wood");
            for (int tier = 0; tier < 3; tier++)
            {
                float width = height * (0.37f - tier * 0.075f), tall = height * 0.45f;
                Transform part = Part("Faceted pine crown",tree,cone,M((seed + tier) % 3 == 0 ? "Pine light" : "Pine dark"),
                    new Vector3(0,height * (0.20f + tier * 0.21f),0),new Vector3(width,tall,width));
                part.localRotation = Quaternion.Euler(0,seed * 37 + tier * 15,0);
            }
        }

        private static void Bush(Transform parent,Vector3 position,float size,int seed)
        {
            Transform bush = Group("Faceted leafy shrub",parent); bush.localPosition = position;
            var random = new System.Random(seed);
            for (int j = 0; j < 6; j++)
            {
                Vector3 offset = new Vector3(((float)random.NextDouble() - 0.5f) * size,size * (0.23f + (float)random.NextDouble() * 0.32f),((float)random.NextDouble() - 0.5f) * size * 0.7f);
                Part("Angular foliage clump",bush,foliage,M(j % 3 == 0 ? "Leaves sunlit" : j % 3 == 1 ? "Leaves green" : "Leaves dark"),offset,Vector3.one * size * (0.75f + j % 2 * 0.15f));
            }
            for (int j = 0; j < 4 && size < 1; j++)
            {
                Transform blade = Part("Curbside grass blade",bush,leaf,M("Leaves green"),new Vector3((j - 1.5f) * 0.13f,0,0.18f),new Vector3(0.6f,1.2f,0.5f));
                blade.localRotation = Quaternion.Euler(0,j * 85,-45);
            }
        }

        private static void BuildStreetProps()
        {
            Transform props = Group("Parking-lot street props",world);
            BuildSedan(props);
            Cone(props,new Vector3(-3.6f,0.02f,6.2f),0.8f);
            Cone(props,new Vector3(-4.5f,0.02f,6.3f),0.52f);
            Cone(props,new Vector3(3.5f,0,-5.7f),1.7f);
            Barrel(props,new Vector3(13.8f,0.2f,7.4f),1.35f);
            Crate(props,new Vector3(13.3f,0.2f,8.0f));
            Crate(props,new Vector3(-11.5f,0.2f,6.8f));
            BuildParkingSign(props);
            UtilityPole(props,new Vector3(-6.3f,0,10),10.9f);
            UtilityPole(props,new Vector3(13.9f,0,9.8f),11.8f);
            UtilityPole(props,new Vector3(-17.5f,0,10),9.1f);
            UtilityPole(props,new Vector3(7.8f,0,32),11.5f);
            foreach (float offset in new[] { 0f,0.25f })
            {
                Cable(props,new Vector3(-17.5f,8.7f + offset,10),new Vector3(-6.3f,10.35f + offset,10));
                Cable(props,new Vector3(-6.3f,10.35f + offset,10),new Vector3(7.8f,11.0f + offset,32));
                Cable(props,new Vector3(7.8f,11.0f + offset,32),new Vector3(13.9f,11.3f + offset,9.8f));
            }
            Transform barrier = Group("Foreground yellow-black construction barrier",props);
            barrier.localPosition = new Vector3(-2.8f,0,-6.3f); barrier.localRotation = Quaternion.Euler(0,-8,0);
            Box("Black barrier panel",barrier,new Vector3(0,0.91f,0),new Vector3(3.2f,1.62f,0.34f),"Lamp frame");
            for (int i = 0; i < 5; i++)
            {
                Transform stripe = Box("Yellow diagonal safety stripe",barrier,new Vector3(-1.24f + i * 0.62f,0.91f,-0.18f),new Vector3(0.34f,1.55f,0.018f),"Safety yellow");
                stripe.localRotation = Quaternion.Euler(0,0,12);
            }
            Box("Barrier top rail",barrier,new Vector3(0,1.78f,0),new Vector3(3.43f,0.15f,0.45f),"Steel");
            Box("Barrier bottom rail",barrier,new Vector3(0,0.1f,0),new Vector3(3.4f,0.18f,0.46f),"Steel");
            for (int side = -1; side <= 1; side += 2) Box("Barrier edge frame",barrier,new Vector3(side * 1.65f,0.94f,0),new Vector3(0.14f,1.65f,0.44f),"Safety yellow");
            Box("Foreground concrete curb",props,new Vector3(0.7f,0.13f,-6.0f),new Vector3(5.5f,0.26f,0.35f),"Light concrete");
            Box("Foreground curb paint",props,new Vector3(0.2f,0.27f,-6.0f),new Vector3(0.9f,0.016f,0.36f),"Worn curb yellow");
        }

        private static void BuildSedan(Transform parent)
        {
            Transform car = Group("Parked classic blue sedan",parent);
            car.localPosition = new Vector3(-7.4f,0.06f,6); car.localRotation = Quaternion.Euler(0,14,0);
            Box("Car chassis",car,new Vector3(0,0.71f,0),new Vector3(3.15f,0.82f,5.15f),"Sedan blue");
            Box("Car hood",car,new Vector3(0,1.1f,-1.73f),new Vector3(3.0f,0.26f,1.48f),"Sedan blue");
            Box("Cabin roof",car,new Vector3(0,2.23f,0.2f),new Vector3(2.64f,0.18f,2.6f),"Sedan blue");
            Box("Cabin glazing",car,new Vector3(0,1.72f,0.2f),new Vector3(2.70f,0.96f,2.59f),"Sedan glass");
            for (int side = -1; side <= 1; side += 2)
            {
                for (int post = 0; post < 3; post++) Box("Window pillar",car,new Vector3(side * 1.39f,1.76f,-1.1f + post * 1.25f),new Vector3(0.075f,1.06f,0.095f),"Sedan trim");
                Box("Door chrome",car,new Vector3(side * 1.6f,0.83f,0.24f),new Vector3(0.04f,0.06f,3.3f),"Sedan trim");
                for (int axle = -1; axle <= 1; axle += 2)
                {
                    Part("Black tire",car,cloud,M("Ink black"),new Vector3(side * 1.50f,0.49f,axle * 1.63f),new Vector3(0.48f,0.95f,0.95f));
                    Part("Wheel hub",car,cloud,M("Steel"),new Vector3(side * 1.75f,0.49f,axle * 1.63f),new Vector3(0.10f,0.55f,0.55f));
                }
                Box("Amber indicator",car,new Vector3(side * 1.38f,0.91f,-2.60f),new Vector3(0.27f,0.33f,0.04f),"Safety yellow");
                Box("Headlamp",car,new Vector3(side * 1.03f,0.91f,-2.61f),new Vector3(0.38f,0.35f,0.05f),"Paint white");
            }
            Box("Front chrome bumper",car,new Vector3(0,0.46f,-2.72f),new Vector3(3.28f,0.35f,0.28f),"Sedan trim");
            Box("Front radiator grille",car,new Vector3(0,0.91f,-2.61f),new Vector3(1.64f,0.35f,0.04f),"Ink black");
            for (int i = 0; i < 12; i++) Box("Grille chrome slat",car,new Vector3(-0.76f + i * 0.138f,0.91f,-2.635f),new Vector3(0.035f,0.34f,0.016f),"Steel");
            Box("License plate",car,new Vector3(0,0.48f,-2.876f),new Vector3(0.58f,0.16f,0.018f),"Paint white");
            WorldText("Sedan license lettering",car,"CUBE 01",new Vector3(0,0.48f,-2.891f),0.013f,displayFont,Color.black);
        }

        private static void Cone(Transform parent,Vector3 position,float height)
        {
            Transform root = Group("Orange-white traffic cone",parent); root.localPosition = position;
            Box("Cone base",root,new Vector3(0,height * 0.06f,0),new Vector3(height * 0.64f,height * 0.12f,height * 0.64f),"Cone orange");
            Part("Orange tapered cone",root,cone,M("Cone orange"),new Vector3(0,height * 0.10f,0),new Vector3(height * 0.48f,height * 0.90f,height * 0.48f));
            for (int stripe = 0; stripe < 2; stripe++)
            {
                float y = height * (0.31f + stripe * 0.27f), w = height * (0.42f - stripe * 0.14f);
                Mesh ring = MeshAsset("Cone band " + height.ToString("F2",System.Globalization.CultureInfo.InvariantCulture) + " " + stripe,
                    Frustum(0.5f,0.37f,1,12));
                Part("White reflective cone band",root,ring,M("Paint white"),new Vector3(0,y,0),new Vector3(w,height * 0.14f,w));
            }
        }

        private static void Barrel(Transform parent,Vector3 position,float height)
        {
            Transform root = Group("Orange-white street barrel",parent); root.localPosition = position;
            Mesh barrel = MeshAsset("Street barrel cylinder",Frustum(0.5f,0.5f,1,16));
            Part("Barrel body",root,barrel,M("Cone orange"),Vector3.zero,new Vector3(0.85f,height,0.85f));
            foreach (float y in new[] { 0.27f,0.66f }) Part("Barrel reflective stripe",root,barrel,M("Paint white"),new Vector3(0,y * height,0),new Vector3(0.87f,height * 0.16f,0.87f));
            Part("Barrel foot",root,barrel,M("Cone orange"),Vector3.zero,new Vector3(1.0f,0.12f,1.0f));
        }

        private static void Crate(Transform parent,Vector3 position)
        {
            Transform root = Group("Weathered wooden crate",parent); root.localPosition = position;
            Box("Crate body",root,new Vector3(0,0.48f,0),new Vector3(1.05f,0.94f,0.9f),"Wood");
            for (int i = 0; i < 6; i++) Box("Crate plank",root,new Vector3(-0.47f + i * 0.185f,0.48f,-0.47f),new Vector3(0.16f,0.9f,0.04f),"Wood light");
            foreach (float y in new[] { 0.14f,0.79f }) Box("Crate cross brace",root,new Vector3(0,y,-0.51f),new Vector3(1.12f,0.13f,0.06f),"Wood");
        }

        private static void UtilityPole(Transform parent,Vector3 position,float height)
        {
            Transform pole = Group("Timber utility pole and streetlight",parent); pole.localPosition = position;
            Box("Wood utility pole",pole,new Vector3(0,height * 0.5f,0),new Vector3(0.30f,height,0.30f),"Wood light");
            for (int level = 0; level < 2; level++)
            {
                Box("Pole crossarm",pole,new Vector3(0,height - 0.65f - level * 0.61f,0),new Vector3(1.75f,0.18f,0.19f),"Wood");
                for (int side = -1; side <= 1; side += 2) Box("Cable insulator",pole,new Vector3(side * 0.62f,height - 0.43f - level * 0.61f,0),new Vector3(0.09f,0.31f,0.09f),"Steel");
            }
            Stroke("Streetlamp bracket",pole,new[] { new Vector3(0,height - 1.2f,0),new Vector3(1.05f,height - 1.15f,-0.2f),new Vector3(1.27f,height - 1.38f,-0.3f) },0.08f,"Steel");
            Box("Streetlamp hood",pole,new Vector3(1.27f,height - 1.46f,-0.3f),new Vector3(0.72f,0.12f,0.46f),"Lamp frame");
            Box("Streetlamp warm lens",pole,new Vector3(1.27f,height - 1.54f,-0.3f),new Vector3(0.56f,0.05f,0.33f),"Warm lamp");
        }

        private static void Cable(Transform parent,Vector3 a,Vector3 b)
        {
            Vector3[] points = new Vector3[25];
            for (int i = 0; i < points.Length; i++) { float t = i / 24f; points[i] = Vector3.Lerp(a,b,t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 1.35f; }
            Stroke("Sagging overhead cable",parent,points,0.037f,"Ink black");
        }

        private static void BuildParkingSign(Transform parent)
        {
            Transform sign = Group("Blue P parking sign",parent); sign.localPosition = new Vector3(11.7f,0.18f,7.38f);
            Box("Parking sign concrete foot",sign,new Vector3(0,0.18f,0),new Vector3(0.67f,0.36f,0.60f),"Light concrete");
            Box("Parking sign steel post",sign,new Vector3(0,1.67f,0),new Vector3(0.09f,3.34f,0.09f),"Steel");
            Box("Parking sign white rim",sign,new Vector3(0,3.15f,-0.01f),new Vector3(1.4f,1.65f,0.11f),"Paint white");
            Box("Parking sign blue face",sign,new Vector3(0,3.15f,-0.085f),new Vector3(1.27f,1.51f,0.05f),"Parking blue");
            WorldText("Parking P",sign,"P",new Vector3(0,3.18f,-0.118f),0.115f,displayFont,Color.white);
        }

        private static Renderer BuildHero()
        {
            PlayerCubePresentationBuilder.PrepareAssets();
            PlayerCubeVisual visual = PlayerCubePresentationBuilder.InstantiatePreview(world);
            Transform hero = visual.transform.parent;
            hero.localPosition = new Vector3(3.0f,0.74f,-0.9f); hero.localRotation = Quaternion.Euler(0,35,0);
            hero.localScale = Vector3.one * 1.45f;
            return visual.Body;
        }

        private static void ConfigureLighting(Transform parent)
        {
            Light sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (sun == null) sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(49,-32,0);
            sun.color = new Color(1,0.96f,0.87f); sun.intensity = 2.4f; sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f; sun.shadowBias = 0.025f; sun.shadowNormalBias = 0.20f;
            RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.40f,0.55f,0.69f);
            RenderSettings.ambientEquatorColor = new Color(0.42f,0.46f,0.49f);
            RenderSettings.ambientGroundColor = new Color(0.30f,0.32f,0.30f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.52f,0.77f,0.92f); RenderSettings.fogStartDistance = 52; RenderSettings.fogEndDistance = 160;
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Blue summer sky.mat");
            if (sky == null) { sky = new Material(Shader.Find("CubeDash/Horizon Skybox")); AssetDatabase.CreateAsset(sky,Art + "Materials/Blue summer sky.mat"); }
            sky.SetColor("_ZenithColor",new Color(0.035f,0.35f,0.83f)); sky.SetColor("_HorizonColor",new Color(0.30f,0.69f,0.95f));
            sky.SetColor("_GroundColor",new Color(0.30f,0.46f,0.43f)); sky.SetFloat("_GradientPower",0.45f);
            sky.SetFloat("_CloudOpacity",0); sky.SetColor("_SunColor",new Color(1.3f,1.15f,0.88f));
            sky.SetVector("_SunDirection",-sun.transform.forward); sky.SetFloat("_SunSize",0.6f);
            RenderSettings.skybox = sky; EditorUtility.SetDirty(sky);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(MainMenuGraphics.ProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,MainMenuGraphics.ProfilePath); }
            MainMenuGraphics.ConfigureProfile(profile);
            var volume = new GameObject("Menu daylight grading").AddComponent<Volume>(); volume.transform.SetParent(parent,false); volume.isGlobal = true; volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
        }

        private static void BuildUI(Transform parent,MainMenuController controller,Camera camera)
        {
            RectTransform canvas = Rect("Reference Menu Canvas",parent);
            Canvas c = canvas.gameObject.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 20;
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1672,940); scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            RectTransform buttons = Rect("Left menu ribbons",canvas); Stretch(buttons);
            Button start = Ribbon(buttons,"START",MenuGlyphGraphic.Glyph.Play,new Vector2(62,-260),new Vector2(455,122),true,controller.StartGame);
            Button shop = Ribbon(buttons,"SHOP",MenuGlyphGraphic.Glyph.Cart,new Vector2(66,-404),new Vector2(395,102),false,controller.OpenShop);
            Button options = Ribbon(buttons,"OPTIONS",MenuGlyphGraphic.Glyph.Gear,new Vector2(66,-511),new Vector2(395,106),false,controller.OpenOptions);
            Button quit = Ribbon(buttons,"QUIT",MenuGlyphGraphic.Glyph.Exit,new Vector2(66,-623),new Vector2(395,112),false,controller.Quit);
            Button[] navigation = { start,shop,options,quit };
            for (int i = 0; i < navigation.Length; i++) navigation[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = navigation[(i + 3) % 4],selectOnDown = navigation[(i + 1) % 4] };
            Set(controller,"startButton",start);
            BuildPanels(canvas,controller);
            EventSystem events = Object.FindAnyObjectByType<EventSystem>();
            if (events == null)
            {
                events = new GameObject("Menu EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
                events.transform.SetParent(parent,false);
            }
            events.firstSelectedGameObject = start.gameObject;
            // Keep a navigation anchor when mouse clicks miss the menu controls.
            var module = events.GetComponent<InputSystemUIInputModule>();
            if (module != null) module.deselectOnBackgroundClick = false;
        }

        private static void BuildPanels(RectTransform canvas,MainMenuController controller)
        {
            MainMenuSections.ApplyToScene(canvas.gameObject.scene);
        }

        private static Button Ribbon(Transform parent,string name,MenuGlyphGraphic.Glyph glyph,Vector2 position,Vector2 size,bool primary,UnityAction action)
        {
            RectTransform root = Rect(name,parent); root.anchorMin = root.anchorMax = new Vector2(0,1); root.pivot = new Vector2(0,1); root.anchoredPosition = position; root.sizeDelta = size;
            MenuRibbonGraphic ribbon = root.gameObject.AddComponent<MenuRibbonGraphic>(); ribbon.Primary = primary;
            Button button = root.gameObject.AddComponent<Button>(); button.targetGraphic = ribbon; button.transition = Selectable.Transition.None;
            UnityEventTools.AddPersistentListener(button.onClick,action); root.gameObject.AddComponent<ArcadeButton>();
            RectTransform icon = Rect("Vector icon",root); icon.anchorMin = icon.anchorMax = new Vector2(0,0.5f); icon.pivot = new Vector2(0,0.5f);
            icon.anchoredPosition = new Vector2(39,0); icon.sizeDelta = new Vector2(primary ? 62 : 68,primary ? 62 : 68);
            MenuGlyphGraphic graphic = icon.gameObject.AddComponent<MenuGlyphGraphic>(); graphic.Symbol = glyph; graphic.color = primary ? new Color(1,0.98f,0.85f) : Color.black; graphic.raycastTarget = false;
            Text label = Label("Label",root,name,primary ? 65 : 47,primary ? new Color(1,0.99f,0.94f) : Color.black,true);
            label.alignment = TextAnchor.MiddleLeft; label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0,0.5f);
            label.rectTransform.pivot = new Vector2(0,0.5f); label.rectTransform.anchoredPosition = new Vector2(primary ? 148 : 136,primary ? 1 : 0);
            label.rectTransform.sizeDelta = new Vector2(size.x - 152,size.y - 15);
            return button;
        }

        private static Text Label(string name,Transform parent,string value,int size,Color color,bool sharp = false)
        {
            GameObject root = Rect(name,parent).gameObject;
            Text label = sharp ? root.AddComponent<MenuLabelText>() : root.AddComponent<Text>(); label.text = value; label.font = displayFont; label.fontSize = size;
            label.color = color; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }

        private static void ConfigureBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath,true) };
            bool level = false;
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (entry.path == ScenePath) continue;
                if (entry.path == "Assets/Scenes/Level.unity") { level = true; scenes.Add(new EditorBuildSettingsScene(entry.path,true)); }
                else scenes.Add(entry);
            }
            if (!level) scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Level.unity",true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void BuildAndCaptureFromCommandLine()
        {
            BuildFromCommandLine();
            CaptureFromCommandLine();
        }

        public static void CaptureFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Camera camera = Object.FindAnyObjectByType<Camera>();
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            var oldMode = canvas.renderMode; Camera oldCamera = canvas.worldCamera;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
            var target = new RenderTexture(1672,940,24); var image = new Texture2D(1672,940,TextureFormat.RGB24,false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; Canvas.ForceUpdateCanvases();
                bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
                try
                {
                    ShaderUtil.allowAsyncCompilation = false;
                    for (int frame = 0; frame < 3; frame++) camera.Render();
                }
                finally { ShaderUtil.allowAsyncCompilation = asyncCompilation; }
                RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1672,940),0,0); image.Apply();
                string path = Environment.GetEnvironmentVariable("CUBE_DASH_MENU_PREVIEW") ?? Path.Combine(Path.GetDirectoryName(Application.dataPath),"main-menu-preview.png");
                File.WriteAllBytes(path,image.EncodeToPNG()); Debug.Log("Menu preview saved to " + path);
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
                Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            }
        }

        private static Material M(string name) => palette[name];
        private static void Lit(string name,Color color,float emission = 0,float smoothness = 0.16f,float metallic = 0)
        {
            Material material = MaterialAsset(name,Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",smoothness); material.SetFloat("_Metallic",metallic);
            if (emission > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",color * emission); }
            else { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor",Color.black); }
            palette[name] = material; EditorUtility.SetDirty(material);
        }
        private static void Surface(string name,Color color,Color weather,float type)
        {
            Material material = MaterialAsset(name,Shader.Find("CubeDash/Menu Surface"));
            material.SetColor("_BaseColor",color); material.SetColor("_WeatherColor",weather); material.SetFloat("_SurfaceType",type);
            palette[name] = material; EditorUtility.SetDirty(material);
        }
        private static Material MaterialAsset(string name,Shader shader)
        {
            if (shader == null) throw new InvalidOperationException("Missing menu shader for " + name);
            string path = Art + "Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = name,enableInstancing = true }; AssetDatabase.CreateAsset(material,path); }
            material.shader = shader; return material;
        }
        private static Transform Group(string name,Transform parent)
        {
            var root = new GameObject(name).transform; root.SetParent(parent,false); return root;
        }
        private static Transform Box(string name,Transform parent,Vector3 position,Vector3 scale,string material)
            => Part(name,parent,cube,M(material),position,scale);
        private static Transform Part(string name,Transform parent,Mesh mesh,Material material,Vector3 position,Vector3 scale)
        {
            Transform root = Group(name,parent); root.localPosition = position; root.localScale = scale;
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material; return root;
        }
        private static void Dot(string name,Transform parent,Vector3 point,float radius,string material)
            => Part(name,parent,cloud,M(material),point,new Vector3(radius * 2,radius * 2,0.014f));
        private static Vector3[] Offset(Vector3 origin,Vector2[] points)
        {
            var result = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) result[i] = origin + new Vector3(points[i].x,points[i].y,0);
            return result;
        }
        private static void Stroke(string name,Transform parent,Vector3[] points,float width,string material)
        {
            for (int i = 0; i < points.Length - 1; i++)
            {
                Transform piece = Box(name,parent,(points[i] + points[i + 1]) * 0.5f,new Vector3(width,width,Vector3.Distance(points[i],points[i + 1])),material);
                piece.localRotation = Quaternion.LookRotation(points[i + 1] - points[i],Vector3.up);
            }
        }
        private static void WorldText(string name,Transform parent,string value,Vector3 position,float size,Font font,Color color,float tilt = 0)
        {
            Transform root = Group(name,parent); root.localPosition = position; root.localRotation = Quaternion.Euler(0,0,tilt);
            TextMesh text = root.gameObject.AddComponent<TextMesh>(); text.text = value; text.font = font; text.fontSize = 96; text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = color; text.lineSpacing = 0.83f;
            root.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        private static Mesh Frustum(float bottom = 0.5f,float top = 0,float height = 1,int sides = 8)
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            for (int side = 0; side < sides; side++)
            {
                float a = side * Mathf.PI * 2 / sides, b = (side + 1) * Mathf.PI * 2 / sides;
                Vector3 loA = new Vector3(Mathf.Cos(a) * bottom,0,Mathf.Sin(a) * bottom), loB = new Vector3(Mathf.Cos(b) * bottom,0,Mathf.Sin(b) * bottom);
                Vector3 hiA = new Vector3(Mathf.Cos(a) * top,height,Mathf.Sin(a) * top), hiB = new Vector3(Mathf.Cos(b) * top,height,Mathf.Sin(b) * top);
                int i = vertices.Count; vertices.AddRange(new[] { loA,hiA,loB,hiB,Vector3.zero,loB,loA,Vector3.up * height,hiA,hiB });
                indices.AddRange(new[] { i,i + 1,i + 2,i + 2,i + 1,i + 3,i + 4,i + 5,i + 6,i + 7,i + 8,i + 9 });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh MeshAsset(string name,Mesh mesh)
        {
            string path = Art + "Meshes/" + name + ".asset"; mesh.name = name;
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh,path); return mesh; }
            EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
        }
        private static RectTransform Rect(string name,Transform parent)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false); return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Place(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f,0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private static void Set(Object target,string field,Object value)
        {
            var settings = new SerializedObject(target); settings.FindProperty(field).objectReferenceValue = value; settings.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Folder(string path)
        {
            path = path.TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}
