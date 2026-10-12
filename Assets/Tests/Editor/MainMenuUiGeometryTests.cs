using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class MainMenuUiGeometryTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [TestCase(455, 122)]
        [TestCase(200, 80)]
        [TestCase(900, 244)]
        [TestCase(310, 150)]
        public void EveryStartStripeIncludingItsSoftEdgeStaysInsideTheButtonFace(float width, float height)
        {
            var root = new GameObject("Start ribbon sample", typeof(RectTransform));
            Mesh mesh = null;
            try
            {
                var ribbon = root.AddComponent<MenuRibbonGraphic>(); ribbon.Primary = true;
                ribbon.rectTransform.sizeDelta = new Vector2(width, height);
                mesh = Populate(ribbon);
                Rect rect = ribbon.rectTransform.rect;
                Vector2[] contour = { new Vector2(0, 0), new Vector2(0.025f, 1), new Vector2(0.94f, 1),
                    new Vector2(1, 0.74f), new Vector2(0.996f, 0.45f), new Vector2(0.967f, 0.22f), new Vector2(0.91f, 0) };
                for (int index = 0; index < contour.Length; index++)
                    contour[index] = new Vector2(rect.xMin + 5 + contour[index].x * (width - 12), rect.yMin + 3 + contour[index].y * (height - 10));
                Color32[] colors = mesh.colors32; Vector3[] vertices = mesh.vertices;
                int stripeVertices = 0;
                for (int index = 0; index < vertices.Length; index++)
                {
                    if (colors[index].r != 255 || colors[index].g != 84 || colors[index].b != 102) continue;
                    stripeVertices++;
                    for (int edge = 0; edge < contour.Length; edge++)
                    {
                        Vector2 start = contour[edge], end = contour[(edge + 1) % contour.Length];
                        float distance = -Cross(end - start, (Vector2)vertices[index] - start) / (end - start).magnitude;
                        Assert.That(distance, Is.GreaterThanOrEqualTo(-0.02f), "A stripe or its antialias fringe must never paint across the border.");
                    }
                }
                Assert.That(stripeVertices, Is.GreaterThan(30));
                AssertValidMesh(mesh);
            }
            finally { if (mesh != null) Object.DestroyImmediate(mesh); Object.DestroyImmediate(root); }
        }

        [TestCase(MenuGlyphGraphic.Glyph.Play)]
        [TestCase(MenuGlyphGraphic.Glyph.Cart)]
        [TestCase(MenuGlyphGraphic.Glyph.Gear)]
        [TestCase(MenuGlyphGraphic.Glyph.Exit)]
        [TestCase(MenuGlyphGraphic.Glyph.Collection)]
        [TestCase(MenuGlyphGraphic.Glyph.Check)]
        public void IconsHaveSmoothAlphaContoursAndValidVectorGeometry(MenuGlyphGraphic.Glyph symbol)
        {
            var root = new GameObject("Icon sample", typeof(RectTransform));
            Mesh mesh = null;
            try
            {
                var graphic = root.AddComponent<MenuGlyphGraphic>(); graphic.Symbol = symbol;
                graphic.rectTransform.sizeDelta = new Vector2(68, 68); graphic.color = Color.black;
                mesh = Populate(graphic); AssertValidMesh(mesh);
                int transparent = 0, opaque = 0;
                foreach (Color32 color in mesh.colors32)
                {
                    if (color.a == 0) transparent++;
                    if (color.a == 255) opaque++;
                }
                Assert.That(transparent, Is.GreaterThan(10), "Overlay icons need their own smooth silhouette fringe.");
                Assert.That(opaque, Is.GreaterThan(10));
                Assert.That(mesh.vertexCount, Is.LessThan(8000), "Keep procedural UI geometry lightweight.");
                if (symbol == MenuGlyphGraphic.Glyph.Gear) Assert.That(mesh.vertexCount, Is.GreaterThan(500), "Refine curved contours rather than stretching the old coarse gear sectors.");
            }
            finally { if (mesh != null) Object.DestroyImmediate(mesh); Object.DestroyImmediate(root); }
        }

        [Test]
        public void GearKeepsItsOpenCenterWithoutAntialiasingEveryInternalTriangle()
        {
            var root = new GameObject("Gear sample", typeof(RectTransform));
            Mesh mesh = null;
            try
            {
                var gear = root.AddComponent<MenuGlyphGraphic>(); gear.Symbol = MenuGlyphGraphic.Glyph.Gear;
                gear.rectTransform.sizeDelta = new Vector2(68, 68);
                mesh = Populate(gear);
                Vector3[] points = mesh.vertices; int[] triangles = mesh.triangles;
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    Vector2 a = points[triangles[index]], b = points[triangles[index + 1]], c = points[triangles[index + 2]];
                    if (Mathf.Abs(Cross(b - a, c - a)) < 0.00001f) continue;
                    float first = Cross(b - a, -a), second = Cross(c - b, -b), third = Cross(a - c, -c);
                    bool coversCenter = (first >= 0 && second >= 0 && third >= 0) || (first <= 0 && second <= 0 && third <= 0);
                    Assert.That(coversCenter, Is.False, "The cog's center must remain a clean hole, not a filled disc.");
                }
            }
            finally { if (mesh != null) Object.DestroyImmediate(mesh); Object.DestroyImmediate(root); }
        }

        [Test]
        public void AntialiasFringeTracksCanvasScaleInsteadOfBecomingAThickBlurAtHigherResolution()
        {
            var canvasObject = new GameObject("UI scale sample", typeof(Canvas));
            var root = new GameObject("Play icon", typeof(RectTransform));
            Mesh first = null, second = null;
            try
            {
                Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                root.transform.SetParent(canvasObject.transform, false);
                var icon = root.AddComponent<MenuGlyphGraphic>(); icon.Symbol = MenuGlyphGraphic.Glyph.Play;
                icon.rectTransform.sizeDelta = new Vector2(68, 68);
                canvas.scaleFactor = 1; first = Populate(icon);
                canvas.scaleFactor = 2; second = Populate(icon);
                Assert.That(FirstFringeWidth(second) / FirstFringeWidth(first), Is.EqualTo(0.5f).Within(0.01f));
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first); if (second != null) Object.DestroyImmediate(second);
                Object.DestroyImmediate(root); Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void HigherResolutionLabelsKeepTheOriginalFontSizeLayoutAndTextMetrics()
        {
            var plainObject = new GameObject("Original label", typeof(RectTransform));
            var sharpObject = new GameObject("Higher-resolution label", typeof(RectTransform));
            Mesh plainMesh = null, sharpMesh = null;
            try
            {
                Text plain = plainObject.AddComponent<Text>(); Text sharp = sharpObject.AddComponent<MenuLabelText>();
                Font font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
                Assert.That(font, Is.Not.Null);
                foreach (Text label in new[] { plain, sharp })
                {
                    label.font = font; label.text = "START"; label.fontSize = 65;
                    label.alignment = TextAnchor.MiddleLeft; label.rectTransform.sizeDelta = new Vector2(360, 110);
                }
                Assert.That(sharp.preferredWidth, Is.EqualTo(plain.preferredWidth).Within(0.01f));
                Assert.That(sharp.preferredHeight, Is.EqualTo(plain.preferredHeight).Within(0.01f));
                plainMesh = Populate(plain); sharpMesh = Populate(sharp);
                Assert.That(sharpMesh.vertexCount, Is.EqualTo(plainMesh.vertexCount));
                Assert.That(Vector3.Distance(sharpMesh.bounds.center, plainMesh.bounds.center), Is.LessThan(2.5f));
                Assert.That(Vector3.Distance(sharpMesh.bounds.size, plainMesh.bounds.size), Is.LessThan(4f));
                Assert.That(sharp.fontSize, Is.EqualTo(65), "Sample the atlas at higher resolution without changing authored text settings.");
            }
            finally
            {
                if (plainMesh != null) Object.DestroyImmediate(plainMesh); if (sharpMesh != null) Object.DestroyImmediate(sharpMesh);
                Object.DestroyImmediate(plainObject); Object.DestroyImmediate(sharpObject);
            }
        }

        [Test]
        public void AllFourSavedRibbonLabelsUseSharperSamplingWithoutChangingTheirButtonWiring()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                MenuRibbonGraphic[] ribbons = menu.GetComponentsInChildren<MenuRibbonGraphic>(true);
                Assert.That(ribbons.Length, Is.EqualTo(4));
                foreach (MenuRibbonGraphic ribbon in ribbons)
                {
                    MenuLabelText label = ribbon.GetComponentInChildren<MenuLabelText>(true);
                    Assert.That(label, Is.Not.Null); Assert.That(label.text, Is.EqualTo(ribbon.name));
                    Assert.That(label.font, Is.Not.Null); Assert.That(label.raycastTarget, Is.False);
                    Assert.That(ribbon.GetComponent<Button>().onClick.GetPersistentTarget(0), Is.SameAs(menu));
                }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator VectorIconsRenderIntermediateEdgePixelsWithoutCameraAntialiasingOrPostProcessing()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("GPU rendering required.");
            yield return new EnterPlayMode();
            var cameraObject = new GameObject("Isolated UI edge camera", typeof(Camera));
            var canvasObject = new GameObject("Isolated UI edge canvas", typeof(Canvas));
            var iconObject = new GameObject("Smooth play icon", typeof(RectTransform));
            var target = new RenderTexture(512, 512, 24) { antiAliasing = 1 };
            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
            RenderTexture oldActive = RenderTexture.active;
            bool oldAsync = UnityEditor.ShaderUtil.allowAsyncCompilation;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.orthographic = true; camera.orthographicSize = 5; camera.allowMSAA = false;
                camera.cullingMask = 1 << 31; camera.targetTexture = target;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
                Canvas canvas = canvasObject.GetComponent<Canvas>(); canvasObject.layer = 31;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                iconObject.layer = 31; iconObject.transform.SetParent(canvasObject.transform, false);
                var icon = iconObject.AddComponent<MenuGlyphGraphic>(); icon.Symbol = MenuGlyphGraphic.Glyph.Play; icon.color = Color.black;
                icon.rectTransform.sizeDelta = new Vector2(120, 120); icon.rectTransform.anchoredPosition = new Vector2(0.35f, 0.35f);
                UnityEditor.ShaderUtil.allowAsyncCompilation = false;
                Canvas.ForceUpdateCanvases();
                for (int frame = 0; frame < 3; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                int grayPixels = 0, inkPixels = 0;
                foreach (Color pixel in image.GetPixels())
                {
                    if (pixel.r > 0.04f && pixel.r < 0.96f) grayPixels++;
                    if (pixel.r < 0.04f) inkPixels++;
                }
                Assert.That(inkPixels, Is.GreaterThan(1000));
                Assert.That(grayPixels, Is.GreaterThan(80), "The alpha contour must genuinely smooth rendered edges with camera AA disabled.");
                Debug.Log($"UI edge render: {grayPixels} antialiased edge pixels, {inkPixels} solid icon pixels without camera AA.");
            }
            finally
            {
                cameraObject.GetComponent<Camera>().targetTexture = null; RenderTexture.active = oldActive;
                UnityEditor.ShaderUtil.allowAsyncCompilation = oldAsync;
                Object.DestroyImmediate(iconObject); Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        private static float FirstFringeWidth(Mesh mesh)
        {
            Color32[] colors = mesh.colors32; Vector3[] vertices = mesh.vertices;
            for (int index = 1; index < colors.Length; index++)
                if (colors[index].a == 0) return Vector3.Distance(vertices[index], vertices[index - 1]);
            Assert.Fail("No alpha fringe was generated."); return 0;
        }

        private static Mesh Populate(Graphic graphic)
        {
            var mesh = new Mesh();
            using (var helper = new VertexHelper())
            {
                graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }
        private static void AssertValidMesh(Mesh mesh)
        {
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            foreach (Vector3 vertex in mesh.vertices)
                Assert.That(float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y), Is.False);
            foreach (int index in mesh.triangles) Assert.That(index, Is.InRange(0, mesh.vertexCount - 1));
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
