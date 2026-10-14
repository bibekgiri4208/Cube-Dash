using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Small, unscaled-time button feedback, including while the game is paused.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class ArcadeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private bool livelyFeedback;
        [Tooltip("Bounded scale and a white vector focus outline, only for the four main-menu ribbons.")]
        [SerializeField] private bool ribbonFeedback;
        public bool LivelyFeedback => livelyFeedback;
        public bool RibbonFeedback => ribbonFeedback;
        public const float RibbonMaximumScale = 1.035f;
        private float clickAge = -1;
        private bool hovered;
        private bool pressed;
        private bool selected;
        private Button button;
        private Outline outline;
        private MenuRibbonGraphic ribbon;
        private Vector3 scaleVelocity;

        private void Awake()
        {
            button = GetComponent<Button>();
            outline = GetComponent<Outline>();
            ribbon = GetComponent<MenuRibbonGraphic>();
            if (outline != null) outline.enabled = selected;
        }
        private void Update()
        {
            if (ribbonFeedback && ribbon != null) { UpdateRibbon(Time.unscaledDeltaTime); return; }
            float scale = button.IsInteractable() ? (pressed ? 0.95f : selected ? 1.05f : hovered ? 1.035f : 1f) : 1f;
            Vector3 target = Vector3.one * scale;
            if (livelyFeedback)
            {
                bool interactive = button.IsInteractable();
                if (outline != null) outline.enabled = selected && interactive;
                target = !interactive ? Vector3.one : pressed ? new Vector3(0.97f, 0.92f, 1)
                    : Vector3.one * (selected ? 1.055f : hovered ? 1.045f : 1);
                if (clickAge >= 0)
                {
                    clickAge += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(clickAge / 0.28f);
                    target *= 1 - 0.08f * Mathf.Exp(-4 * t) * Mathf.Cos(t * Mathf.PI * 2);
                    if (t == 1) clickAge = -1;
                }
            }
            Vector3 current = transform.localScale;
            // Keep polling interactability, but don't dirty the Canvas once feedback has settled.
            if (current.Equals(target)) return;
            Vector3 next = Vector3.Lerp(current, target, 1 - Mathf.Exp(-22f * Time.unscaledDeltaTime));
            // At very high frame rates, a subpixel Lerp step can round back to the same
            // float forever. Settle that imperceptible tail rather than keeping it alive.
            if ((next - target).sqrMagnitude <= 0.0000000001f ||
                (next.Equals(current) && Time.unscaledDeltaTime > 0 && (current - target).sqrMagnitude <= 0.0000001f)) next = target;
            if (!current.Equals(next)) transform.localScale = next;
        }
        private void UpdateRibbon(float dt)
        {
            dt = Mathf.Clamp(dt, 0, 0.05f);
            bool interactive = button.isActiveAndEnabled && button.IsInteractable();
            bool focused = interactive && (hovered || selected);
            if (outline != null) outline.enabled = false; // The shape-matched ring replaces mesh duplication.
            ribbon.FocusAmount = Mathf.MoveTowards(ribbon.FocusAmount, focused ? 1 : 0, dt * 12);
            float scale = !interactive ? 1 : selected ? RibbonMaximumScale : hovered ? 1.03f : 1;
            Vector3 target = interactive && pressed ? new Vector3(0.985f, 0.94f, 1) : new Vector3(scale, scale, 1);
            if (clickAge >= 0)
            {
                clickAge += dt;
                float t = Mathf.Clamp01(clickAge / 0.18f);
                if (interactive && !pressed) target *= 1 - 0.025f * Mathf.Sin(t * Mathf.PI);
                target.z = 1;
                if (t == 1) clickAge = -1;
            }
            Vector3 current = transform.localScale;
            if (current == target && scaleVelocity == Vector3.zero) return;
            Vector3 next = Vector3.SmoothDamp(current, target, ref scaleVelocity, 0.075f, Mathf.Infinity, dt);
            next.x = Mathf.Clamp(next.x, 0.94f, RibbonMaximumScale);
            next.y = Mathf.Clamp(next.y, 0.94f, RibbonMaximumScale); next.z = 1;
            if ((next - target).sqrMagnitude < 0.00000001f) { next = target; scaleVelocity = Vector3.zero; }
            if (!current.Equals(next)) transform.localScale = next;
        }
        private void OnDisable()
        {
            hovered = pressed = selected = false;
            clickAge = -1;
            scaleVelocity = Vector3.zero;
            if (ribbon != null) ribbon.FocusAmount = 0;
            if (outline != null) outline.enabled = false;
            transform.localScale = Vector3.one;
        }
        public void OnPointerEnter(PointerEventData data)
        {
            hovered = true;
            // One active ribbon indicator for mouse as well as keyboard/gamepad navigation.
            if (ribbonFeedback && button != null && button.isActiveAndEnabled && button.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }
        public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData data) => pressed = false;
        public void OnSelect(BaseEventData data) { selected = true; if (outline != null && !ribbonFeedback) outline.enabled = true; }
        public void OnDeselect(BaseEventData data) { selected = pressed = false; if (outline != null) outline.enabled = false; }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Pulse();
        }
        public void OnSubmit(BaseEventData data) => Pulse();
        private void Pulse()
        {
            if (!livelyFeedback || button == null || !button.isActiveAndEnabled) return;
            bool closing = GetComponentInParent<MenuPanelMotion>()?.Closing == true;
            if (button.IsInteractable() || closing) { pressed = false; clickAge = 0; }
        }
    }
}
