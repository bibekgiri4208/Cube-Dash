using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved horizon aprons cover only the two outer edges of the scenery pool.</summary>
    public sealed class HorizonBackdrop : MonoBehaviour
    {
        [SerializeField] private GameObject behind = null;
        [SerializeField] private GameObject ahead = null;

        public bool CoversBehind => behind != null && behind.activeSelf;
        public bool CoversAhead => ahead != null && ahead.activeSelf;

        public void SetEdges(bool rear, bool front)
        {
            if (behind != null) behind.SetActive(rear);
            if (ahead != null) ahead.SetActive(front);
        }
    }
}
