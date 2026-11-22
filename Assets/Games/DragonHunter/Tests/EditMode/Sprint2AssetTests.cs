using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Multiverse.Games.DragonHunter.Tests
{
    /// <summary>Memastikan aset Sprint 2 ada, valid, dan mematuhi batas README.</summary>
    public class Sprint2AssetTests
    {
        private const string Root = "Assets/Games/DragonHunter";

        private static readonly string[] ClosedMeshes = { "DH_Mesh_FacetedOrb", "DH_Mesh_Cone", "DH_Mesh_Frustum", "DH_Mesh_Spike", "DH_Mesh_Blade" };
        private static readonly string[] AllMeshes =
        {
            "DH_Mesh_FacetedOrb", "DH_Mesh_Cone", "DH_Mesh_Frustum", "DH_Mesh_Spike", "DH_Mesh_Blade",
            "DH_Mesh_WingMembrane", "DH_Mesh_Cape", "DH_Mesh_ShockwaveRing",
        };

        private static Mesh LoadMesh(string name) => AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/Models/" + name + ".asset");

        [Test]
        public void Meshes_ExistAndAreLowPoly([ValueSource(nameof(AllMeshes))] string name)
        {
            Mesh mesh = LoadMesh(name);
            Assert.IsNotNull(mesh, name);
            int triangles = mesh.triangles.Length / 3;
            Assert.Greater(triangles, 0);
            Assert.Less(triangles, 10000, "README: model < 10.000 triangles");
        }

        [Test]
        public void ClosedMeshes_FaceOutward([ValueSource(nameof(ClosedMeshes))] string name)
        {
            // Winding yang salah membuat sisi tak terlihat (backface culling). Semua normal sisi
            // harus menjauhi pusat bentuk.
            Mesh mesh = LoadMesh(name);
            Assert.IsNotNull(mesh, name);
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            Vector3 center = mesh.bounds.center;
            int inward = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 faceCenter = (a + b + c) / 3f;
                if (Vector3.Dot(n, faceCenter - center) < 0f) inward++;
            }
            Assert.AreEqual(0, inward, $"{name}: {inward} sisi menghadap ke dalam.");
        }

        [Test]
        public void UpFacingMeshes_AreVisibleFromAbove()
        {
            foreach (string name in new[] { "DH_Mesh_ShockwaveRing" })
            {
                Mesh mesh = LoadMesh(name);
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                    Assert.Greater(n.y, 0f, name + " harus menghadap ke atas.");
                }
            }
        }

        [Test]
        public void TelegraphSector_FacesUp([Values(36f, 70f, 360f)] float angle)
        {
            Mesh mesh = ProceduralMeshes.Sector(angle);
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
                Assert.Greater(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).y, 0f);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void AudioClips_AreOggAndImported()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio" });
            Assert.GreaterOrEqual(guids.Length, 23, "21 SFX + 2 musik");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.AreEqual(".ogg", Path.GetExtension(path), path);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.Greater(clip.length, 0.05f, path);
            }
        }

        [Test]
        public void MusicLayers_HaveIdenticalLength_ForSyncedCrossfade()
        {
            var a = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Music/DH_Music_Battle.ogg");
            var b = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Music/DH_Music_BattleEnraged.ogg");
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual(a.samples, b.samples);
        }

        [Test]
        public void Font_IsBundledWithLicense()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/Cinzel-Variable.ttf"));
            Assert.IsTrue(File.Exists(Root + "/Fonts/Cinzel-OFL.txt"), "Lisensi OFL wajib ikut didistribusikan bersama font.");
        }

        [Test]
        public void SoftParticleTexture_WithinTextureLimit()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_DH_SoftParticle.png");
            Assert.IsNotNull(tex);
            Assert.LessOrEqual(Mathf.Max(tex.width, tex.height), 1024);
        }
    }
}
