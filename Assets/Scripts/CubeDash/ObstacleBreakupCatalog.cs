using System;
using UnityEngine;

namespace CubeDash
{
    public enum ObstacleFragmentRole { Plastic, Frame, Concrete, BodyPanel, Wheel, Glass, Metal }

    /// <summary>Offline-separated pieces of the actual obstacle meshes, in road-space metres.</summary>
    public sealed class ObstacleBreakupCatalog : ScriptableObject
    {
        public const string ResourcePath = "ObstacleBreakups";
        [Serializable]
        public sealed class Fragment
        {
            public Mesh mesh;
            public Vector3 center;
            public ObstacleFragmentRole role;
        }
        [Serializable]
        public sealed class Breakup
        {
            public Mesh source;
            public Mesh[] aliases = new Mesh[0];
            public Fragment[] fragments = new Fragment[0];
        }
        [SerializeField] private Breakup[] models = new Breakup[0];
        [SerializeField] private Material[] materials = new Material[0];
        public Breakup[] Models => models;
        public Material[] Materials => materials;
        public Breakup Fallback => models.Length > 0 ? models[0] : null;
        public Breakup For(Mesh source)
        {
            foreach (Breakup model in models)
            {
                if (model.source == source) return model;
                foreach (Mesh alias in model.aliases) if (alias == source) return model;
            }
            return Fallback;
        }
    }
}
