using UnityEngine;

namespace CubeDash
{
    /// <summary>Game-clock-driven armor field on a saved vehicle-shaped mesh; no runtime mesh/material creation.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VehicleShield : MonoBehaviour
    {
        private static readonly int EffectAgeId = Shader.PropertyToID("_EffectAge");
        private MeshRenderer fieldRenderer;
        private MaterialPropertyBlock properties;
        private float age;
        public float EffectAge => age;
        public bool Visible => isActiveAndEnabled && FieldRenderer.enabled;
        public MeshRenderer FieldRenderer => fieldRenderer != null ? fieldRenderer : fieldRenderer = GetComponent<MeshRenderer>();

        private void Awake() => ResetField();

        public void Tick(float dt, bool protectedVehicle)
        {
            if (!protectedVehicle || !isActiveAndEnabled) { ResetField(); return; }
            age += Mathf.Max(0, dt);
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetFloat(EffectAgeId, age);
            FieldRenderer.SetPropertyBlock(properties);
            FieldRenderer.enabled = true;
        }

        public void ResetField()
        {
            age = 0;
            FieldRenderer.enabled = false;
        }

        private void OnDisable() => ResetField();
    }
}
