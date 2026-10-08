using UnityEngine;

namespace CubeDash
{
    /// <summary>Animates a saved ribbon of scene meshes behind the cube. No runtime geometry.</summary>
    public sealed class CubeWake : MonoBehaviour
    {
        [SerializeField] private CubeDashGame game = null;
        [SerializeField] private Transform player = null;
        [SerializeField] private Transform[] pieces = new Transform[0];
        [SerializeField, Min(1f)] private float followSharpness = 17f;
        public Transform[] Pieces => pieces;

        public void SetColor(Color color)
        {
            MaterialPropertyBlock appearance = new MaterialPropertyBlock();
            color.a = 0.12f;
            appearance.SetColor("_Tint", color);
            foreach (Transform piece in pieces) piece.GetComponent<Renderer>().SetPropertyBlock(appearance);
        }

        public void ResetWake()
        {
            foreach (Transform piece in pieces)
            {
                Vector3 position = piece.position;
                position.x = player.position.x;
                piece.position = position;
            }
        }

        private void LateUpdate()
        {
            if (game == null || player == null || game.State != CubeDashGame.RunState.Running) return;
            float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            for (int i = 0; i < pieces.Length; i++)
            {
                Vector3 position = pieces[i].position;
                float target = i == 0 ? player.position.x : pieces[i - 1].position.x;
                position.x = Mathf.Lerp(position.x, target, blend);
                pieces[i].position = position;
            }
        }
    }
}
