using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Multiverse.Games.DragonHunter.EditorTools
{
    /// <summary>
    /// Membangkitkan mesh low-poly bergaya faceted (flat shading) milik Dragon Hunter sebagai aset
    /// .asset di folder Models, plus tekstur partikel lembut. Semua bentuk dibuat dari nol oleh kode ini.
    /// Aset yang sudah ada diperbarui di tempat (GUID tetap) sehingga referensi scene tidak putus.
    /// </summary>
    public static class DragonHunterMeshLibrary
    {
        private const string Root = "Assets/Games/DragonHunter";
        private const string ModelFolder = Root + "/Models";
        private const string TexturePath = Root + "/Textures/T_DH_SoftParticle.png";

        public sealed class Set
        {
            public Mesh Orb, Cone, Frustum, Spike, WingMembrane, Blade, Cape, Ring;
            public Texture2D SoftParticle;
        }

        public static Set EnsureAll()
        {
            if (!AssetDatabase.IsValidFolder(ModelFolder)) AssetDatabase.CreateFolder(Root, "Models");

            var set = new Set
            {
                Orb = Save("DH_Mesh_FacetedOrb", BuildOrb()),
                Cone = Save("DH_Mesh_Cone", BuildCone(8)),
                Frustum = Save("DH_Mesh_Frustum", BuildFrustum(0.62f)),
                Spike = Save("DH_Mesh_Spike", BuildSpike()),
                WingMembrane = Save("DH_Mesh_WingMembrane", BuildWing()),
                Blade = Save("DH_Mesh_Blade", BuildBlade()),
                Cape = Save("DH_Mesh_Cape", BuildCape()),
                Ring = Save("DH_Mesh_ShockwaveRing", BuildRing(0.86f, 64)),
            };
            set.SoftParticle = EnsureSoftParticleTexture();
            AssetDatabase.SaveAssets();
            return set;
        }

        // ================================================================ Simpan
        private static Mesh Save(string name, Mesh built)
        {
            string path = ModelFolder + "/" + name + ".asset";
            built.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }

            existing.Clear();
            existing.vertices = built.vertices;
            existing.normals = built.normals;
            existing.uv = built.uv;
            existing.triangles = built.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(built);
            return existing;
        }

        /// <summary>Mesh flat-shaded: tiap segitiga punya vertex dan normal sendiri.</summary>
        private static Mesh Faceted(IList<Vector3> positions, IList<int> triangles, bool doubleSided = false)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void AddTri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int start = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                uvs.Add(new Vector2(a.x + 0.5f, a.z + 0.5f));
                uvs.Add(new Vector2(b.x + 0.5f, b.z + 0.5f));
                uvs.Add(new Vector2(c.x + 0.5f, c.z + 0.5f));
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            }

            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = positions[triangles[i]], b = positions[triangles[i + 1]], c = positions[triangles[i + 2]];
                AddTri(a, b, c);
                if (doubleSided) AddTri(a, c, b);
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ================================================================ Bentuk
        /// <summary>Icosphere 1x subdivisi (80 sisi), jari-jari 0.5, dengan sedikit jitter deterministik.</summary>
        private static Mesh BuildOrb()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var p = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int idx)) return idx;
                p.Add((p[a] + p[b]) * 0.5f);
                cache[key] = p.Count - 1;
                return p.Count - 1;
            }

            var tris = new List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            var rng = new System.Random(11);
            for (int i = 0; i < p.Count; i++)
                p[i] = p[i].normalized * (0.5f * (0.96f + (float)rng.NextDouble() * 0.06f));

            return Faceted(p, tris);
        }

        /// <summary>Kerucut: alas jari-jari 0.5 di z=0, ujung di z=1 (tanduk, cakar).</summary>
        private static Mesh BuildCone(int sides)
        {
            var p = new List<Vector3> { new Vector3(0, 0, 1), Vector3.zero };
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                p.Add(new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f));
            }
            var tris = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % sides;
                tris.AddRange(new[] { 0, a, b });  // selimut
                tris.AddRange(new[] { 1, b, a });  // alas
            }
            return Faceted(p, tris);
        }

        /// <summary>Balok meruncing: sisi z=+0.5 berukuran 1x1, sisi z=-0.5 mengecil (ekor, leher, moncong).</summary>
        private static Mesh BuildFrustum(float backScale)
        {
            float h = 0.5f, b = 0.5f * backScale;
            var p = new List<Vector3>
            {
                new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h),
                new Vector3(-b, -b, -h), new Vector3(b, -b, -h), new Vector3(b, b, -h), new Vector3(-b, b, -h),
            };
            int[] tris =
            {
                0, 2, 1, 0, 3, 2, // depan
                4, 5, 6, 4, 6, 7, // belakang
                0, 1, 5, 0, 5, 4, // bawah
                3, 7, 6, 3, 6, 2, // atas
                0, 4, 7, 0, 7, 3, // kiri
                1, 2, 6, 1, 6, 5, // kanan
            };
            // Daftar di atas ditulis berlawanan dengan winding depan Unity; dibalik per segitiga
            // agar normal menghadap keluar (diverifikasi oleh Sprint2AssetTests.ClosedMeshes_FaceOutward).
            for (int i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            return Faceted(p, tris);
        }

        /// <summary>Piramida segi empat: alas 1x1 di y=0, ujung di y=1 (duri punggung).</summary>
        private static Mesh BuildSpike()
        {
            var p = new List<Vector3>
            {
                new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f),
                new Vector3(0, 1, -0.15f),
            };
            int[] tris = { 0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0, 0, 1, 2, 0, 2, 3 };
            return Faceted(p, tris);
        }

        /// <summary>Selaput sayap bergaya kelelawar dengan tepi bergerigi, di bidang XZ, panjang 1 ke +X. Dua sisi.</summary>
        private static Mesh BuildWing()
        {
            Vector2[] outline =
            {
                new Vector2(0f, 0.28f), new Vector2(0.45f, 0.38f), new Vector2(1f, 0.34f),     // tulang depan
                new Vector2(0.78f, 0.02f), new Vector2(0.92f, -0.32f),                        // jari 1
                new Vector2(0.62f, -0.22f), new Vector2(0.62f, -0.66f),                       // jari 2
                new Vector2(0.38f, -0.42f), new Vector2(0.24f, -0.72f),                       // jari 3
                new Vector2(0.12f, -0.38f), new Vector2(0f, -0.36f),                          // pangkal belakang
            };
            var p = new List<Vector3> { new Vector3(0.05f, 0f, 0f) };
            foreach (Vector2 o in outline) p.Add(new Vector3(o.x, 0f, o.y));
            var tris = new List<int>();
            for (int i = 1; i < p.Count - 1; i++) tris.AddRange(new[] { 0, i, i + 1 });
            return Faceted(p, tris, doubleSided: true);
        }

        /// <summary>Bilah pedang berpenampang belah ketupat, pangkal z=0, ujung runcing z=1, lebar 1 (diskalakan).</summary>
        private static Mesh BuildBlade()
        {
            const float w = 0.5f, th = 0.12f, shoulder = 0.8f;
            var p = new List<Vector3>
            {
                new Vector3(-w, 0, 0), new Vector3(0, th, 0), new Vector3(w, 0, 0), new Vector3(0, -th, 0),
                new Vector3(-w * 0.85f, 0, shoulder), new Vector3(0, th * 0.8f, shoulder), new Vector3(w * 0.85f, 0, shoulder), new Vector3(0, -th * 0.8f, shoulder),
                new Vector3(0, 0, 1f),
            };
            var tris = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int a = i, b = (i + 1) % 4, c = 4 + (i + 1) % 4, d = 4 + i;
                tris.AddRange(new[] { a, d, c, a, c, b });
                tris.AddRange(new[] { d, 8, c });
            }
            tris.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            return Faceted(p, tris);
        }

        /// <summary>Jubah: trapesium melengkung (atas 0.8, bawah 1.0, tinggi 1 ke -Y). Dua sisi.</summary>
        private static Mesh BuildCape()
        {
            const int cols = 4, rows = 4;
            var p = new List<Vector3>();
            for (int r = 0; r <= rows; r++)
            {
                float v = r / (float)rows;
                float width = Mathf.Lerp(0.8f, 1.0f, v);
                for (int c = 0; c <= cols; c++)
                {
                    float u = c / (float)cols - 0.5f;
                    float bulge = -0.12f * (1f - 4f * u * u) * Mathf.Sin(v * Mathf.PI * 0.9f) - 0.1f * v;
                    p.Add(new Vector3(u * width, -v, bulge));
                }
            }
            var tris = new List<int>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int i = r * (cols + 1) + c;
                    tris.AddRange(new[] { i, i + 1, i + cols + 2, i, i + cols + 2, i + cols + 1 });
                }
            return Faceted(p, tris, doubleSided: true);
        }

        /// <summary>Cincin datar (annulus) jari-jari luar 1 untuk gelombang kejut, menghadap atas.</summary>
        private static Mesh BuildRing(float inner, int segments)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts.Add(dir * inner);
                verts.Add(dir);
                uvs.Add(new Vector2(i / (float)segments, 0f));
                uvs.Add(new Vector2(i / (float)segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                tris.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ============================================================== Tekstur
        /// <summary>Tekstur partikel bulat lembut 64x64 (gradien radial), dibuat sendiri.</summary>
        private static Texture2D EnsureSoftParticleTexture()
        {
            string folder = Path.GetDirectoryName(TexturePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, "Textures");

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, 2.2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string full = Path.GetFullPath(TexturePath);
            bool changed = !File.Exists(full) || !SameBytes(File.ReadAllBytes(full), png);
            if (changed)
            {
                File.WriteAllBytes(full, png);
                AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer != null && (importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency || importer.maxTextureSize != 64))
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 64;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
