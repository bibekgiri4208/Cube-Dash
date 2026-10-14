using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash
{
    /// <summary>Classic GPU instancing of native foliage, retaining its original meshes, wind, LODs and shading.</summary>
    public sealed class CountrySidePlantInstancing : IDisposable
    {
        private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        private readonly List<(Renderer renderer, Material[] original, Material[] instanced)> renderers = new List<(Renderer, Material[], Material[])>();
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        public int RendererCount => renderers.Count;

        public CountrySidePlantInstancing(Transform[] plants, Material[] retainedVariants)
        {
            if (!SystemInfo.supportsInstancing) return;
            var shaders = new HashSet<Shader>();
            // Serialized template materials retain INSTANCING_ON variants in player builds.
            foreach (Material template in retainedVariants)
                if (template != null && template.enableInstancing) shaders.Add(template.shader);
            if (shaders.Count == 0) return;
            // SRP Batcher otherwise takes priority over classic GPU instancing. This deliberately
            // unused property opts the renderer out without overriding any native shader input.
            block.SetFloat(Shader.PropertyToID("_CubeDashPlantBatch"), 1);
            var seen = new HashSet<Renderer>();
            foreach (Transform plant in plants)
            {
                if (plant == null) continue;
                foreach (Renderer renderer in plant.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!seen.Add(renderer) || renderer.HasPropertyBlock() || renderer.isPartOfStaticBatch
                        || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
                    Material[] original = renderer.sharedMaterials;
                    bool supported = original.Length > 0;
                    foreach (Material material in original) if (material == null || !shaders.Contains(material.shader)) supported = false;
                    if (!supported) continue;
                    var instanced = new Material[original.Length];
                    for (int i = 0; i < original.Length; i++)
                    {
                        Material source = original[i];
                        if (!copies.TryGetValue(source, out Material copy))
                        {
                            copy = new Material(source) { name = source.name + " (countryside instanced)", enableInstancing = true };
                            copies.Add(source, copy);
                        }
                        instanced[i] = copy;
                    }
                    renderers.Add((renderer, original, instanced));
                }
            }
            SetEnabled(true);
        }

        public void SetEnabled(bool enabled)
        {
            foreach (var item in renderers)
            {
                if (item.renderer == null) continue;
                item.renderer.sharedMaterials = enabled ? item.instanced : item.original;
                item.renderer.SetPropertyBlock(enabled ? block : null);
            }
        }

        public void Dispose()
        {
            SetEnabled(false);
            foreach (Material copy in copies.Values)
                if (Application.isPlaying) Object.Destroy(copy); else Object.DestroyImmediate(copy);
            copies.Clear(); renderers.Clear();
        }
    }
}
