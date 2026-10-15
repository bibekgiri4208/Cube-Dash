using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Authors one reusable transition overlay; no map/menu scene rewrites are needed.</summary>
    public static class LoadingScreenAuthoring
    {
        public const string ImagePath = "Assets/2D Images/Loading Screen.png";
        public const string PrefabPath = "Assets/Resources/Loading Screen.prefab";

        [MenuItem("Tools/Cube Dash/Create Themed Loading Screen")]
        public static void ApplyFromCommandLine()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("Loading Screen is already authored; preserved existing artwork/layout edits.");
                return;
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ImagePath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            if (sprite == null || font == null) throw new InvalidOperationException("The Loading Screen sprite and Main Menu font are required.");
            var root = new GameObject("Loading Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(SceneLoader));
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1672, 940); scaler.matchWidthOrHeight = 0.5f;
                var blocker = Rect("Opaque Input Blocker", root.transform); Stretch(blocker);
                blocker.gameObject.AddComponent<Image>().color = Color.black;
                var artRect = Rect("Loading Artwork", root.transform); Stretch(artRect);
                var artwork = artRect.gameObject.AddComponent<Image>(); artwork.sprite = sprite; artwork.raycastTarget = false;
                var aspect = artRect.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; aspect.aspectRatio = sprite.rect.width / sprite.rect.height;

                var safe = Rect("Safe Area", root.transform); Stretch(safe);
                var card = Rect("Loading Card", safe);
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 0); card.pivot = new Vector2(0.5f, 0);
                card.anchoredPosition = new Vector2(0, 44); card.sizeDelta = new Vector2(980, 180);
                Panel(card, LevelMenuTheme.Paper, 4, 20);
                var title = Label("Loading Text", card, font, "LOADING...", 42, TextAnchor.MiddleLeft);
                title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = new Vector2(1, 1);
                title.rectTransform.pivot = new Vector2(0.5f, 1); title.rectTransform.anchoredPosition = new Vector2(-68, -20);
                title.rectTransform.sizeDelta = new Vector2(-204, 58);
                var destination = Label("Destination", card, font, "ENTERING MAP", 22, TextAnchor.MiddleLeft);
                destination.rectTransform.anchorMin = new Vector2(0, 1); destination.rectTransform.anchorMax = new Vector2(1, 1);
                destination.rectTransform.pivot = new Vector2(0.5f, 1); destination.rectTransform.anchoredPosition = new Vector2(-48, -80);
                destination.rectTransform.sizeDelta = new Vector2(-164, 36);
                var percentage = Label("Progress Percentage", card, font, "0%", 38, TextAnchor.MiddleRight);
                percentage.rectTransform.anchorMin = percentage.rectTransform.anchorMax = new Vector2(1, 1);
                percentage.rectTransform.pivot = new Vector2(1, 1); percentage.rectTransform.anchoredPosition = new Vector2(-32, -23);
                percentage.rectTransform.sizeDelta = new Vector2(118, 54);

                var bar = Rect("Loading Slider", card);
                bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(0.5f, 0);
                bar.anchoredPosition = new Vector2(0, 26); bar.sizeDelta = new Vector2(-64, 28);
                var track = Panel(bar, LevelMenuTheme.Cream, 3, 8); track.raycastTarget = false;
                var area = Rect("Fill Area", bar); Stretch(area); area.offsetMin = new Vector2(5, 5); area.offsetMax = new Vector2(-5, -5);
                var fill = Rect("Fill", area); Stretch(fill);
                var fillGraphic = Panel(fill, LevelMenuTheme.Accent, 0, 4); fillGraphic.raycastTarget = false;
                var slider = bar.gameObject.AddComponent<Slider>(); slider.fillRect = fill; slider.minValue = 0; slider.maxValue = 1;
                slider.value = 0; slider.interactable = false; slider.transition = Selectable.Transition.None;
                slider.navigation = new Navigation { mode = Navigation.Mode.None };
                var loader = root.GetComponent<SceneLoader>();
                Set(loader, "artwork", artwork); Set(loader, "safeArea", safe); Set(loader, "card", card);
                Set(loader, "progressBar", slider); Set(loader, "loadingText", title); Set(loader, "destinationText", destination);
                Set(loader, "percentage", percentage); Set(loader, "visibility", root.GetComponent<CanvasGroup>());
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new InvalidOperationException("Could not save Loading Screen.");
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            return rect;
        }

        private static MenuPanelGraphic Panel(RectTransform rect, Color color, float border, float cut)
        {
            var panel = rect.gameObject.AddComponent<MenuPanelGraphic>(); panel.color = color;
            panel.BorderColor = LevelMenuTheme.Ink; panel.BorderWidth = border; panel.CornerRadius = 0; panel.CornerCut = cut;
            return panel;
        }

        private static Text Label(string name, Transform parent, Font font, string text, int size, TextAnchor alignment)
        {
            var label = Rect(name, parent).gameObject.AddComponent<MenuLabelText>(); label.font = font;
            label.text = text; label.fontSize = size; label.alignment = alignment; label.color = LevelMenuTheme.Ink;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = size;
            label.raycastTarget = false; return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
