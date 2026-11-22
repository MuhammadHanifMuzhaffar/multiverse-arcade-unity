using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Mesh datar yang dibangkitkan saat runtime untuk penanda area serangan (tanpa file aset).
    /// </summary>
    public static class ProceduralMeshes
    {
        /// <summary>
        /// Juring (sector) berjari-jari 1 di bidang XZ, menghadap +Z. angle 360 = lingkaran penuh.
        /// Skala transform dipakai untuk jari-jari sebenarnya.
        /// </summary>
        public static Mesh Sector(float fullAngleDegrees, int segments = 40)
        {
            fullAngleDegrees = Mathf.Clamp(fullAngleDegrees, 1f, 360f);
            segments = Mathf.Max(3, segments);

            var vertices = new Vector3[segments + 2];
            var uvs = new Vector2[segments + 2];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            float half = fullAngleDegrees * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float deg = Mathf.Lerp(-half, half, i / (float)segments);
                float rad = deg * Mathf.Deg2Rad;
                var v = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                vertices[i + 1] = v;
                uvs[i + 1] = new Vector2(0.5f + v.x * 0.5f, 0.5f + v.z * 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            var mesh = new Mesh { name = $"DH_Sector_{fullAngleDegrees:0}" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
