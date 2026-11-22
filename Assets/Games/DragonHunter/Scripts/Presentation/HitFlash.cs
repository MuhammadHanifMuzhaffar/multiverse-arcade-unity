using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Kilatan warna saat terkena serangan + tint permanen (mis. naga Enraged) memakai
    /// MaterialPropertyBlock, sehingga material aset tidak pernah diubah saat runtime.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color flashColor = new Color(1f, 0.93f, 0.85f, 1f);
        [SerializeField] private float flashDuration = 0.12f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock block;
        private Color[] baseColors;
        private Color currentFlash;
        private float flashEndTime = -1f;
        private Color tintColor = Color.white;
        private float tintAmount;
        private bool dirty;

        public void SetRenderers(Renderer[] targets) => renderers = targets;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            CacheBaseColors();
        }

        private void CacheBaseColors()
        {
            if (renderers == null) renderers = new Renderer[0];
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material m = renderers[i] != null ? renderers[i].sharedMaterial : null;
                baseColors[i] = m != null && m.HasProperty(ColorId) ? m.color : Color.white;
            }
        }

        public void Flash() => Flash(flashColor);

        public void Flash(Color color)
        {
            currentFlash = color;
            flashEndTime = Time.time + flashDuration;
            dirty = true;
        }

        public void SetTint(Color color, float amount)
        {
            tintColor = color;
            tintAmount = Mathf.Clamp01(amount);
            dirty = true;
        }

        private void LateUpdate()
        {
            bool flashing = Time.time < flashEndTime;
            if (!flashing && !dirty) return;

            float flashT = flashing ? Mathf.Clamp01((flashEndTime - Time.time) / flashDuration) : 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = Color.Lerp(baseColors[i], tintColor, tintAmount);
                c = Color.Lerp(c, currentFlash, flashT);
                renderers[i].GetPropertyBlock(block);
                block.SetColor(ColorId, c);
                renderers[i].SetPropertyBlock(block);
            }

            // Satu frame tambahan setelah kilatan selesai untuk kembali ke warna dasar.
            dirty = flashing;
        }
    }
}
