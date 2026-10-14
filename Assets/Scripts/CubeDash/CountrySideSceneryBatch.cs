using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CubeDash
{
    /// <summary>Run-owned, lossless batches of nearby rigid artwork. Native plants and object-space shaders are never merged.</summary>
    public sealed class CountrySideSceneryBatch : IDisposable
    {
        private readonly List<Renderer> sources = new List<Renderer>();
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        public int SourceCount => sources.Count;
        public int BatchCount => objects.Count;

        private readonly struct Key : IEquatable<Key>
        {
            private readonly int cell, side, layer, sortingLayer, sortingOrder;
            private readonly uint renderingLayers;
            private readonly ShadowCastingMode shadows;
            private readonly bool receiveShadows;
            private readonly ReflectionProbeUsage reflection;
            private readonly MotionVectorGenerationMode motion;
            public Key(Renderer renderer, Vector3 center)
            {
                cell = Mathf.FloorToInt(center.z / 32); side = center.x < 0 ? -1 : 1;
                layer = renderer.gameObject.layer; renderingLayers = renderer.renderingLayerMask;
                shadows = renderer.shadowCastingMode; receiveShadows = renderer.receiveShadows;
                reflection = renderer.reflectionProbeUsage; motion = renderer.motionVectorGenerationMode;
                sortingLayer = renderer.sortingLayerID; sortingOrder = renderer.sortingOrder;
            }
            public bool Equals(Key other) => cell == other.cell && side == other.side && layer == other.layer
                && renderingLayers == other.renderingLayers && shadows == other.shadows && receiveShadows == other.receiveShadows
                && reflection == other.reflection && motion == other.motion && sortingLayer == other.sortingLayer && sortingOrder == other.sortingOrder;
            public override bool Equals(object obj) => obj is Key key && Equals(key);
            public override int GetHashCode() => HashCode.Combine(cell, side, layer, renderingLayers, shadows, receiveShadows, reflection, motion);
        }

        public CountrySideSceneryBatch(Transform[] tiles, Renderer[] candidates)
        {
            // Combining per-object SH samples would change their lighting in a probe-lit scene.
            if ((LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count > 0)
                || Object.FindAnyObjectByType<ReflectionProbe>() != null) return;
            foreach (Transform tile in tiles)
            {
                if (tile == null) continue;
                var groups = new Dictionary<Key, List<Renderer>>();
                foreach (Renderer renderer in candidates)
                {
                    if (!CanBatch(renderer, tile)) continue;
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Vector3 center = tile.InverseTransformPoint(renderer.transform.TransformPoint(mesh.bounds.center));
                    var key = new Key(renderer, center);
                    if (!groups.TryGetValue(key, out var group)) groups.Add(key, group = new List<Renderer>());
                    group.Add(renderer);
                }
                foreach (var group in groups.Values) if (group.Count > 1) Build(tile, group);
            }
        }

        private static bool CanBatch(Renderer renderer, Transform tile)
        {
            if (!(renderer is MeshRenderer) || !renderer.enabled || renderer.forceRenderingOff || renderer.isPartOfStaticBatch || !renderer.gameObject.activeInHierarchy
                || !renderer.transform.IsChildOf(tile) || renderer.HasPropertyBlock() || renderer.lightmapIndex >= 0
                || renderer.GetComponentInParent<LODGroup>() != null || renderer.GetComponentInParent<Animator>() != null
                || renderer.GetComponentInParent<Animation>() != null || renderer.probeAnchor != null
                || renderer.lightProbeProxyVolumeOverride != null || renderer.transform.localToWorldMatrix.determinant <= 0
                || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return false;
            Material[] palette = renderer.sharedMaterials;
            if (palette.Length != filter.sharedMesh.subMeshCount || palette.Length == 0) return false;
            foreach (Material material in palette)
                if (material == null || material.shader.name != "CubeDash/Curved Lit" || material.renderQueue >= 2500) return false;
            return true;
        }

        private void Build(Transform tile, List<Renderer> group)
        {
            var parts = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 toTile = tile.worldToLocalMatrix;
            foreach (Renderer renderer in group)
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Material[] palette = renderer.sharedMaterials;
                for (int sub = 0; sub < palette.Length; sub++)
                {
                    if (mesh.GetIndexCount(sub) == 0) continue;
                    if (!parts.TryGetValue(palette[sub], out var instances)) parts.Add(palette[sub], instances = new List<CombineInstance>());
                    instances.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = toTile * renderer.transform.localToWorldMatrix });
                }
            }
            var temporary = new List<Mesh>();
            try
            {
                var combined = new CombineInstance[parts.Count]; var materials = new Material[parts.Count]; int index = 0;
                foreach (var part in parts)
                {
                    var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                    temporary.Add(mesh); mesh.CombineMeshes(part.Value.ToArray(), true, true, false);
                    combined[index] = new CombineInstance { mesh = mesh, transform = Matrix4x4.identity };
                    materials[index++] = part.Key;
                }
                var result = new Mesh { name = "Countryside - lossless rigid batch", indexFormat = IndexFormat.UInt32 };
                meshes.Add(result); result.CombineMeshes(combined, false, false, false);
                // The audited Curved Lit shader consumes only position, normal and UV0.
                // Do not carry unused tangent/color/rise channels or retain a second CPU mesh copy.
                result.tangents = Array.Empty<Vector4>(); result.colors = Array.Empty<Color>(); result.uv3 = Array.Empty<Vector2>();
                result.UploadMeshData(true);
                var item = new GameObject("Countryside rigid batch", typeof(MeshFilter), typeof(MeshRenderer));
                objects.Add(item); item.transform.SetParent(tile, false); item.layer = group[0].gameObject.layer;
                item.GetComponent<MeshFilter>().sharedMesh = result;
                var target = item.GetComponent<MeshRenderer>(); Renderer source = group[0];
                target.sharedMaterials = materials; target.shadowCastingMode = source.shadowCastingMode;
                target.receiveShadows = source.receiveShadows; target.renderingLayerMask = source.renderingLayerMask;
                target.lightProbeUsage = LightProbeUsage.Off; target.reflectionProbeUsage = source.reflectionProbeUsage;
                target.motionVectorGenerationMode = source.motionVectorGenerationMode;
                target.sortingLayerID = source.sortingLayerID; target.sortingOrder = source.sortingOrder;
                target.localBounds = HighwayPresentation.CurveBounds(result.bounds, item.transform);
                foreach (Renderer renderer in group) { renderer.enabled = false; sources.Add(renderer); }
            }
            finally { foreach (Mesh mesh in temporary) Destroy(mesh); }
        }

        public void Dispose()
        {
            foreach (Renderer source in sources) if (source != null) source.enabled = true;
            foreach (GameObject item in objects) Destroy(item);
            foreach (Mesh mesh in meshes) Destroy(mesh);
            sources.Clear(); objects.Clear(); meshes.Clear();
        }
        private static void Destroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
