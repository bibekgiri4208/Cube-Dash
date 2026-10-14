using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class MainMenuRibbonFeedbackTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { Time.timeScale = 1; if (Application.isPlaying) yield return new ExitPlayMode(); }

        private static ArcadeButton[] Ribbons(MainMenuController menu)
        {
            Transform root = menu.GetComponentInChildren<Canvas>(true).transform.Find("Left menu ribbons");
            return new[] { "START", "SHOP", "OPTIONS", "QUIT" }.Select(n => root.Find(n).GetComponent<ArcadeButton>()).ToArray();
        }

        [Test]
        public void SavedPolishIsLimitedToFourMainRibbonsAndReapplicationPreservesEditsAndWiring()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MainMenuController>(true)).Single();
                var buttons = Ribbons(menu);
                Assert.That(menu.GetComponentsInChildren<ArcadeButton>(true).Count(b => b.RibbonFeedback), Is.EqualTo(4));
                foreach (var button in buttons) Assert.That(button.RibbonFeedback, Is.True);
                AssertSeparation(buttons, true);
                var original = buttons.Select(b => StandaloneMapSceneTests.Snapshot(b.GetComponent<Button>())).ToArray();
                var rect = buttons[2].GetComponent<RectTransform>(); rect.anchoredPosition += Vector2.down * 2;
                Vector2 authored = rect.anchoredPosition; int count = menu.GetComponentsInChildren<Transform>(true).Length;
                MainMenuRibbonPolish.ApplyToScene(scene); MainMenuRibbonPolish.ApplyToScene(scene);
                Assert.That(rect.anchoredPosition, Is.EqualTo(authored));
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(buttons.Select(b => StandaloneMapSceneTests.Snapshot(b.GetComponent<Button>())), Is.EqualTo(original));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            var gameplay = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try { Assert.That(gameplay.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArcadeButton>(true)).All(b => !b.RibbonFeedback), Is.True); }
            finally { EditorSceneManager.ClosePreviewScene(gameplay); }
        }

        [TestCase(false)] [TestCase(true)]
        public void WhiteOutlineFollowsTheRibbonContourAndRetainsAntialiasedEdges(bool primary)
        {
            var go = new GameObject("Ribbon outline probe", typeof(RectTransform), typeof(MenuRibbonGraphic));
            try
            {
                var graphic = go.GetComponent<MenuRibbonGraphic>(); graphic.Primary = primary;
                graphic.rectTransform.sizeDelta = new Vector2(395, 106);
                var method = typeof(MenuRibbonGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                using (var idle = new VertexHelper()) using (var focus = new VertexHelper())
                {
                    method.Invoke(graphic, new object[] { idle }); graphic.FocusAmount = 1;
                    method.Invoke(graphic, new object[] { focus });
                    Assert.That(focus.currentVertCount, Is.GreaterThan(idle.currentVertCount));
                    var stream = new List<UIVertex>(); focus.GetUIVertexStream(stream);
                    Assert.That(stream.Count(v => v.color.r == 255 && v.color.g == 255 && v.color.b == 255 && v.color.a == 255), Is.GreaterThan(30));
                    Assert.That(stream.Any(v => v.color.r == 255 && v.color.g == 255 && v.color.b == 255 && v.color.a == 0), Is.True);
                    Assert.That(graphic.raycastTarget, Is.True, "No extra raycast-blocking focus object is needed.");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator HoverMovesOneWhiteIndicatorAndAllFourStaySeparatedDuringTransitions()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.GetComponentInChildren<MainMenuEntrance>().Complete();
            var buttons = Ribbons(menu); var pointer = new PointerEventData(EventSystem.current);
            Time.timeScale = 0;
            for (int current = 0; current < buttons.Length; current++)
            {
                if (current > 0) buttons[current - 1].OnPointerExit(pointer);
                buttons[current].OnPointerEnter(pointer);
                for (int frame = 0; frame < 35; frame++) { AssertSeparation(buttons, false); yield return null; }
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(buttons[current].gameObject));
                Assert.That(buttons[current].transform.localScale.x, Is.InRange(1.025f, ArcadeButton.RibbonMaximumScale));
                Assert.That(buttons[current].GetComponent<MenuRibbonGraphic>().FocusAmount, Is.EqualTo(1));
                for (int i = 0; i < buttons.Length; i++)
                    if (i != current) Assert.That(buttons[i].GetComponent<MenuRibbonGraphic>().FocusAmount, Is.Zero);
                Capture(menu, buttons, buttons[current].name.ToLowerInvariant(), 1280, 720);
                if (current == 2) Capture(menu, buttons, "options-portrait", 720, 1280);
            }
            Assert.That(menu.CurrentPanel, Is.Null, "Hover/selection do not dispatch actions.");
        }

        [UnityTest]
        public IEnumerator PressReleaseDisableAndKeyboardFocusStayBoundedAndStopIdleWrites()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.GetComponentInChildren<MainMenuEntrance>().Complete();
            var buttons = Ribbons(menu); var feedback = buttons[1]; var button = feedback.GetComponent<Button>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            feedback.OnPointerEnter(pointer); feedback.OnPointerDown(pointer);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(feedback.transform.localScale.y, Is.InRange(0.94f, 0.96f));
            feedback.OnPointerUp(pointer); feedback.OnPointerClick(pointer);
            for (int i = 0; i < 90; i++) { AssertSeparation(buttons, false); yield return null; }
            Assert.That(menu.CurrentPanel, Is.Null);
            button.interactable = false; yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(feedback.GetComponent<MenuRibbonGraphic>().FocusAmount, Is.Zero);
            Assert.That(Vector3.Distance(feedback.transform.localScale, Vector3.one), Is.LessThan(0.001f));
            button.interactable = true; feedback.OnPointerExit(pointer);
            EventSystem.current.SetSelectedGameObject(buttons[2].gameObject);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(buttons[2].GetComponent<MenuRibbonGraphic>().FocusAmount, Is.EqualTo(1));
            foreach (var item in buttons) item.transform.hasChanged = false;
            for (int i = 0; i < 6; i++) yield return null;
            foreach (var item in buttons) Assert.That(item.transform.hasChanged, Is.False);
            buttons[2].enabled = false;
            Assert.That(buttons[2].transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(buttons[2].GetComponent<MenuRibbonGraphic>().FocusAmount, Is.Zero);
        }

        private static void AssertSeparation(ArcadeButton[] buttons, bool maximum)
        {
            float previousBottom = float.PositiveInfinity;
            foreach (var button in buttons)
            {
                var rect = button.GetComponent<RectTransform>();
                float scale = maximum ? ArcadeButton.RibbonMaximumScale : rect.localScale.y;
                Assert.That(scale, Is.InRange(0.94f, ArcadeButton.RibbonMaximumScale));
                float padding = (MenuRibbonGraphic.FocusOutset + 6) * scale;
                float top = rect.anchoredPosition.y + padding;
                float bottom = rect.anchoredPosition.y - rect.rect.height * scale - padding;
                Assert.That(previousBottom - top, Is.GreaterThan(6), "Hover/selection/outline must not touch the next ribbon: " + button.name);
                previousBottom = bottom;
            }
        }

        private static void Capture(MainMenuController menu, ArcadeButton[] buttons, string name, int width, int height)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_RIBBON_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            Camera camera = Camera.main; var canvas = menu.GetComponentInChildren<Canvas>();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance;
            bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false; Canvas.ForceUpdateCanvases();
                for (int frame = 0; frame < 4; frame++) camera.Render();
                AssertSeparation(buttons, false);
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
