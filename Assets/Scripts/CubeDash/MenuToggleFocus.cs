using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Highlights a selected checkbox independently of its checked state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Toggle))]
    public sealed class MenuToggleFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Graphic focusFrame;
        private Toggle toggle;
        private bool selected;
        public Graphic FocusFrame => focusFrame;
        public bool Focused { get; private set; }

        private void Awake() => toggle = GetComponent<Toggle>();
        private void OnEnable()
        {
            selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            Refresh();
        }
        private void Update() => Refresh();
        private void Refresh() => SetFocus(selected && toggle != null && toggle.IsActive() && toggle.IsInteractable());
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
