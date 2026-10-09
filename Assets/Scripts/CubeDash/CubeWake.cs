using UnityEngine;

namespace CubeDash
{
    /// <summary>A distance-sampled ribbon: the tail follows the driven path, not the frame rate.</summary>
    public sealed class CubeWake : MonoBehaviour
    {
        [SerializeField] private CubeDashGame game = null;
        [SerializeField] private Transform player = null;
        [SerializeField] private Transform[] pieces = new Transform[0];
        [SerializeField, Min(0.1f)] private float minimumLength = 2.8f;
        [SerializeField, Min(0.1f)] private float maximumLength = 4.4f;
        private const int HistorySize = 160;
        private const float SampleSpacing = 0.05f;
        private readonly float[] history = new float[HistorySize];
        private Renderer[] renderers;
        private MaterialPropertyBlock appearance;
        private Color trailColor;
        private int newest;
        private float sampleDistance;
        private float previousDistance;
        private float previousX;
        public Transform[] Pieces => pieces;

        private void EnsureInitialized()
        {
            if (renderers != null) return;
            renderers = new Renderer[pieces.Length];
            appearance = new MaterialPropertyBlock();
            for (int i = 0; i < pieces.Length; i++) renderers[i] = pieces[i].GetComponent<Renderer>();
        }

        public void SetColor(Color color)
        {
            EnsureInitialized();
            trailColor = color;
            UpdateAppearance(0);
        }

        private void UpdateAppearance(float pulse)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                float taper = 1f - (i + 0.5f) / pieces.Length;
                Color color = trailColor * (1.4f + pulse * 1.8f);
                color.a = Mathf.Pow(taper, 1.3f) * (0.62f + pulse * 0.2f);
                appearance.SetColor("_Tint", color);
                renderers[i].SetPropertyBlock(appearance);
            }
        }

        public void ResetWake()
        {
            EnsureInitialized();
            newest = 0;
            sampleDistance = previousDistance = game != null ? game.Distance : 0;
            previousX = player.position.x;
            for (int i = 0; i < HistorySize; i++) history[i] = previousX;
            float spacing = minimumLength / Mathf.Max(1, pieces.Length);
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i].position = new Vector3(player.position.x, 0.024f, player.position.z - 0.58f - (i + 0.5f) * spacing);
                pieces[i].rotation = Quaternion.identity;
                pieces[i].localScale = new Vector3(0.85f * Mathf.Pow(1f - (i + 0.5f) / pieces.Length, 0.7f),
                    0.012f, spacing + 0.015f);
            }
            UpdateAppearance(0);
        }

        private void Start() => ResetWake();

        private float SampleX(float distance)
        {
            if (distance >= sampleDistance)
                return Mathf.Lerp(history[newest], player.position.x,
                    Mathf.InverseLerp(sampleDistance, game.Distance, distance));
            float age = Mathf.Clamp((sampleDistance - distance) / SampleSpacing, 0, HistorySize - 2);
            int index = Mathf.FloorToInt(age);
            return Mathf.Lerp(history[(newest - index + HistorySize) % HistorySize],
                history[(newest - index - 1 + HistorySize) % HistorySize], age - index);
        }

        private void LateUpdate()
        {
            if (game == null || player == null || game.State != CubeDashGame.RunState.Running) return;
            EnsureInitialized();
            float distance = game.Distance;
            // Bounded even when a frame spans more than the entire tail.
            if (distance - sampleDistance > (HistorySize - 1) * SampleSpacing)
                sampleDistance = distance - (HistorySize - 1) * SampleSpacing;
            while (sampleDistance + SampleSpacing <= distance)
            {
                sampleDistance += SampleSpacing;
                newest = (newest + 1) % HistorySize;
                history[newest] = Mathf.Lerp(previousX, player.position.x,
                    Mathf.InverseLerp(previousDistance, distance, sampleDistance));
            }
            previousDistance = distance;
            previousX = player.position.x;
            float length = Mathf.Lerp(minimumLength, maximumLength, Mathf.InverseLerp(10f, 24f, game.Speed));
            float spacing = length / Mathf.Max(1, pieces.Length);
            for (int i = 0; i < pieces.Length; i++)
            {
                float near = 0.58f + i * spacing;
                float far = near + spacing;
                Vector3 a = new Vector3(SampleX(distance - i * spacing), 0.024f, player.position.z - near);
                Vector3 b = new Vector3(SampleX(distance - (i + 1) * spacing), 0.024f, player.position.z - far);
                pieces[i].position = (a + b) * 0.5f;
                pieces[i].rotation = Quaternion.LookRotation(a - b, Vector3.up);
                float taper = Mathf.Pow(1f - (i + 0.5f) / pieces.Length, 0.7f);
                pieces[i].localScale = new Vector3((0.85f + game.CollectionPulse * 0.12f) * taper,
                    0.012f, Vector3.Distance(a, b) + 0.015f);
            }
            UpdateAppearance(game.CollectionPulse);
        }
    }
}
