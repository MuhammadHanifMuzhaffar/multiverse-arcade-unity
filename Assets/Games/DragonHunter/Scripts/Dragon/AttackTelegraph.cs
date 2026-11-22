using System.Collections.Generic;
using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Penanda area serangan di lantai.
    /// Windup: area peringatan + isian yang membesar sesuai progres (kapan harus dodge).
    /// Aktif: area berubah ke warna bahaya.
    /// </summary>
    public class AttackTelegraph : MonoBehaviour
    {
        [SerializeField] private MeshFilter areaFilter;
        [SerializeField] private MeshRenderer areaRenderer;
        [SerializeField] private MeshFilter fillFilter;
        [SerializeField] private MeshRenderer fillRenderer;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Material activeMaterial;
        [SerializeField] private float groundOffset = 0.04f;

        private readonly Dictionary<int, Mesh> meshCache = new Dictionary<int, Mesh>();
        private MaterialPropertyBlock block;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Color warningBase = Color.white;

        public bool IsVisible => areaRenderer != null && areaRenderer.enabled;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (warningMaterial != null && warningMaterial.HasProperty(ColorId)) warningBase = warningMaterial.color;
            Hide();
        }

        private void OnDestroy()
        {
            foreach (Mesh m in meshCache.Values)
                if (m != null) Destroy(m);
            meshCache.Clear();
        }

        /// <summary>Tampilkan fase windup. progress 0..1.</summary>
        public void ShowWarning(DragonAttackDefinition attack, Vector3 origin, Vector3 forward, float progress)
        {
            if (attack == null) return;
            Place(attack, origin, forward);

            SetRenderer(areaRenderer, areaFilter, attack, warningMaterial, true);
            SetRenderer(fillRenderer, fillFilter, attack, activeMaterial, true);

            float p = Mathf.Clamp01(progress);
            if (fillFilter != null)
                fillFilter.transform.localScale = new Vector3(p, 1f, p);

            // Denyut alpha makin cepat menjelang serangan.
            if (areaRenderer != null)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 22f, p));
                block.SetColor(ColorId, new Color(warningBase.r, warningBase.g, warningBase.b, warningBase.a * pulse));
                areaRenderer.SetPropertyBlock(block);
            }
        }

        /// <summary>Tampilkan fase aktif (area bahaya).</summary>
        public void ShowActive(DragonAttackDefinition attack, Vector3 origin, Vector3 forward)
        {
            if (attack == null) return;
            Place(attack, origin, forward);
            SetRenderer(areaRenderer, areaFilter, attack, activeMaterial, true);
            if (areaRenderer != null) areaRenderer.SetPropertyBlock(null);
            if (fillRenderer != null) fillRenderer.enabled = false;
        }

        public void Hide()
        {
            if (areaRenderer != null) areaRenderer.enabled = false;
            if (fillRenderer != null) fillRenderer.enabled = false;
        }

        private void Place(DragonAttackDefinition attack, Vector3 origin, Vector3 forward)
        {
            Vector3 flatForward = AttackGeometry.Flatten(forward);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;

            transform.SetPositionAndRotation(new Vector3(origin.x, groundOffset, origin.z),
                                             Quaternion.LookRotation(flatForward.normalized, Vector3.up));
            transform.localScale = new Vector3(attack.range, 1f, attack.range);
        }

        private void SetRenderer(MeshRenderer renderer, MeshFilter filter, DragonAttackDefinition attack, Material material, bool enabled)
        {
            if (renderer == null || filter == null) return;
            filter.sharedMesh = GetMesh(attack.shape == AttackShape.Circle ? 360f : attack.angle);
            if (material != null) renderer.sharedMaterial = material;
            renderer.enabled = enabled;
        }

        private Mesh GetMesh(float angle)
        {
            int key = Mathf.RoundToInt(angle);
            if (!meshCache.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = ProceduralMeshes.Sector(angle, key >= 360 ? 64 : 32);
                meshCache[key] = mesh;
            }
            return mesh;
        }
    }
}
