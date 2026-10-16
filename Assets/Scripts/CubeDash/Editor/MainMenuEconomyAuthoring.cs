using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Adds saved economy UI and a menu-only car preview; never rebuilds the diorama or maps.</summary>
    public static class MainMenuEconomyAuthoring
    {
        public const string IllustrationPath = "Assets/MainMenu/Epic Supercar.png";
        private static readonly Color Ink = new Color(0.018f, 0.014f, 0.025f);
        private static readonly Color Cream = new Color(1, 0.99f, 0.94f);
        private static readonly Color Paper = new Color(1, 0.82f, 0.84f);
        private static readonly Color Epic = new Color(0.61f, 0.27f, 0.78f);

        [MenuItem("Tools/Cube Dash/Add Wallet and Epic Supercar Shop")]
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            var scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the economy UI.");
            AssetDatabase.SaveAssets();
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            if (menu.GetComponent<MenuSkinShop>() != null) return; // Preserve existing artwork and manual shop edits.
            BakeIllustration();
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            var canvas = menu.GetComponentInChildren<Canvas>(true).GetComponent<RectTransform>();
            var shop = menu.gameObject.AddComponent<MenuSkinShop>();
            var safe = Rect("Economy Safe Area", canvas, Vector2.zero, Vector2.zero);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
            safe.SetAsFirstSibling();
            var wallet = Rect("Coin Wallet", safe, new Vector2(68, -48), new Vector2(252, 72));
            wallet.anchorMin = wallet.anchorMax = new Vector2(0, 1); wallet.pivot = new Vector2(0, 1);
            Text menuCoins = Wallet(wallet, font);
            var card = menu.ShopPanel.transform.Find("Card").GetComponent<RectTransform>();
            var footer = Rect("Shop Coin Wallet", card, new Vector2(-395, -270), new Vector2(252, 64));
            Text shopCoins = Wallet(footer, font);
            var status = Label("Shop Economy Status", card, font, "NEED 100 MORE COINS", 20, new Vector2(-60, -270), new Vector2(360, 48));

            Transform epic = card.Find("Epic Skins"); epic.Find("Empty state").gameObject.SetActive(false);
            var slots = epic.Find("Skin viewport/Skin slots").GetComponent<RectTransform>();
            var grid = slots.GetComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(284, 206); grid.constraintCount = 1;
            var product = Rect("Epic Supercar", slots, Vector2.zero, new Vector2(284, 206));
            Panel(product, new Color(0.95f, 0.86f, 1), Epic, 2, 10);
            Label("Name", product, font, "NEON SUPERCAR", 25, new Vector2(0, 76), new Vector2(266, 38));
            var imageRect = Rect("Car Model Illustration", product, new Vector2(0, 6), new Vector2(266, 108));
            var image = imageRect.gameObject.AddComponent<Image>(); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IllustrationPath);
            image.preserveAspect = true; image.raycastTarget = false;
            Label("Price", product, font, "100 COINS", 21, new Vector2(-73, -78), new Vector2(118, 42));
            var buyRect = Rect("Purchase Equip", product, new Vector2(66, -78), new Vector2(136, 44));
            var ribbon = buyRect.gameObject.AddComponent<MenuRibbonGraphic>(); ribbon.Primary = true;
            var action = buyRect.gameObject.AddComponent<Button>(); action.targetGraphic = ribbon; action.transition = Selectable.Transition.None;
            buyRect.gameObject.AddComponent<ArcadeButton>();
            var actionLabel = Label("Label", buyRect, font, "BUY", 24, new Vector2(-3, 1), new Vector2(125, 40)); actionLabel.color = Cream;
            UnityEventTools.AddPersistentListener(action.onClick, shop.PurchaseOrToggle);
            var back = card.Find("BACK").GetComponent<Button>();
            action.navigation = Nav(back); back.navigation = Nav(action);

            Transform cube = menu.Hero.transform.parent;
            var car = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Supercar.prefab"), scene);
            car.name = "Equipped Epic Supercar Preview"; car.transform.SetParent(cube.parent, false);
            car.transform.position = menu.Hero.bounds.center - Vector3.up * menu.Hero.bounds.extents.y;
            car.transform.rotation = Quaternion.Euler(0, -145, 0); car.transform.localScale = Vector3.one * 0.72f;
            car.SetActive(false);
            Set(shop, "menu", menu); Set(shop, "menuCoins", menuCoins); Set(shop, "shopCoins", shopCoins);
            Set(shop, "action", action); Set(shop, "actionLabel", actionLabel); Set(shop, "status", status);
            Set(shop, "cubePreview", cube.gameObject); Set(shop, "carPreview", car);
            var layout = menu.gameObject.AddComponent<MenuEconomyLayout>();
            Set(layout, "safeArea", safe); Set(layout, "canvas", canvas); Set(layout, "shopMotion", menu.ShopPanel.GetComponent<MenuPanelMotion>());
            MainMenuWalletPolish.ApplyToScene(scene);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuSkinsRevamp.MonsterPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuSkinsRevamp.MilitaryPath) != null)
                MainMenuSkinsRevamp.ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void BakeIllustration()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(IllustrationPath) != null) return;
            Scene previousScene = SceneManager.GetActiveScene();
            Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(preview);
            bool async = ShaderUtil.allowAsyncCompilation;
            Texture2D texture = null;
            RenderTexture target = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var car = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Supercar.prefab"));
                car.transform.position = Vector3.zero; car.transform.rotation = Quaternion.identity;
                foreach (Transform child in car.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var camera = new GameObject("Illustration Camera").AddComponent<Camera>();
                camera.enabled = false; camera.cullingMask = 1 << 31;
                camera.transform.position = new Vector3(6, 3.5f, 7);
                camera.transform.LookAt(new Vector3(0, 0.6f, 0));
                camera.orthographic = true; camera.orthographicSize = 1.65f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.95f, 0.86f, 1);
                camera.allowHDR = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);
                var light = new GameObject("Illustration Key").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.7f; light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.Euler(35, -35, 0);
                RenderSettings.sun = light;
                target = new RenderTexture(768, 432, 24); camera.targetTexture = target;
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(768, 432, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 768, 432), 0, 0); texture.Apply();
                File.WriteAllBytes(IllustrationPath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(IllustrationPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(IllustrationPath);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            finally
            {
                RenderTexture.active = previousTarget;
                SceneManager.SetActiveScene(previousScene); EditorSceneManager.CloseScene(preview, true);
                if (target != null) Object.DestroyImmediate(target);
                if (texture != null) Object.DestroyImmediate(texture);
                ShaderUtil.allowAsyncCompilation = async;
            }
        }

        private static Text Wallet(RectTransform rect, Font font)
        {
            Panel(rect, Paper, Ink, 3, 12);
            var coin = Rect("Coin", rect, new Vector2(-86, 0), new Vector2(44, 44));
            var graphic = coin.gameObject.AddComponent<MenuPanelGraphic>(); graphic.color = new Color(1, 0.72f, 0.12f);
            graphic.BorderColor = Ink; graphic.BorderWidth = 3; graphic.CornerRadius = 22; graphic.raycastTarget = false;
            Label("Coin Mark", coin, font, "C", 26, Vector2.zero, new Vector2(36, 38));
            Label("Caption", rect, font, "COINS", 15, new Vector2(24, 19), new Vector2(160, 24), TextAnchor.MiddleLeft);
            return Label("Balance", rect, font, "0", 30, new Vector2(24, -7), new Vector2(160, 40), TextAnchor.MiddleLeft);
        }

        private static Navigation Nav(Selectable other) => new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = other, selectOnDown = other, selectOnLeft = other, selectOnRight = other };
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void Panel(RectTransform rect, Color color, Color border, float width, float cut)
        {
            var graphic = rect.gameObject.AddComponent<MenuPanelGraphic>(); graphic.color = color; graphic.BorderColor = border;
            graphic.BorderWidth = width; graphic.CornerRadius = 0; graphic.CornerCut = cut; graphic.raycastTarget = false;
        }
        private static Text Label(string name, Transform parent, Font font, string text, int size, Vector2 position, Vector2 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var label = Rect(name, parent, position, dimensions).gameObject.AddComponent<MenuLabelText>();
            label.font = font; label.text = text; label.fontSize = size; label.color = Ink; label.alignment = align;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 13; label.resizeTextMaxSize = size;
            label.raycastTarget = false; return label;
        }
        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
