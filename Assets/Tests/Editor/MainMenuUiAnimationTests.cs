using System.Collections;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class MainMenuUiAnimationTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            Time.timeScale = 1;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void SavedAnimationsAreSceneOnlyAndReapplicationPreservesManualTimingAndArtwork()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                var entrance = menu.GetComponentInChildren<MainMenuEntrance>(true);
                Assert.That(entrance, Is.Not.Null);
                foreach (GameObject panel in new[] { menu.ShopPanel, menu.OptionsPanel })
                {
                    Assert.That(panel.GetComponent<MenuPanelMotion>(), Is.Not.Null);
                    Assert.That(panel.activeSelf, Is.False);
                }
                foreach (ArcadeButton button in entrance.GetComponentsInChildren<ArcadeButton>(true)) Assert.That(button.LivelyFeedback, Is.True);
                var settings = new UnityEditor.SerializedObject(entrance); settings.FindProperty("stagger").floatValue = 0.12f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                var positions = entrance.GetComponentsInChildren<Button>().Select(button => button.GetComponent<RectTransform>().anchoredPosition).ToArray();
                MainMenuUiAnimations.ApplyToScene(scene); MainMenuUiAnimations.ApplyToScene(scene);
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(settings.FindProperty("stagger").floatValue, Is.EqualTo(0.12f));
                Assert.That(entrance.GetComponentsInChildren<Button>().Select(button => button.GetComponent<RectTransform>().anchoredPosition), Is.EqualTo(positions));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator StartupStaggersRibbonsInUnscaledTimeAndSettlesWithoutIdleTransformWrites()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); var entrance = menu.GetComponentInChildren<MainMenuEntrance>();
            entrance.Complete(); var rects = entrance.GetComponentsInChildren<Button>().Select(button => button.GetComponent<RectTransform>()).ToArray();
            var positions = rects.Select(rect => rect.anchoredPosition).ToArray();
            Time.timeScale = 0; entrance.enabled = false; entrance.enabled = true;
            Assert.That(rects.All(rect => rect.GetComponent<CanvasGroup>().alpha == 0), Is.True);
            float deadline = Time.realtimeSinceStartup + 15;
            while (rects[1].GetComponent<CanvasGroup>().alpha == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Capture(menu, "entrance-early");
            Assert.That(rects[0].GetComponent<CanvasGroup>().alpha, Is.GreaterThan(rects[1].GetComponent<CanvasGroup>().alpha));
            Assert.That(rects[1].GetComponent<CanvasGroup>().alpha, Is.GreaterThan(rects[3].GetComponent<CanvasGroup>().alpha));
            yield return Wait(0.22f); Capture(menu, "entrance-middle");
            while (entrance.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            Capture(menu, "entrance-settled");
            Assert.That(entrance.Playing, Is.False);
            for (int i = 0; i < rects.Length; i++)
                Assert.That(Vector2.Distance(rects[i].anchoredPosition, positions[i]), Is.LessThan(0.001f));
            Assert.That(rects.All(rect => rect.GetComponent<CanvasGroup>().alpha == 1), Is.True);
            yield return Wait(0.8f);
            foreach (RectTransform rect in rects) rect.hasChanged = false;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(rects.All(rect => !rect.hasChanged), Is.True, "Settled buttons must not dirty their transforms every frame.");
        }

        [UnityTest]
        public IEnumerator ColdLoadingFramesCannotSkipTheStaggerAndEarlyNavigationStillWorks()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); var entrance = menu.GetComponentInChildren<MainMenuEntrance>();
            entrance.Complete(); entrance.enabled = false; entrance.enabled = true;
            var tick = typeof(MainMenuEntrance).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 6; i++) tick.Invoke(entrance, new object[] { 5f });
            var ribbons = entrance.GetComponentsInChildren<Button>();
            Assert.That(entrance.Playing, Is.True, "A multi-second loading frame must not fast-forward the entire entrance.");
            Assert.That(ribbons[0].GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0));
            Assert.That(ribbons[3].GetComponent<CanvasGroup>().alpha, Is.Zero);
            menu.OpenShop(); Assert.That(entrance.Playing, Is.False);
            Assert.That(ribbons.All(button => button.GetComponent<CanvasGroup>().alpha == 1), Is.True);
            Assert.That(menu.CurrentPanel, Is.SameAs(menu.ShopPanel));
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("BACK"));
        }

        [UnityTest]
        public IEnumerator PanelsFadeLiftCloseAndReverseWithoutLosingOpenerFocusOrAllowingClosingInput()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var card = menu.OptionsPanel.transform.Find("Card").GetComponent<RectTransform>();
            Vector2 position = card.anchoredPosition; Vector3 scale = card.localScale;
            Time.timeScale = 0; menu.OpenOptions();
            var group = menu.OptionsPanel.GetComponent<CanvasGroup>(); var motion = menu.OptionsPanel.GetComponent<MenuPanelMotion>();
            Assert.That(group.alpha, Is.Zero); Assert.That(motion.Playing, Is.True);
            yield return Wait(0.1f); Capture(menu, "options-opening");
            Assert.That(group.alpha, Is.GreaterThan(0).And.LessThan(1));
            yield return Wait(0.3f); Capture(menu, "options-open");
            Assert.That(group.alpha, Is.EqualTo(1)); Assert.That(card.anchoredPosition, Is.EqualTo(position));
            menu.ClosePanels();
            Assert.That(motion.Closing, Is.True); Assert.That(menu.OptionsPanel.activeSelf, Is.True);
            Assert.That(group.interactable || group.blocksRaycasts, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("OPTIONS"));
            menu.ClosePanels(); Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("OPTIONS"));
            typeof(MenuPanelMotion).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motion, new object[] { 0.06f });
            Capture(menu, "options-closing");
            float alpha = group.alpha; menu.OpenOptions();
            Assert.That(motion.Closing, Is.False); Assert.That(group.alpha, Is.EqualTo(alpha), "Reopening must reverse from the current pose, not flash/reset.");
            yield return Wait(0.4f);
            Assert.That(card.anchoredPosition, Is.EqualTo(position)); Assert.That(card.localScale, Is.EqualTo(scale));
            for (int i = 0; i < 6; i++) { menu.OpenShop(); menu.OpenOptions(); menu.ClosePanels(); }
            yield return MainMenuSectionTests.WaitForPanelClosed(menu.OptionsPanel);
            yield return MainMenuSectionTests.WaitForPanelClosed(menu.ShopPanel);
            Assert.That(menu.CurrentPanel, Is.Null); Assert.That(card.anchoredPosition, Is.EqualTo(position));
        }

        [UnityTest]
        public IEnumerator HoverPressAndSubmitAnimateButDoNotDispatchDuplicateClicksOrAffectLevelButtons()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.GetComponentInChildren<MainMenuEntrance>().Complete();
            Button shop = menu.GetComponentsInChildren<Button>().Single(button => button.name == "SHOP");
            var feedback = shop.GetComponent<ArcadeButton>(); var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            feedback.OnPointerEnter(pointer); yield return Wait(0.2f);
            Assert.That(shop.transform.localScale.x, Is.GreaterThan(1.02f));
            feedback.OnPointerDown(pointer); yield return Wait(0.15f);
            Assert.That(shop.transform.localScale.y, Is.LessThan(0.96f));
            feedback.OnPointerUp(pointer); feedback.OnPointerClick(pointer); yield return Wait(0.05f);
            Assert.That(menu.CurrentPanel, Is.Null, "Feedback never invokes the button action itself.");
            yield return Wait(0.7f);
            var counter = new ClickCounter(); shop.onClick.AddListener(counter.Increment);
            EventSystem.current.SetSelectedGameObject(shop.gameObject);
            ExecuteEvents.Execute(shop.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(counter.Calls, Is.EqualTo(1)); Assert.That(menu.CurrentPanel, Is.SameAs(menu.ShopPanel));
            menu.ClosePanels(); yield return MainMenuSectionTests.WaitForPanelClosed(menu.ShopPanel);
            var scene = EditorSceneManager.OpenPreviewScene(LevelMenuTheme.ScenePath);
            try
            {
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ArcadeButton>(true)).All(button => !button.LivelyFeedback), Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private sealed class ClickCounter
        {
            public int Calls;
            public void Increment() => Calls++;
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
        private static void Capture(MainMenuController menu, string name) => typeof(MainMenuSectionTests)
            .GetMethod("CaptureIfRequested", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { menu, name });
    }
}
