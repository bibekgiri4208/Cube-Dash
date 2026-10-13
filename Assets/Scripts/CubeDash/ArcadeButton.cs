using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Small, unscaled-time button feedback, including while the game is paused.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class ArcadeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private bool hovered;
        private bool pressed;
        private bool selected;
        private Button button;
        private Outline outline;

        private void Awake()
        {
            button = GetComponent<Button>();
            outline = GetComponent<Outline>();
            if (outline != null) outline.enabled = selected;
        }
        private void Update()
        {
            float scale = button.IsInteractable() ? (pressed ? 0.95f : selected ? 1.05f : hovered ? 1.035f : 1f) : 1f;
            Vector3 target = Vector3.one * scale;
            Vector3 current = transform.localScale;
            // Keep polling interactability, but don't dirty the Canvas once feedback has settled.
            if (current.Equals(target)) return;
            Vector3 next = Vector3.Lerp(current, target, 1 - Mathf.Exp(-22f * Time.unscaledDeltaTime));
            if ((next - target).sqrMagnitude <= 0.0000000001f) next = target;
            if (!current.Equals(next)) transform.localScale = next;
        }
        private void OnDisable()
        {
            hovered = pressed = selected = false;
            if (outline != null) outline.enabled = false;
            transform.localScale = Vector3.one;
        }
        public void OnPointerEnter(PointerEventData data) => hovered = true;
        public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData data) => pressed = false;
        public void OnSelect(BaseEventData data) { selected = true; if (outline != null) outline.enabled = true; }
        public void OnDeselect(BaseEventData data) { selected = pressed = false; if (outline != null) outline.enabled = false; }
    }
}
