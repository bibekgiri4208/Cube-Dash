using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Adds a saved Options gift dialog without rebuilding the menu or changing its audio.</summary>
    public static class MainMenuRedeemAuthoring
    {
        public const string PanelName = "Options - Redeem Gift";

        [MenuItem("Tools/Cube Dash/Add Options Redeem Code")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            var scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save redeem dialog.");
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            Transform canvas = menu.GetComponentInChildren<Canvas>(true).transform;
            var settings = new SerializedObject(menu);
            if (menu.RedeemPanel != null)
            {
                if (menu.RedeemPanel.transform.parent != canvas || settings.FindProperty("redeemButton").objectReferenceValue == null)
                    throw new InvalidOperationException("Inspect partial/custom redeem dialog before authoring.");
                if (UpgradeGift(menu.RedeemPanel)) EditorSceneManager.MarkSceneDirty(scene);
                return; // Preserve manually authored layout and callbacks on reapplication.
            }
            if (canvas.Find(PanelName) != null) throw new InvalidOperationException("Inspect the unassigned redeem dialog first.");
            Font heading = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            Font body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (heading == null) throw new InvalidOperationException("Main Menu Anton font is required.");

            RectTransform overlay = Rect(PanelName, canvas, Vector2.zero, Vector2.zero);
            overlay.gameObject.SetActive(false);
            Stretch(overlay, Vector2.zero, Vector2.one);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.02f, 0.045f, 0.64f);
            overlay.gameObject.AddComponent<CanvasGroup>();
            var dialog = overlay.gameObject.AddComponent<MenuRedeemCode>();
            RectTransform shadow = Rect("Soft card shadow", overlay, new Vector2(8, -9), new Vector2(820, 650));
            Face(shadow, LevelMenuTheme.Ink, 0).raycastTarget = false;
            RectTransform card = Rect("Card", overlay, Vector2.zero, new Vector2(820, 650));
            Face(card, LevelMenuTheme.Paper, 4);
            RectTransform header = Rect("Ribbon header", card, Vector2.zero, Vector2.zero);
            Stretch(header, new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.96f));
            header.gameObject.AddComponent<MenuRibbonGraphic>().raycastTarget = false;
            Label("Heading", card, "REDEEM A GIFT", heading, 48, LevelMenuTheme.Ink, new Vector2(0.08f, 0.85f), new Vector2(0.92f, 0.95f));

            RectTransform form = Rect("Code form", card, Vector2.zero, Vector2.zero);
            Stretch(form, new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.79f));
            Label("Code heading", form, "ENTER YOUR CODE", heading, 32, LevelMenuTheme.Ink, new Vector2(0, 0.72f), new Vector2(1, 0.91f));
            RectTransform inputRect = Rect("Redeem code input", form, Vector2.zero, Vector2.zero);
            Stretch(inputRect, new Vector2(0.04f, 0.43f), new Vector2(0.96f, 0.68f));
            var inputFace = Face(inputRect, LevelMenuTheme.Cream, 3);
            var input = inputRect.gameObject.AddComponent<InputField>(); input.targetGraphic = inputFace;
            Text inputText = Label("Text", inputRect, string.Empty, body, 32, LevelMenuTheme.Ink,
                new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), false);
            inputText.supportRichText = false; inputText.alignment = TextAnchor.MiddleLeft; inputText.resizeTextForBestFit = false;
            Text placeholder = Label("Placeholder", inputRect, "Redeem code...", body, 30, new Color(0.45f, 0.30f, 0.35f),
                new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), false);
            placeholder.alignment = TextAnchor.MiddleLeft;
            input.textComponent = inputText; input.placeholder = placeholder; input.characterLimit = 64;
            input.lineType = InputField.LineType.SingleLine;
            UnityEventTools.AddPersistentListener(input.onSubmit, dialog.SubmitCode);
            Text status = Label("Status", form, string.Empty, body, 26, new Color(0.72f, 0.04f, 0.12f),
                new Vector2(0, 0.24f), new Vector2(1, 0.4f));
            Button redeem = Action(form, "REDEEM", true, heading, dialog.Redeem,
                new Vector2(0.24f, 0.01f), new Vector2(0.76f, 0.22f));
            MenuGiftReveal gift = Gift(card, heading);
            Button again = Action(card, "REDEEM AGAIN", true, heading, dialog.RedeemAgain,
                new Vector2(0.08f, 0.07f), new Vector2(0.56f, 0.18f));
            Button back = Action(card, "BACK", false, heading, menu.ClosePanels,
                new Vector2(0.63f, 0.07f), new Vector2(0.92f, 0.18f));
            again.gameObject.SetActive(false); gift.gameObject.SetActive(false);
            input.navigation = Nav(back, redeem); redeem.navigation = Nav(input, back);
            back.navigation = Nav(redeem, input); again.navigation = Nav(back, back);
            again.navigation = Horizontal(again.navigation, back);

            Set(dialog, "menu", menu); Set(dialog, "codeInput", input); Set(dialog, "form", form.gameObject);
            Set(dialog, "status", status); Set(dialog, "redeem", redeem); Set(dialog, "again", again);
            Set(dialog, "back", back); Set(dialog, "gift", gift);
            var layout = overlay.gameObject.AddComponent<MenuDialogLayout>();
            Set(layout, "card", card); Set(layout, "shadow", shadow);
            var layoutData = new SerializedObject(layout); layoutData.FindProperty("maximumSize").vector2Value = new Vector2(820, 650);
            layoutData.ApplyModifiedPropertiesWithoutUndo();
            var motion = overlay.gameObject.AddComponent<MenuPanelMotion>(); Set(motion, "card", card); Set(motion, "shadow", shadow);

            Transform optionsCard = menu.OptionsPanel.transform.Find("Card");
            var menuData = new SerializedObject(menu);
            var optionsBack = menuData.FindProperty("optionsClose").objectReferenceValue as Button;
            var fullscreen = menuData.FindProperty("fullscreen").objectReferenceValue as Toggle;
            Button opener = Action(optionsCard, "REDEEM CODE", true, heading, menu.OpenRedeem, Vector2.zero, Vector2.one);
            var openerRect = opener.GetComponent<RectTransform>();
            openerRect.anchorMin = openerRect.anchorMax = Vector2.one * 0.5f;
            openerRect.anchoredPosition = new Vector2(-255, -270); openerRect.sizeDelta = new Vector2(320, 58);
            opener.navigation = Horizontal(Nav(fullscreen, optionsBack), optionsBack);
            var toggleNav = fullscreen.navigation; toggleNav.selectOnDown = opener; fullscreen.navigation = toggleNav;
            var backNav = optionsBack.navigation; backNav.selectOnUp = opener; backNav.selectOnLeft = opener; optionsBack.navigation = backNav;
            Set(menu, "redeemPanel", dialog); Set(menu, "redeemButton", opener);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Added Options redeem dialog, repeatable 5,000-coin gift and silent gift-box animation. Menu audio and artwork preserved.");
        }

        private static MenuGiftReveal Gift(RectTransform card, Font heading)
        {
            RectTransform root = Rect("Gift reveal", card, Vector2.zero, Vector2.zero);
            Stretch(root, new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.81f));
            var gift = root.gameObject.AddComponent<MenuGiftReveal>();
            Set(gift, "artwork", Artwork(root));
            RectTransform reward = Rect("Reward message", root, Vector2.zero, Vector2.zero);
            Stretch(reward, new Vector2(0, 0.01f), new Vector2(1, 0.26f));
            var group = reward.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0;
            Text amount = Label("Coin amount", reward, "+5,000 COINS", heading, 50, LevelMenuTheme.Cream,
                new Vector2(0, 0.44f), Vector2.one);
            Label("Gift attribution", reward, "Gift from Bibek Giri Productions", heading, 25, LevelMenuTheme.Ink,
                Vector2.zero, new Vector2(1, 0.44f));
            RewardBadge(reward);
            Set(gift, "reward", group); Set(gift, "amount", amount);
            return gift;
        }

        private static bool UpgradeGift(MenuRedeemCode dialog)
        {
            MenuGiftReveal gift = dialog.Gift;
            if (gift.Artwork != null)
            {
                if (gift.Artwork.GetComponent<CanvasRenderer>() != null) return false;
                Undo.AddComponent<CanvasRenderer>(gift.Artwork.gameObject); return true;
            }
            // Keep the first version recoverable, without displaying its blocky rectangles over the new illustration.
            Transform old = gift.transform.Find("Gift box");
            if (old != null) old.gameObject.SetActive(false);
            Set(gift, "artwork", Artwork((RectTransform)gift.transform));
            var amount = gift.Amount; amount.fontSize = amount.resizeTextMaxSize = 50; amount.color = LevelMenuTheme.Cream;
            var reward = (RectTransform)amount.transform.parent; RewardBadge(reward);
            reward.Find("Gift attribution").GetComponent<Text>().fontSize = 25;
            reward.Find("Gift attribution").GetComponent<Text>().resizeTextMaxSize = 25;
            return true;
        }

        private static MenuGiftGraphic Artwork(RectTransform root)
        {
            var rect = Rect("Illustrated gift celebration", root, Vector2.zero, Vector2.zero);
            Stretch(rect, new Vector2(0, 0.28f), Vector2.one); rect.SetAsFirstSibling();
            var graphic = rect.gameObject.AddComponent<MenuGiftGraphic>(); graphic.raycastTarget = false;
            return graphic;
        }

        private static void RewardBadge(RectTransform reward)
        {
            var rect = Rect("Reward badge", reward, Vector2.zero, Vector2.zero);
            Stretch(rect, new Vector2(0.19f, 0.43f), new Vector2(0.81f, 1.02f)); rect.SetAsFirstSibling();
            var face = Face(rect, LevelMenuTheme.Ink, 2); face.BorderColor = new Color(1, 0.75f, 0.23f); face.raycastTarget = false;
        }

        private static Button Action(Transform parent, string name, bool primary, Font font, UnityAction callback, Vector2 min, Vector2 max)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero); Stretch(rect, min, max);
            var ribbon = rect.gameObject.AddComponent<MenuRibbonGraphic>(); ribbon.Primary = primary;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = ribbon; button.transition = Selectable.Transition.None;
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = LevelMenuTheme.Cream;
            outline.effectDistance = new Vector2(3, -3); outline.enabled = false;
            var feedback = rect.gameObject.AddComponent<ArcadeButton>(); var data = new SerializedObject(feedback);
            data.FindProperty("livelyFeedback").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            Label("Label", rect, name, font, 34, primary ? LevelMenuTheme.Cream : LevelMenuTheme.Ink,
                new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f));
            UnityEventTools.AddPersistentListener(button.onClick, callback); return button;
        }

        private static Navigation Nav(Selectable up, Selectable down) => new Navigation
        { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
        private static Navigation Horizontal(Navigation nav, Selectable other)
        { nav.selectOnLeft = nav.selectOnRight = other; return nav; }

        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "Add redeem dialog"); return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private static MenuPanelGraphic Face(RectTransform rect, Color color, float border)
        {
            var graphic = rect.gameObject.AddComponent<MenuPanelGraphic>(); graphic.color = color;
            graphic.CornerRadius = 0; graphic.CornerCut = 10; graphic.BorderWidth = border; graphic.BorderColor = LevelMenuTheme.Ink;
            return graphic;
        }

        private static Text Label(string name, Transform parent, string value, Font font, int size, Color color,
            Vector2 min, Vector2 max, bool sharp = true)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero); Stretch(rect, min, max);
            Text text = sharp ? rect.gameObject.AddComponent<MenuLabelText>() : rect.gameObject.AddComponent<Text>();
            text.text = value; text.font = font; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 8; text.resizeTextMaxSize = size;
            return text;
        }
    }
}
