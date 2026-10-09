using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>
    /// Repositions and simplifies the authored HUD. Every result is a saved scene object;
    /// nothing is created at runtime. Safe to re-run because each edit checks first.
    /// </summary>
    public static class CubeDashHudLayout
    {
        private static readonly Color Navy = new Color(0.035f, 0.075f, 0.13f, 0.94f);
        private static readonly Color PanelFill = new Color(0.10f, 0.16f, 0.25f, 0.96f);
        private static readonly Color White = new Color(0.94f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.64f, 0.77f, 0.83f);
        private static readonly Color Coral = new Color(1f, 0.31f, 0.23f);
        private static Sprite rounded;

        [MenuItem("Tools/Cube Dash/Refine HUD Layout")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            RunnerHud hud = Object.FindAnyObjectByType<RunnerHud>();
            if (hud == null) throw new InvalidOperationException("The authored Cube Dash HUD is required.");
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/2D Images/ArcadePanel.png");
            if (rounded == null) throw new InvalidOperationException("The rounded panel sprite is required.");
            Apply(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("HUD refined: score top-right, icon pause top-left, bottom instruction removed, " +
                "simplified start menu, quit button added to game over.");
        }

        public static void Apply(RunnerHud hud)
        {
            Transform safe = hud.transform.Find("Safe Area");
            MoveScoreTopRight(safe);
            MovePauseTopLeft(safe);
            RemoveBottomInstruction(safe);
            SimplifyStartMenu(safe);
            StylePauseMenu(safe, hud);
            AddQuitToGameOver(safe, hud);
        }

        private static void MoveScoreTopRight(Transform safe)
        {
            RectTransform scorePanel = GetRect("Score Panel", safe);
            Place(scorePanel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(235, 124));
            Panel(Ensure<Image>(scorePanel), Navy);
            scorePanel.SetAsFirstSibling();

            Text brand = safe.Find("Brand").GetComponent<Text>();
            brand.text = "CUBE / DASH";
            brand.fontSize = 15;
            brand.color = Muted;
            brand.alignment = TextAnchor.MiddleRight;
            Place(brand.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-38, -42), new Vector2(200, 25));

            Text score = safe.Find("Distance").GetComponent<Text>();
            score.text = "0 POINTS";
            score.fontSize = 32;
            score.fontStyle = FontStyle.Bold;
            score.color = White;
            score.alignment = TextAnchor.MiddleRight;
            Place(score.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-30, -80), new Vector2(205, 48));

            Text best = safe.Find("Best").GetComponent<Text>();
            best.text = "BEST  0";
            best.fontSize = 14;
            best.color = Muted;
            best.alignment = TextAnchor.MiddleRight;
            Place(best.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-32, -120), new Vector2(190, 24));

            Transform feedback = safe.Find("Pickup Feedback");
            if (feedback != null)
            {
                Place((RectTransform)feedback, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-194, -80), new Vector2(64, 36));
                feedback.GetComponent<Text>().alignment = TextAnchor.MiddleRight;
            }
        }

        private static void MovePauseTopLeft(Transform safe)
        {
            RectTransform pause = GetRect("Pause", safe);
            Place(pause, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(58, 58));
            Panel(Ensure<Image>(pause), Navy);

            Transform label = pause.Find("Label");
            if (label != null) label.gameObject.SetActive(false);

            Transform existing = pause.Find("Pause Icon");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            RectTransform icon = Rect("Pause Icon", pause);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            icon.sizeDelta = new Vector2(30, 26);
            Bar("Bar Left", icon, new Vector2(-6.5f, 0), new Vector2(8, 26));
            Bar("Bar Right", icon, new Vector2(6.5f, 0), new Vector2(8, 26));
        }

        private static void RemoveBottomInstruction(Transform safe)
        {
            Transform hint = safe.Find("Desktop Controls Hint");
            if (hint != null) hint.gameObject.SetActive(false);
            Transform lane = safe.Find("Lane Controls");
            if (lane != null) lane.gameObject.SetActive(false);
        }

        private static void SimplifyStartMenu(Transform safe)
        {
            Transform card = safe.Find("Menu Overlay/Menu Card");
            Place((RectTransform)card, new Vector2(0.5f, 0.60f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540, 360));
            Panel(card.GetComponent<Image>(), Navy);

            Transform accent = card.Find("Accent");
            accent.gameObject.SetActive(true);
            Place((RectTransform)accent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(56, 5));
            accent.GetComponent<Image>().color = Coral;

            card.Find("Eyebrow").gameObject.SetActive(false);

            Text heading = card.Find("Heading").GetComponent<Text>();
            heading.text = "CUBE DASH";
            heading.fontSize = 46;
            heading.fontStyle = FontStyle.Bold;
            heading.color = White;
            heading.alignment = TextAnchor.MiddleCenter;
            Place(heading.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -66), new Vector2(500, 86));

            Text description = card.Find("Description").GetComponent<Text>();
            description.text = "Collect your color. Avoid the rest.";
            description.fontSize = 17;
            description.color = Muted;
            description.alignment = TextAnchor.MiddleCenter;
            Place(description.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -128), new Vector2(470, 40));

            RectTransform primary = (RectTransform)card.Find("Primary Action");
            Place(primary, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 162), new Vector2(300, 46));
            Panel(primary.GetComponent<Image>(), Coral);
            Text primaryLabel = primary.GetComponentInChildren<Text>();
            primaryLabel.text = "START RUN";
            primaryLabel.color = White;
            primaryLabel.fontSize = 22;
            primaryLabel.fontStyle = FontStyle.Bold;

            Transform restart = card.Find("Restart");
            Place((RectTransform)restart, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 102), new Vector2(300, 46));
            Panel(restart.GetComponent<Image>(), PanelFill);
            restart.Find("Label").GetComponent<Text>().color = Muted;
            restart.gameObject.SetActive(false);

            Transform keyboardHint = card.Find("Keyboard Hint");
            Place((RectTransform)keyboardHint, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 102), new Vector2(450, 22));
            keyboardHint.GetComponent<Text>().color = Muted;
        }

        private static void StylePauseMenu(Transform safe, RunnerHud hud)
        {
            Transform card = safe.Find("Menu Overlay/Menu Card");
            Transform existing = card.Find("Pause Motif");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            RectTransform motif = Rect("Pause Motif", card);
            motif.anchorMin = motif.anchorMax = new Vector2(0.5f, 1);
            motif.pivot = new Vector2(0.5f, 1);
            motif.anchoredPosition = new Vector2(0, -12);
            motif.sizeDelta = new Vector2(110, 30);

            Motif("Motif Blue", motif, new Vector2(-24, 2), new Vector2(16, 16), new Color(0.25f, 0.5f, 0.95f));
            Motif("Motif Green", motif, new Vector2(24, 2), new Vector2(16, 16), new Color(0.3f, 0.78f, 0.45f));
            Motif("Motif Coral", motif, new Vector2(0, -2), new Vector2(22, 22), Coral);
            motif.gameObject.SetActive(false);

            RectTransform quit = card.Find("Quit") as RectTransform;
            if (quit == null)
            {
                quit = Rect("Quit", card);
                Image background = quit.gameObject.AddComponent<Image>();
                Button created = quit.gameObject.AddComponent<Button>();
                created.targetGraphic = background;
                Text label = Label("Label", quit, "QUIT", 18, White);
                label.fontStyle = FontStyle.Bold;
            }
            if (quit.GetComponent<ArcadeButton>() == null) quit.gameObject.AddComponent<ArcadeButton>();
            Button quitButton = quit.GetComponent<Button>();
            if (quitButton == null)
            {
                quitButton = quit.gameObject.AddComponent<Button>();
                quitButton.targetGraphic = quit.GetComponent<Image>();
            }
            if (quitButton.onClick.GetPersistentEventCount() == 0)
                UnityEventTools.AddPersistentListener(quitButton.onClick, hud.QuitGame);
            MatchButtonFeedback(quitButton);
            Place(quit, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 42), new Vector2(300, 46));
            Panel(quit.GetComponent<Image>(), PanelFill);
            quit.gameObject.SetActive(false);

            // Resume, Restart and Quit share one size and a single D-pad navigation order.
            Button primary = card.Find("Primary Action").GetComponent<Button>();
            Button restart = card.Find("Restart").GetComponent<Button>();
            Link(primary, null, restart);
            Link(restart, primary, quitButton);
            Link(quitButton, restart, null);
        }

        private static void Link(Button button, Button up, Button down)
        {
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            button.navigation = navigation;
        }

        /// <summary>Matches the hand-authored buttons so hover/press feedback is consistent.</summary>
        private static void MatchButtonFeedback(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = White;
            colors.highlightedColor = new Color(1f, 0.93f, 0.88f, 1f);
            colors.pressedColor = new Color(0.72f, 0.78f, 0.85f, 1f);
            colors.selectedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
            colors.disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (button.GetComponent<Outline>() == null)
            {
                Outline outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.65f, 0.35f, 0.95f);
                outline.effectDistance = new Vector2(2, -2);
                outline.enabled = false;
            }
        }

        private static void Motif(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void AddQuitToGameOver(Transform safe, RunnerHud hud)
        {
            Transform composition = safe.Find("Game Over Overlay/Abstract Composition");
            RectTransform compositionRect = (RectTransform)composition;
            Place(compositionRect, new Vector2(0.5f, 0.60f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 430));
            Panel(Ensure<Image>(composition), Navy);

            Place((RectTransform)composition.Find("Retry"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -103), new Vector2(230, 46));
            Place((RectTransform)composition.Find("Tap Hint"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(300, 26));

            RectTransform quit = composition.Find("Quit") as RectTransform;
            if (quit == null)
            {
                quit = Rect("Quit", composition);
                Image background = quit.gameObject.AddComponent<Image>();
                Button button = quit.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                Text label = Label("Label", quit, "QUIT", 18, White);
                label.fontStyle = FontStyle.Bold;
            }
            if (quit.GetComponent<ArcadeButton>() == null) quit.gameObject.AddComponent<ArcadeButton>();
            Button quitButton = quit.GetComponent<Button>();
            if (quitButton == null)
            {
                quitButton = quit.gameObject.AddComponent<Button>();
                quitButton.targetGraphic = quit.GetComponent<Image>();
            }
            if (quitButton.onClick.GetPersistentEventCount() == 0)
                UnityEventTools.AddPersistentListener(quitButton.onClick, hud.QuitGame);
            MatchButtonFeedback(quitButton);
            Place(quit, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -158), new Vector2(230, 44));
            Panel(quit.GetComponent<Image>(), PanelFill);

            // Gamepads reach Quit with D-pad down from Retry; mouse and keyboard still work.
            Button retry = composition.Find("Retry") != null ? composition.Find("Retry").GetComponent<Button>() : null;
            if (retry != null)
            {
                Navigation down = retry.navigation;
                down.mode = Navigation.Mode.Explicit;
                down.selectOnDown = quitButton;
                retry.navigation = down;
                Navigation up = quitButton.navigation;
                up.mode = Navigation.Mode.Explicit;
                up.selectOnUp = retry;
                quitButton.navigation = up;
            }
        }

        private static void Bar(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = White;
            image.raycastTarget = false;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text Label(string name, Transform parent, string value, int size, Color color)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static void Panel(Image image, Color color)
        {
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
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
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
