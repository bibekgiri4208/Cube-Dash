using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Shows the authored focus frame without resizing the slider or changing its value.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Slider))]
    public sealed class MenuSliderFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Graphic focusFrame;
        private Slider slider;
        private bool selected;
        public Graphic FocusFrame => focusFrame;
        public bool Focused { get; private set; }

        private void Awake() => slider = GetComponent<Slider>();
        private void OnEnable()
        {
            selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            Refresh();
        }
        private void Update() => Refresh();
        private void Refresh() => SetFocus(selected && slider != null && slider.IsActive() && slider.IsInteractable());
        private void SetFocus(bool value)
        {
            Focused = value;
            if (focusFrame != null && focusFrame.enabled != value) focusFrame.enabled = value;
        }
        private void OnDisable() { selected = false; SetFocus(false); }
        public void OnSelect(BaseEventData data) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData data) { selected = false; SetFocus(false); }
    }
}
