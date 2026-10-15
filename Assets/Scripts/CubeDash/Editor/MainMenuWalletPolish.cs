using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Polishes only the existing top-left wallet, retaining the live balance reference and Shop UI.</summary>
    public static class MainMenuWalletPolish
    {
        private static readonly Color Ink = new Color(0.035f, 0.018f, 0.028f);

        [MenuItem("Tools/Cube Dash/Polish Main Menu Coin Display")]
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the coin display.");
        }

        public static void ApplyToScene(Scene scene)
        {
            var shop = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MenuSkinShop>(true)).Single();
            var wallet = (RectTransform)shop.MenuCoins.transform.parent;
            wallet.anchorMin = wallet.anchorMax = new Vector2(0, 1); wallet.pivot = new Vector2(0, 1);
            wallet.anchoredPosition = new Vector2(68, -48); wallet.sizeDelta = new Vector2(320, 90);
            var face = wallet.GetComponent<MenuPanelGraphic>();
            Style(face, new Color(1, 0.87f, 0.87f), Ink, 3, 15);

            var shadow = Child("Wallet Drop Shadow", wallet.parent, wallet.anchoredPosition + new Vector2(5, -6), wallet.sizeDelta);
            shadow.anchorMin = shadow.anchorMax = wallet.anchorMin; shadow.pivot = wallet.pivot;
            Style(Get<MenuPanelGraphic>(shadow), new Color(0.025f, 0.01f, 0.02f, 0.78f), Color.clear, 0, 15);
            int shadowIndex = wallet.GetSiblingIndex() - (shadow.GetSiblingIndex() < wallet.GetSiblingIndex() ? 1 : 0);
            shadow.SetSiblingIndex(shadowIndex);

            var inset = Child("Inner Highlight", wallet, Vector2.zero, new Vector2(310, 80)); inset.SetAsFirstSibling();
            Style(Get<MenuPanelGraphic>(inset), new Color(1, 0.91f, 0.90f), new Color(1, 0.98f, 0.89f), 1, 11);
            var socket = Child("Coin Socket", wallet, new Vector2(-112, 0), new Vector2(82, 72)); socket.SetSiblingIndex(1);
            Style(Get<MenuPanelGraphic>(socket), new Color(0.19f, 0.065f, 0.10f), Ink, 2, 11);

            Transform oldCoin = wallet.Find("Coin"); oldCoin.gameObject.SetActive(false); // Retain the original editable mark.
            var icon = Child("Gold Star Coins", wallet, new Vector2(-112, 1), new Vector2(66, 66));
            Get<MenuCoinGraphic>(icon).raycastTarget = false;
            var caption = wallet.Find("Caption").GetComponent<Text>();
            caption.text = "YOUR COINS"; caption.color = new Color(0.43f, 0.23f, 0.27f);
            Label(caption, new Vector2(42, 23), new Vector2(180, 22), 15, 13);
            shop.MenuCoins.color = Ink;
            Label(shop.MenuCoins, new Vector2(42, -7), new Vector2(180, 49), 38, 18);
            Transform accent = wallet.Find("Gold Accent");
            if (accent != null) UnityEngine.Object.DestroyImmediate(accent.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void Label(Text label, Vector2 position, Vector2 size, int fontSize, int minimum)
        {
            var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            label.alignment = TextAnchor.MiddleLeft; label.fontSize = fontSize;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = minimum; label.resizeTextMaxSize = fontSize;
            label.raycastTarget = false;
        }
        private static RectTransform Child(string name, Transform parent, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            var rect = existing != null ? (RectTransform)existing : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (existing == null) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static T Get<T>(RectTransform rect) where T : Component => rect.GetComponent<T>() ?? rect.gameObject.AddComponent<T>();
        private static void Style(MenuPanelGraphic graphic, Color fill, Color border, float width, float cut)
        {
            graphic.color = fill; graphic.BorderColor = border; graphic.BorderWidth = width;
            graphic.CornerRadius = 0; graphic.CornerCut = cut; graphic.raycastTarget = false;
        }
    }
}
