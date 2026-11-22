using System;
using System.Collections.Generic;
using Multiverse.Hub;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Multiverse.Games.DragonHunter.EditorTools
{
    /// <summary>
    /// Menyusun ulang scene Dragon Hunter secara deterministik: arena, Hunter, naga, telegraf,
    /// kamera, HUD, dan panel pause/hasil. Semua material dibuat di folder Dragon Hunter sendiri
    /// (aturan isolasi aset). Aturan proyek tetap dipenuhi: 1 Main Camera + 1 AudioListener,
    /// Canvas 1920x1080 match 0.5, EventSystem, dan tombol Back to Hub (ReturnToHub).
    /// </summary>
    public static class DragonHunterSceneBuilder
    {
        private const string Root = "Assets/Games/DragonHunter";
        private const string MaterialFolder = Root + "/Materials";
        private static string ScenePath => Root + "/Scenes/DragonHunter_Main.unity";

        // Palet datar Dragon Hunter: ember oranye di atas arang hangat.
        private static readonly Color Bg = Hex("#17110F");
        private static readonly Color Surface = Hex("#241915");
        private static readonly Color SurfaceHover = Hex("#33231D");
        private static readonly Color TextColor = Hex("#F0E6DF");
        private static readonly Color TextMuted = Hex("#B8A79B");
        private static readonly Color Accent = Hex("#D9552B");
        private static readonly Color AccentSoft = Hex("#E0A93B");

        private struct Palette
        {
            public Material Ground, GroundInner, Rim, Pillar, EmberCrack;
            public Material HunterArmor, HunterCloak, HunterHelm, HunterVisor, Blade, Gold, Arrow;
            public Material DragonScale, DragonDark, DragonBelly, DragonWing, DragonHorn, DragonEye;
            public Material TelegraphWarn, TelegraphActive, Fire;
            public Material Spark, Dust, HitEmber, SlashTrail, ArrowTrail, Shockwave, AmbientEmber;
        }

        // Diisi di awal Build(); dipakai helper pembuat objek.
        private static DragonHunterMeshLibrary.Set meshes;
        private static Font uiFont;
        private static DragonHunterAudio clickAudio;
        private const string AudioFolder = Root + "/Audio";

        [MenuItem("Tools/Multiverse/Dragon Hunter/Rebuild Scene", false, 100)]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Dragon Hunter",
                    "Scene DragonHunter_Main akan disusun ulang dari kode (isi scene saat ini diganti). Lanjutkan?",
                    "Rebuild", "Batal"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Build();
            EditorUtility.DisplayDialog("Dragon Hunter", "Scene berhasil disusun ulang.", "OK");
        }

        /// <summary>CLI: -executeMethod Multiverse.Games.DragonHunter.EditorTools.DragonHunterSceneBuilder.RebuildBatch</summary>
        public static void RebuildBatch()
        {
            try
            {
                Build();
                Debug.Log("[DragonHunter] Scene builder selesai: " + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[DragonHunter] Scene builder gagal: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            meshes = DragonHunterMeshLibrary.EnsureAll();
            uiFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/Cinzel-Variable.ttf");
            ConfigureAudioImports();
            Palette p = CreateMaterials();

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (GameObject rootObject in scene.GetRootGameObjects())
                Object.DestroyImmediate(rootObject);

            ConfigureEnvironment();

            Camera cam = CreateCamera(out ThirdPersonCamera cameraRig);
            CreateLights();
            CreateArena(p);

            HunterController hunter = CreateHunter(p, out HunterCombat combat, out HunterInput input);
            DragonBoss dragon = CreateDragon(p, hunter);
            AttackTelegraph telegraph = CreateTelegraph(p);
            Set(dragon, "telegraph", telegraph);

            Set(hunter, "cameraTransform", cam.transform);
            Set(combat, "target", dragon);
            Set(combat, "aimCamera", cam.transform);
            cameraRig.SetTargets(hunter.transform, dragon.transform);
            Set(cameraRig, "target", hunter.transform);
            Set(cameraRig, "lockTarget", dragon.transform);

            var managerGo = new GameObject("GameManager");
            var manager = managerGo.AddComponent<DragonHunterManager>();

            Set(input, "hunter", hunter);
            Set(input, "combat", combat);
            Set(input, "cameraRig", cameraRig);
            Set(input, "manager", manager);

            DragonHunterAudio audio = CreateAudio(managerGo.transform, manager, hunter, combat, dragon);
            clickAudio = audio;
            CreateVfx(p, hunter, combat, dragon, cameraRig);

            CreateEventSystem();
            BuildUi(manager, out DragonHunterHUD hud);
            clickAudio = null;

            Set(manager, "hunter", hunter);
            Set(manager, "combat", combat);
            Set(manager, "dragon", dragon);
            Set(manager, "cameraRig", cameraRig);
            Set(manager, "hud", hud);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Gagal menyimpan scene " + ScenePath);
            AssetDatabase.SaveAssets();
        }

        // ================================================================= Material
        private static Palette CreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(Root, "Materials");

            var p = new Palette
            {
                Ground = Lit("M_DH_ArenaGround", "#2B211D", 0.15f),
                GroundInner = Lit("M_DH_ArenaInner", "#3A2A22", 0.25f),
                Rim = Lit("M_DH_ArenaRim", "#46332A", 0.1f),
                Pillar = Lit("M_DH_ArenaPillar", "#54402F", 0.05f),
                EmberCrack = Lit("M_DH_EmberCrack", "#3A140B", 0.3f, emission: "#E0561F", emissionStrength: 1.6f),

                HunterArmor = Lit("M_DH_HunterArmor", "#7E8A90", 0.45f, metallic: 0.4f),
                HunterCloak = Lit("M_DH_HunterCloak", "#6B2E22", 0.1f),
                HunterHelm = Lit("M_DH_HunterHelm", "#5F686D", 0.55f, metallic: 0.5f),
                HunterVisor = Lit("M_DH_HunterVisor", "#3A1A0C", 0.6f, emission: "#FFB347", emissionStrength: 1.4f),
                Blade = Lit("M_DH_Blade", "#D9D2C6", 0.8f, metallic: 0.7f),
                Gold = Lit("M_DH_Gold", "#B88A3E", 0.6f, metallic: 0.8f),
                Arrow = Lit("M_DH_Arrow", "#5A3A16", 0.3f, emission: "#F0B04A", emissionStrength: 1.2f),

                DragonScale = Lit("M_DH_DragonScale", "#7A2318", 0.35f),
                DragonDark = Lit("M_DH_DragonDark", "#4A140E", 0.3f),
                DragonBelly = Lit("M_DH_DragonBelly", "#B4572E", 0.2f),
                DragonWing = Lit("M_DH_DragonWing", "#5A1A14", 0.15f),
                DragonHorn = Lit("M_DH_DragonHorn", "#E6D3B3", 0.4f),
                DragonEye = Lit("M_DH_DragonEye", "#5A2A00", 0.7f, emission: "#FFC04D", emissionStrength: 2.2f),

                TelegraphWarn = Unlit("M_DH_TelegraphWarn", new Color(1f, 0.55f, 0.2f, 0.32f)),
                TelegraphActive = Unlit("M_DH_TelegraphActive", new Color(1f, 0.24f, 0.1f, 0.55f)),
                Fire = ParticleMaterial("M_DH_FireParticle", new Color(1f, 0.55f, 0.18f, 0.8f), additive: true),
                Spark = ParticleMaterial("M_DH_Spark", new Color(1f, 0.85f, 0.55f, 0.9f), additive: true),
                HitEmber = ParticleMaterial("M_DH_HitEmber", new Color(1f, 0.4f, 0.12f, 0.8f), additive: true),
                AmbientEmber = ParticleMaterial("M_DH_AmbientEmber", new Color(1f, 0.5f, 0.2f, 0.7f), additive: true),
                Dust = ParticleMaterial("M_DH_Dust", new Color(0.45f, 0.36f, 0.3f, 0.55f), additive: false),
                SlashTrail = Unlit("M_DH_SlashTrail", new Color(1f, 0.92f, 0.8f, 0.85f)),
                ArrowTrail = Unlit("M_DH_ArrowTrail", new Color(1f, 0.75f, 0.35f, 0.6f)),
                Shockwave = Unlit("M_DH_Shockwave", new Color(1f, 0.45f, 0.15f, 0.75f)),
            };

            AssetDatabase.SaveAssets();
            return p;
        }

        private static Material Lit(string name, string hex, float smoothness, float metallic = 0f,
                                    string emission = null, float emissionStrength = 1f)
        {
            Material m = LoadOrCreate(name, "Standard");
            m.color = Hex(hex);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Hex(emission) * emissionStrength);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Unlit(string name, Color color)
        {
            Material m = LoadOrCreate(name, "Sprites/Default");
            m.color = color;
            m.renderQueue = (int)RenderQueue.Transparent + 10;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material ParticleMaterial(string name, Color tint, bool additive)
        {
            Material m = LoadOrCreate(name, additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended",
                                      "Sprites/Default");
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);
            else m.color = tint;
            if (meshes != null && meshes.SoftParticle != null && m.HasProperty("_MainTex")) m.mainTexture = meshes.SoftParticle;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LoadOrCreate(string name, string shaderName, string fallbackShader = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null && fallbackShader != null) shader = Shader.Find(fallbackShader);
            if (shader == null) throw new InvalidOperationException("Shader tidak ditemukan: " + shaderName);

            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        // ============================================================== Lingkungan
        private static void ConfigureEnvironment()
        {
            RenderSettings.skybox = null; // tanpa skybox bawaan Unity
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("#4A362D");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Bg;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 75f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        private static Camera CreateCamera(out ThirdPersonCamera rig)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 160f;
            go.transform.position = new Vector3(0f, 4f, -18f);
            go.AddComponent<AudioListener>();
            rig = go.AddComponent<ThirdPersonCamera>();
            return cam;
        }

        private static void CreateLights()
        {
            var sun = new GameObject("Sun_Ember");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex("#FFD2A8");
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            // Cahaya bara di tepi arena.
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                var glow = new GameObject("EmberGlow_" + i);
                var l = glow.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = Hex("#FF6A2A");
                l.range = 14f;
                l.intensity = 1.6f;
                l.shadows = LightShadows.None;
                glow.transform.position = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 1.5f, 19f);
            }
        }

        private static void CreateAmbientEmbers(Transform parent, Material material)
        {
            var go = new GameObject("AmbientEmbers");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("#FFB25A"), Hex("#FF5A1F"));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 28f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(38f, 38f, 0.2f);
            shape.rotation = new Vector3(-90f, 0f, 0f); // bidang datar, emisi ke atas

            ParticleSystem.NoiseModule noiseModule = ps.noise;
            noiseModule.enabled = true;
            noiseModule.strength = 0.6f;
            noiseModule.frequency = 0.3f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            ConfigureParticleRenderer(go, material);
        }

        private static ParticleSystem CreateBurstSystem(string name, Transform parent, Material material, Color a, Color b,
                                                        Vector2 speed, Vector2 size, Vector2 life, float gravity,
                                                        ParticleSystemShapeType shapeType, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = shapeType;
            shape.radius = radius;
            if (shapeType == ParticleSystemShapeType.Circle) shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule sizeModule = ps.sizeOverLifetime;
            sizeModule.enabled = true;
            sizeModule.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, gravity < 0f ? 2.2f : 0.3f));

            ConfigureParticleRenderer(go, material);
            return ps;
        }

        private static void ConfigureParticleRenderer(GameObject go, Material material)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void CreateVfx(Palette p, HunterController hunter, HunterCombat combat, DragonBoss dragon, ThirdPersonCamera cameraRig)
        {
            var root = new GameObject("VFX");
            ParticleSystem sparks = CreateBurstSystem("HitSparks", root.transform, p.Spark, Hex("#FFF1C9"), Hex("#FFB347"),
                                                      new Vector2(4f, 11f), new Vector2(0.08f, 0.22f), new Vector2(0.15f, 0.4f), 1.2f,
                                                      ParticleSystemShapeType.Sphere, 0.2f);
            ParticleSystem embers = CreateBurstSystem("HitEmbers", root.transform, p.HitEmber, Hex("#FF8A3D"), Hex("#C8361B"),
                                                      new Vector2(1.5f, 4f), new Vector2(0.15f, 0.4f), new Vector2(0.4f, 0.9f), -0.2f,
                                                      ParticleSystemShapeType.Sphere, 0.35f);
            ParticleSystem dust = CreateBurstSystem("Dust", root.transform, p.Dust, Hex("#6B574A"), Hex("#3E322B"),
                                                    new Vector2(1.5f, 5f), new Vector2(0.6f, 1.6f), new Vector2(0.6f, 1.4f), -0.05f,
                                                    ParticleSystemShapeType.Circle, 1.5f);

            var ring = new GameObject("NovaShockwave");
            ring.transform.SetParent(root.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = meshes.Ring;
            var ringRenderer = ring.AddComponent<MeshRenderer>();
            ConfigureFlatRenderer(ringRenderer, p.Shockwave);

            var vfx = root.AddComponent<DragonHunterVfx>();
            Set(vfx, "hunter", hunter);
            Set(vfx, "combat", combat);
            Set(vfx, "dragon", dragon);
            Set(vfx, "cameraRig", cameraRig);
            Set(vfx, "sparks", sparks);
            Set(vfx, "dust", dust);
            Set(vfx, "hitEmbers", embers);
            Set(vfx, "shockwave", ringRenderer);
        }

        // ================================================================= Audio
        private static readonly string[] MusicClips = { "DH_Music_Battle", "DH_Music_BattleEnraged" };

        private static void ConfigureAudioImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                bool music = path.Contains("/Music/");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                // Musik: terkompresi di memori agar kedua lapis bisa dimulai sinkron. SFX: dekompresi saat load (latensi rendah).
                AudioClipLoadType loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                float quality = music ? 0.6f : 0.7f;
                if (settings.loadType == loadType && settings.compressionFormat == AudioCompressionFormat.Vorbis
                    && Mathf.Approximately(settings.quality, quality) && !importer.loadInBackground)
                    continue;

                settings.loadType = loadType;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = quality;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
        }

        private static AudioClip Clip(string name)
        {
            string folder = Array.IndexOf(MusicClips, name) >= 0 ? "/Music/" : "/SFX/";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + folder + name + ".ogg");
            if (clip == null) throw new InvalidOperationException("Audio tidak ditemukan: " + name + " (jalankan Tools~/generate_audio.py)");
            return clip;
        }

        private static DragonHunterAudio CreateAudio(Transform parent, DragonHunterManager manager, HunterController hunter,
                                                     HunterCombat combat, DragonBoss dragon)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var audio = go.AddComponent<DragonHunterAudio>();
            Set(audio, "manager", manager);
            Set(audio, "hunter", hunter);
            Set(audio, "combat", combat);
            Set(audio, "dragon", dragon);

            var map = new Dictionary<string, string>
            {
                { "swordSwing1", "DH_Sfx_SwordSwing1" }, { "swordSwing2", "DH_Sfx_SwordSwing2" },
                { "swordHit", "DH_Sfx_SwordHit" }, { "heavyHit", "DH_Sfx_HeavyHit" },
                { "arrowShoot", "DH_Sfx_ArrowShoot" }, { "arrowHit", "DH_Sfx_ArrowHit" },
                { "dodge", "DH_Sfx_Dodge" }, { "hunterHurt", "DH_Sfx_HunterHurt" }, { "potion", "DH_Sfx_Potion" },
                { "dragonGrowl", "DH_Sfx_DragonGrowl" }, { "dragonRoar", "DH_Sfx_DragonRoar" },
                { "dragonHurt", "DH_Sfx_DragonHurt" }, { "dragonDeath", "DH_Sfx_DragonDeath" },
                { "biteSnap", "DH_Sfx_BiteSnap" }, { "tailWhoosh", "DH_Sfx_TailWhoosh" },
                { "fireBreath", "DH_Sfx_FireBreath" }, { "chargeStomp", "DH_Sfx_ChargeStomp" },
                { "novaBlast", "DH_Sfx_NovaBlast" },
                { "musicBattle", "DH_Music_Battle" }, { "musicEnraged", "DH_Music_BattleEnraged" },
                { "victory", "DH_Sfx_Victory" }, { "defeat", "DH_Sfx_Defeat" }, { "uiClick", "DH_Sfx_UiClick" },
            };
            foreach (KeyValuePair<string, string> pair in map) Set(audio, "clips." + pair.Key, Clip(pair.Value));
            return audio;
        }

        private static void CreateArena(Palette p)
        {
            var arena = new GameObject("Arena").transform;

            // Collider lantai datar (tak terlihat) + visual lantai bundar.
            var floorCollider = new GameObject("FloorCollider");
            floorCollider.transform.SetParent(arena, false);
            floorCollider.transform.position = new Vector3(0f, -0.5f, 0f);
            floorCollider.AddComponent<BoxCollider>().size = new Vector3(70f, 1f, 70f);

            Primitive(PrimitiveType.Cylinder, "Floor", arena, new Vector3(0f, -0.1f, 0f), Vector3.zero,
                      new Vector3(42f, 0.1f, 42f), p.Ground, collider: false);
            Primitive(PrimitiveType.Cylinder, "FloorSeal", arena, new Vector3(0f, 0.005f, 0f), Vector3.zero,
                      new Vector3(13f, 0.005f, 13f), p.GroundInner, collider: false);

            // Cincin dinding rendah sebagai batas arena (berpenghalang).
            const int segments = 28;
            const float wallRadius = 20.5f;
            float segmentLength = 2f * Mathf.PI * wallRadius / segments + 0.25f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 360f / segments;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Primitive(PrimitiveType.Cube, "Rim_" + i, arena, rot * new Vector3(0f, 0.6f, wallRadius),
                          new Vector3(0f, angle, 0f), new Vector3(segmentLength, 1.2f, 1.4f), p.Rim, collider: true);
            }

            // Pilar pecah di luar cincin (dekorasi, tinggi bervariasi tetapi deterministik).
            var random = new System.Random(7);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f + 22.5f;
                float height = 4f + (float)random.NextDouble() * 6f;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Primitive(PrimitiveType.Cube, "Pillar_" + i, arena, rot * new Vector3(0f, height * 0.5f, 24f),
                          new Vector3(0f, angle + (float)random.NextDouble() * 20f, (float)random.NextDouble() * 6f - 3f),
                          new Vector3(2.2f, height, 2.2f), p.Pillar, collider: false);
            }

            CreateAmbientEmbers(arena, p.AmbientEmber);

            // Retakan bara di lantai.
            for (int i = 0; i < 18; i++)
            {
                float angle = (float)random.NextDouble() * 360f;
                float radius = 3f + (float)random.NextDouble() * 15f;
                Vector3 pos = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.012f, radius);
                float length = 1.5f + (float)random.NextDouble() * 3.5f;
                Primitive(PrimitiveType.Cube, "EmberCrack_" + i, arena, pos,
                          new Vector3(0f, (float)random.NextDouble() * 180f, 0f),
                          new Vector3(0.12f + (float)random.NextDouble() * 0.12f, 0.02f, length), p.EmberCrack, collider: false,
                          castShadows: false);
            }
        }

        // ================================================================= Hunter
        private static HunterController CreateHunter(Palette p, out HunterCombat combat, out HunterInput input)
        {
            var root = new GameObject("Hunter");
            root.transform.position = new Vector3(0f, 0.05f, -10f);

            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.skinWidth = 0.05f;
            cc.stepOffset = 0.3f;

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var renderers = new List<Renderer>
            {
                // Badan & kaki
                Part(meshes.Frustum, "Torso", visual, new Vector3(0f, 1.12f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0.62f, 0.4f, 0.62f), p.HunterArmor),
                Part(meshes.Frustum, "Waist", visual, new Vector3(0f, 0.78f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0.5f, 0.36f, 0.24f), p.HunterCloak),
                Part(meshes.Frustum, "LegL", visual, new Vector3(-0.15f, 0.36f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0.2f, 0.24f, 0.72f), p.HunterHelm),
                Part(meshes.Frustum, "LegR", visual, new Vector3(0.15f, 0.36f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0.2f, 0.24f, 0.72f), p.HunterHelm),
                Part(meshes.Orb, "PauldronL", visual, new Vector3(-0.38f, 1.4f, 0f), new Vector3(0f, 0f, 20f), new Vector3(0.36f, 0.22f, 0.4f), p.HunterHelm),
                Part(meshes.Orb, "PauldronR", visual, new Vector3(0.38f, 1.4f, 0f), new Vector3(0f, 0f, -20f), new Vector3(0.36f, 0.22f, 0.4f), p.HunterHelm),
                Part(meshes.Frustum, "ArmL", visual, new Vector3(-0.42f, 1.08f, 0.04f), new Vector3(-90f, 0f, 8f), new Vector3(0.15f, 0.15f, 0.55f), p.HunterArmor),
                // Kepala
                Part(meshes.Orb, "Helm", visual, new Vector3(0f, 1.72f, 0f), Vector3.zero, new Vector3(0.44f, 0.46f, 0.46f), p.HunterHelm),
                Part(meshes.Cone, "HelmHornL", visual, new Vector3(-0.2f, 1.86f, -0.02f), new Vector3(-60f, -30f, 0f), new Vector3(0.08f, 0.08f, 0.28f), p.DragonHorn),
                Part(meshes.Cone, "HelmHornR", visual, new Vector3(0.2f, 1.86f, -0.02f), new Vector3(-60f, 30f, 0f), new Vector3(0.08f, 0.08f, 0.28f), p.DragonHorn),
                // Jubah & tabung panah
                Part(meshes.Cape, "Cape", visual, new Vector3(0f, 1.46f, -0.3f), new Vector3(8f, 0f, 0f), new Vector3(0.7f, 1.12f, 1f), p.HunterCloak),
                Part(meshes.Frustum, "Quiver", visual, new Vector3(0.2f, 1.2f, -0.34f), new Vector3(-70f, 0f, -20f), new Vector3(0.14f, 0.14f, 0.6f), p.Gold),
            };
            Part(meshes.Frustum, "Visor", visual, new Vector3(0f, 1.73f, 0.21f), Vector3.zero, new Vector3(0.3f, 0.07f, 0.06f), p.HunterVisor);

            Transform swordPivot = new GameObject("SwordPivot").transform;
            swordPivot.SetParent(visual, false);
            swordPivot.localPosition = new Vector3(0.48f, 1.1f, 0.15f);
            swordPivot.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            renderers.Add(Part(meshes.Frustum, "ArmR", swordPivot, new Vector3(0f, 0f, -0.22f), Vector3.zero, new Vector3(0.15f, 0.15f, 0.4f), p.HunterArmor));
            Part(meshes.Frustum, "Grip", swordPivot, new Vector3(0f, 0f, -0.02f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.28f), p.HunterCloak);
            Part(meshes.Frustum, "Guard", swordPivot, new Vector3(0f, 0f, 0.13f), Vector3.zero, new Vector3(0.38f, 0.07f, 0.07f), p.Gold);
            Part(meshes.Blade, "Blade", swordPivot, new Vector3(0f, 0f, 0.16f), Vector3.zero, new Vector3(0.14f, 0.3f, 1.55f), p.Blade);

            Transform bladeTip = new GameObject("BladeTrail").transform;
            bladeTip.SetParent(swordPivot, false);
            bladeTip.localPosition = new Vector3(0f, 0f, 1.35f);
            var trail = bladeTip.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = p.SlashTrail;
            trail.time = 0.14f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = 0.5f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] { new GradientColorKey(Hex("#FFE7C2"), 0f), new GradientColorKey(Hex("#FF8A3D"), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailGradient;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            Transform arrowSpawn = new GameObject("ArrowSpawn").transform;
            arrowSpawn.SetParent(root.transform, false);
            arrowSpawn.localPosition = new Vector3(0.2f, 1.35f, 0.75f);

            var flash = root.AddComponent<HitFlash>();
            flash.SetRenderers(renderers.ToArray());

            var hunter = root.AddComponent<HunterController>();
            Set(hunter, "visualRoot", visual);
            Set(hunter, "hitFlash", flash);

            combat = root.AddComponent<HunterCombat>();
            Set(combat, "swordPivot", swordPivot);
            Set(combat, "arrowSpawn", arrowSpawn);
            Set(combat, "arrowMaterial", p.Arrow);
            Set(combat, "swordTrail", trail);
            Set(combat, "arrowTrailMaterial", p.ArrowTrail);

            input = root.AddComponent<HunterInput>();
            return hunter;
        }

        // ================================================================== Naga
        private static DragonBoss CreateDragon(Palette p, HunterController hunter)
        {
            var root = new GameObject("Emberscale_Dragon");
            root.transform.SetPositionAndRotation(new Vector3(0f, 0f, 6f), Quaternion.Euler(0f, 180f, 0f));
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true; // collider bergerak tanpa simulasi fisika
            body.useGravity = false;

            var dragon = root.AddComponent<DragonBoss>();
            dragon.UseDefaultAttacks();
            dragon.SetHunter(hunter);

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var flashTargets = new List<Renderer>();

            // Tubuh
            Renderer torso = Part(meshes.Orb, "Torso", visual, new Vector3(0f, 2.0f, 0f), Vector3.zero, new Vector3(2.7f, 2.1f, 4.7f), p.DragonScale);
            Hurtbox(torso.gameObject, dragon, 1f, "Body", new Vector3(0.9f, 0.9f, 0.9f));
            flashTargets.Add(torso);
            flashTargets.Add(Part(meshes.Orb, "Belly", visual, new Vector3(0f, 1.6f, 0.2f), Vector3.zero, new Vector3(2.0f, 1.4f, 3.7f), p.DragonBelly));
            flashTargets.Add(Part(meshes.Orb, "Chest", visual, new Vector3(0f, 2.2f, 1.7f), new Vector3(-15f, 0f, 0f), new Vector3(2.0f, 1.9f, 1.8f), p.DragonScale));

            for (int i = 0; i < 7; i++)
            {
                float s = 0.55f - i * 0.05f;
                flashTargets.Add(Part(meshes.Spike, "Spine_" + i, visual, new Vector3(0f, 3.0f - i * 0.06f, 1.6f - i * 0.62f),
                                      new Vector3(-10f, 0f, 0f), new Vector3(s * 0.6f, s, s * 0.9f), p.DragonHorn));
            }

            // Leher + kepala
            flashTargets.Add(Part(meshes.Frustum, "Neck", visual, new Vector3(0f, 2.95f, 2.45f), new Vector3(-38f, 0f, 0f), new Vector3(1.0f, 1.0f, 2.0f), p.DragonScale));
            flashTargets.Add(Part(meshes.Spike, "NeckSpike", visual, new Vector3(0f, 3.55f, 2.6f), new Vector3(-30f, 0f, 0f), new Vector3(0.3f, 0.45f, 0.4f), p.DragonHorn));

            Transform head = new GameObject("Head").transform;
            head.SetParent(visual, false);
            head.localPosition = new Vector3(0f, 3.5f, 3.35f);
            var headBox = head.gameObject.AddComponent<BoxCollider>();
            headBox.center = new Vector3(0f, -0.1f, 0.5f);
            headBox.size = new Vector3(1.3f, 1.2f, 2.7f);
            head.gameObject.AddComponent<DragonHurtbox>().Configure(dragon, 1.3f, "Head");

            flashTargets.Add(Part(meshes.Frustum, "Skull", head, Vector3.zero, Vector3.zero, new Vector3(1.15f, 0.95f, 1.5f), p.DragonScale));
            flashTargets.Add(Part(meshes.Frustum, "Snout", head, new Vector3(0f, -0.08f, 1.15f), Vector3.zero, new Vector3(0.82f, 0.55f, 1.1f), p.DragonScale));
            flashTargets.Add(Part(meshes.Frustum, "Jaw", head, new Vector3(0f, -0.46f, 0.85f), new Vector3(10f, 0f, 0f), new Vector3(0.76f, 0.22f, 1.3f), p.DragonBelly));
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (0.22f + (i / 2) * 0.12f);
                Part(meshes.Cone, "Fang_" + i, head, new Vector3(x, -0.33f, 1.45f - (i / 2) * 0.3f), new Vector3(90f, 0f, 0f), new Vector3(0.08f, 0.08f, 0.2f), p.DragonHorn);
            }
            // Tanduk menyapu ke belakang-atas (+ tanduk kecil di bawahnya)
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                flashTargets.Add(Part(meshes.Cone, "Horn" + s, head, new Vector3(0.36f * side, 0.38f, -0.35f),
                                      new Vector3(-150f, -18f * side, 0f), new Vector3(0.28f, 0.28f, 1.35f), p.DragonHorn));
                flashTargets.Add(Part(meshes.Cone, "HornSmall" + s, head, new Vector3(0.5f * side, 0.05f, -0.5f),
                                      new Vector3(-170f, -35f * side, 0f), new Vector3(0.16f, 0.16f, 0.6f), p.DragonHorn));
            }
            Part(meshes.Frustum, "EyeL", head, new Vector3(-0.44f, 0.15f, 0.58f), new Vector3(0f, -12f, 0f), new Vector3(0.1f, 0.1f, 0.26f), p.DragonEye);
            Part(meshes.Frustum, "EyeR", head, new Vector3(0.44f, 0.15f, 0.58f), new Vector3(0f, 12f, 0f), new Vector3(0.1f, 0.1f, 0.26f), p.DragonEye);

            ParticleSystem fire = CreateFireParticles(head, p.Fire);

            // Ekor meruncing dengan duri
            Vector3[] tailPos = { new Vector3(0f, 1.9f, -2.7f), new Vector3(0f, 1.6f, -4.1f), new Vector3(0f, 1.3f, -5.4f), new Vector3(0f, 1.0f, -6.6f) };
            Vector3[] tailSize = { new Vector3(1.1f, 1.0f, 1.7f), new Vector3(0.85f, 0.75f, 1.6f), new Vector3(0.62f, 0.55f, 1.5f), new Vector3(0.42f, 0.38f, 1.3f) };
            for (int i = 0; i < tailPos.Length; i++)
            {
                Renderer segment = Part(meshes.Frustum, "Tail_" + i, visual, tailPos[i], new Vector3(-8f, 0f, 0f), tailSize[i], i % 2 == 0 ? p.DragonScale : p.DragonDark);
                Hurtbox(segment.gameObject, dragon, 0.8f, "Tail", Vector3.one);
                flashTargets.Add(segment);
                flashTargets.Add(Part(meshes.Spike, "TailSpike_" + i, visual, tailPos[i] + new Vector3(0f, tailSize[i].y * 0.45f, 0f),
                                      new Vector3(-20f, 0f, 0f), new Vector3(0.25f, 0.4f, 0.4f) * (1f - i * 0.15f), p.DragonHorn));
            }
            flashTargets.Add(Part(meshes.Spike, "TailBladeL", visual, new Vector3(-0.1f, 0.95f, -7.4f), new Vector3(0f, -30f, 90f), new Vector3(0.7f, 0.7f, 0.12f), p.DragonHorn));
            flashTargets.Add(Part(meshes.Spike, "TailBladeR", visual, new Vector3(0.1f, 0.95f, -7.4f), new Vector3(0f, 30f, -90f), new Vector3(0.7f, 0.7f, 0.12f), p.DragonHorn));

            // Kaki (hurtbox rendah agar serangan pedang di tanah tetap kena) + cakar
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -1.05f : 1.05f;
                float z = i < 2 ? 1.3f : -1.2f;
                Renderer thigh = Part(meshes.Orb, "Thigh_" + i, visual, new Vector3(x * 1.05f, 1.45f, z), Vector3.zero, new Vector3(0.95f, 1.1f, 1.2f), p.DragonDark);
                flashTargets.Add(thigh);
                Renderer leg = Part(meshes.Frustum, "Leg_" + i, visual, new Vector3(x, 0.7f, z + 0.05f), new Vector3(-90f, 0f, 0f), new Vector3(0.6f, 0.6f, 1.4f), p.DragonDark);
                Hurtbox(leg.gameObject, dragon, 1f, "Leg", Vector3.one);
                flashTargets.Add(leg);
                for (int c = -1; c <= 1; c++)
                {
                    flashTargets.Add(Part(meshes.Cone, "Claw_" + i + "_" + (c + 1), visual, new Vector3(x + c * 0.18f, 0.1f, z + 0.38f),
                                          new Vector3(15f, c * 15f, 0f), new Vector3(0.16f, 0.12f, 0.42f), p.DragonHorn));
                }
            }

            // Sayap
            Transform wingL = CreateWing("WingL", visual, new Vector3(-1.1f, 2.85f, 0.5f), -1f, p, flashTargets);
            Transform wingR = CreateWing("WingR", visual, new Vector3(1.1f, 2.85f, 0.5f), 1f, p, flashTargets);

            Transform aim = new GameObject("AimPoint").transform;
            aim.SetParent(root.transform, false);
            aim.localPosition = new Vector3(0f, 2.3f, 0.6f);

            var flash = root.AddComponent<HitFlash>();
            flash.SetRenderers(flashTargets.ToArray());

            Set(dragon, "visualRoot", visual);
            Set(dragon, "leftWing", wingL);
            Set(dragon, "rightWing", wingR);
            Set(dragon, "aimPoint", aim);
            Set(dragon, "fireParticles", fire);
            Set(dragon, "hitFlash", flash);
            Set(dragon, "hunter", hunter);
            EditorUtility.SetDirty(dragon);
            return dragon;
        }

        private static Transform CreateWing(string name, Transform parent, Vector3 pos, float side, Palette p, List<Renderer> flashTargets)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pos;

            // Mesh selaput mengarah ke +X; sayap kiri dicerminkan lewat skala X negatif.
            flashTargets.Add(Part(meshes.WingMembrane, "Membrane", pivot, Vector3.zero, Vector3.zero, new Vector3(5.2f * side, 1f, 3.6f), p.DragonWing));
            flashTargets.Add(Part(meshes.Frustum, "ArmBone", pivot, new Vector3(1.2f * side, 0.05f, 1.15f), new Vector3(0f, -90f * side, 0f), new Vector3(0.22f, 0.22f, 2.5f), p.DragonDark));
            flashTargets.Add(Part(meshes.Frustum, "HandBone", pivot, new Vector3(3.7f * side, 0.05f, 1.3f), new Vector3(0f, -95f * side, 0f), new Vector3(0.16f, 0.16f, 2.7f), p.DragonDark));
            flashTargets.Add(Part(meshes.Cone, "Talon", pivot, new Vector3(2.4f * side, 0.1f, 1.3f), new Vector3(-90f, 0f, 0f), new Vector3(0.22f, 0.22f, 0.45f), p.DragonHorn));
            return pivot;
        }

        private static ParticleSystem CreateFireParticles(Transform head, Material material)
        {
            var go = new GameObject("FireBreath");
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, -0.15f, 1.7f);
            go.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 19f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("#FFD27A"), Hex("#FF6A2A"));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.gravityModifier = -0.1f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 180f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.2f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Hex("#FFE3A0"), 0f), new GradientColorKey(Hex("#FF7A2E"), 0.4f), new GradientColorKey(Hex("#7A1E0E"), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void Hurtbox(GameObject go, DragonBoss dragon, float multiplier, string part, Vector3 size)
        {
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.size = size;
            go.AddComponent<DragonHurtbox>().Configure(dragon, multiplier, part);
        }

        private static AttackTelegraph CreateTelegraph(Palette p)
        {
            var go = new GameObject("AttackTelegraph");
            var areaFilter = go.AddComponent<MeshFilter>();
            var areaRenderer = go.AddComponent<MeshRenderer>();
            ConfigureFlatRenderer(areaRenderer, p.TelegraphWarn);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(go.transform, false);
            fill.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            var fillFilter = fill.AddComponent<MeshFilter>();
            var fillRenderer = fill.AddComponent<MeshRenderer>();
            ConfigureFlatRenderer(fillRenderer, p.TelegraphActive);

            var telegraph = go.AddComponent<AttackTelegraph>();
            Set(telegraph, "areaFilter", areaFilter);
            Set(telegraph, "areaRenderer", areaRenderer);
            Set(telegraph, "fillFilter", fillFilter);
            Set(telegraph, "fillRenderer", fillRenderer);
            Set(telegraph, "warningMaterial", p.TelegraphWarn);
            Set(telegraph, "activeMaterial", p.TelegraphActive);
            return telegraph;
        }

        private static void ConfigureFlatRenderer(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
        }

        // ==================================================================== UI
        private static void BuildUi(DragonHunterManager manager, out DragonHunterHUD hud)
        {
            RectTransform canvas = CreateCanvas();
            hud = canvas.gameObject.AddComponent<DragonHunterHUD>();

            // Kiri atas: status Hunter
            Text hunterLabel = CreateText(canvas, "HunterLabel", "HUNTER", 22, TextMuted, TextAnchor.MiddleLeft);
            Place(hunterLabel.rectTransform, TopLeft, new Vector2(48f, -30f), new Vector2(300f, 30f));
            UIBar hpBar = CreateBar(canvas, "HunterHP", TopLeft, new Vector2(48f, -62f), new Vector2(520f, 24f), Hex("#C8442B"), Hex("#E8C9A0"));
            UIBar staminaBar = CreateBar(canvas, "HunterStamina", TopLeft, new Vector2(48f, -94f), new Vector2(440f, 12f), AccentSoft, Hex("#7A5A26"));
            Text potion = CreateText(canvas, "Potions", "Potion x3  [R]", 24, TextColor, TextAnchor.MiddleLeft);
            Place(potion.rectTransform, TopLeft, new Vector2(48f, -118f), new Vector2(400f, 34f));

            // Tengah bawah: naga (boss bar). Sprint 3: dipindah dari tengah atas karena bertumpuk
            // dengan blok Hunter di semua resolusi (terdeteksi UiLayoutTests).
            Image bossBacking = CreateImage(canvas, "BossBarBacking", new Color(0.06f, 0.04f, 0.03f, 0.62f));
            Place(bossBacking.rectTransform, BottomCenter, new Vector2(0f, 12f), new Vector2(900f, 128f));
            Text dragonName = CreateText(canvas, "DragonName", "EMBERSCALE WYVERN", 30, TextColor, TextAnchor.MiddleCenter);
            Place(dragonName.rectTransform, BottomCenter, new Vector2(0f, 90f), new Vector2(820f, 40f));
            UIBar dragonBar = CreateBar(canvas, "DragonHP", BottomCenter, new Vector2(0f, 64f), new Vector2(820f, 20f), Accent, Hex("#F0E6DF"));
            Text state = CreateText(canvas, "DragonState", "Stalking", 24, AccentSoft, TextAnchor.MiddleCenter);
            Place(state.rectTransform, BottomCenter, new Vector2(0f, 24f), new Vector2(820f, 34f));

            // Kanan atas: timer dan mode kamera
            Text timer = CreateText(canvas, "Timer", "00:00", 36, TextColor, TextAnchor.MiddleRight);
            Place(timer.rectTransform, TopRight, new Vector2(-48f, -34f), new Vector2(300f, 46f));
            Text lockOn = CreateText(canvas, "LockOn", "Free cam  [Tab]", 22, TextMuted, TextAnchor.MiddleRight);
            Place(lockOn.rectTransform, TopRight, new Vector2(-48f, -78f), new Vector2(400f, 30f));

            // Tengah: pesan dan titik bidik
            Text message = CreateText(canvas, "Message", "", 54, AccentSoft, TextAnchor.MiddleCenter);
            Place(message.rectTransform, Center, new Vector2(0f, 190f), new Vector2(1400f, 80f));
            message.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);

            // Kiri atas di bawah status Hunter: panduan kontrol (bawah layar dipakai boss bar).
            Text hint = CreateText(canvas, "ControlsHint",
                "WASD Move   Shift Sprint   Space Dodge\nLMB Combo   RMB Heavy   E Arrow   R Potion\nTab Lock-on   Esc Pause",
                18, TextMuted, TextAnchor.UpperLeft);
            Place(hint.rectTransform, TopLeft, new Vector2(48f, -164f), new Vector2(620f, 80f));

            Set(hud, "hunterHealthBar", hpBar);
            Set(hud, "hunterStaminaBar", staminaBar);
            Set(hud, "dragonHealthBar", dragonBar);
            Set(hud, "dragonStateText", state);
            Set(hud, "potionText", potion);
            Set(hud, "timerText", timer);
            Set(hud, "messageText", message);
            Set(hud, "lockOnText", lockOn);

            // Panel
            GameObject pause = CreatePanel(canvas, "PausePanel", "PAUSED", null, out Transform pauseButtons);
            Button resume = CreateButton(pauseButtons, "Resume", "Resume", 0);
            UnityEventTools.AddPersistentListener(resume.onClick, manager.Resume);
            Button pauseRetry = CreateButton(pauseButtons, "Retry", "Restart Quest", 1);
            UnityEventTools.AddPersistentListener(pauseRetry.onClick, manager.Retry);
            AddBackToHub(pauseButtons, 2);

            GameObject victory = CreatePanel(canvas, "VictoryPanel", "QUEST CLEAR", Accent, out Transform victoryButtons, out Text victoryDetail);
            Button victoryRetry = CreateButton(victoryButtons, "Retry", "Hunt Again", 0);
            UnityEventTools.AddPersistentListener(victoryRetry.onClick, manager.Retry);
            AddBackToHub(victoryButtons, 1);

            GameObject defeat = CreatePanel(canvas, "DefeatPanel", "QUEST FAILED", Hex("#9C3A2A"), out Transform defeatButtons, out Text defeatDetail);
            Button defeatRetry = CreateButton(defeatButtons, "Retry", "Try Again", 0);
            UnityEventTools.AddPersistentListener(defeatRetry.onClick, manager.Retry);
            AddBackToHub(defeatButtons, 1);

            pause.SetActive(false);
            victory.SetActive(false);
            defeat.SetActive(false);

            Set(manager, "pausePanel", pause);
            Set(manager, "victoryPanel", victory);
            Set(manager, "defeatPanel", defeat);
            Set(manager, "victoryDetailText", victoryDetail);
            Set(manager, "defeatDetailText", defeatDetail);
        }

        private static void AddBackToHub(Transform parent, int index)
        {
            Button back = CreateButton(parent, "BackToHub", "Back to Hub", index);
            var ret = back.gameObject.AddComponent<ReturnToHub>();
            UnityEventTools.AddPersistentListener(back.onClick, ret.ReturnToMainMenu);
        }

        private static GameObject CreatePanel(RectTransform canvas, string name, string title, Color? titleColor, out Transform buttons)
        {
            return CreatePanel(canvas, name, title, titleColor, out buttons, out _);
        }

        private static GameObject CreatePanel(RectTransform canvas, string name, string title, Color? titleColor,
                                              out Transform buttons, out Text detail)
        {
            Image overlay = CreateImage(canvas, name, new Color(0.04f, 0.03f, 0.02f, 0.72f));
            overlay.raycastTarget = true;
            RectTransform ort = overlay.rectTransform;
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero;
            ort.offsetMax = Vector2.zero;

            Image box = CreateImage(ort, "Box", Surface);
            Place(box.rectTransform, Center, Vector2.zero, new Vector2(680f, 560f));
            Image bar = CreateImage(box.rectTransform, "AccentBar", titleColor ?? Accent);
            Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(680f, 12f));

            Text titleText = CreateText(box.rectTransform, "Title", title, 56, titleColor ?? TextColor, TextAnchor.MiddleCenter);
            Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(640f, 80f));

            detail = CreateText(box.rectTransform, "Detail", "", 28, TextMuted, TextAnchor.MiddleCenter);
            Place(detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(640f, 90f));

            var buttonRoot = new GameObject("Buttons", typeof(RectTransform));
            buttonRoot.transform.SetParent(box.transform, false);
            Place((RectTransform)buttonRoot.transform, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(460f, 300f));
            buttons = buttonRoot.transform;
            return overlay.gameObject;
        }

        private static UIBar CreateBar(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fillColor, Color trailColor)
        {
            Image background = CreateImage(parent, name, Surface);
            Place(background.rectTransform, anchor, pos, size);

            Image trail = CreateImage(background.rectTransform, "Trail", trailColor);
            Stretch(trail.rectTransform, 2f);
            Image fill = CreateImage(background.rectTransform, "Fill", fillColor);
            Stretch(fill.rectTransform, 2f);

            var bar = background.gameObject.AddComponent<UIBar>();
            bar.Configure(fill.rectTransform, trail.rectTransform);
            Set(bar, "fill", fill.rectTransform);
            Set(bar, "trail", trail.rectTransform);
            return bar;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 0.5f);
            // Inset hanya vertikal: lebar diatur UIBar lewat anchorMax.x, jadi tidak pernah negatif.
            rt.offsetMin = new Vector2(0f, inset);
            rt.offsetMax = new Vector2(0f, -inset);
        }

        // ================================================================ Helper
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static Renderer Part(Mesh mesh, string name, Transform parent, Vector3 localPos, Vector3 localEuler,
                                     Vector3 scale, Material material, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return renderer;
        }

        private static Renderer Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localEuler,
                                          Vector3 scale, Material material, bool collider = false, bool castShadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = scale;

            Collider c = go.GetComponent<Collider>();
            if (!collider && c != null) Object.DestroyImmediate(c);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return renderer;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) throw new InvalidOperationException($"Field '{field}' tidak ada di {target.GetType().Name}.");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static RectTransform CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = null; // kotak datar, tanpa sprite UI bawaan Unity
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text CreateText(Transform parent, string name, string content, int size, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, int index)
        {
            // Image putih: transisi ColorTint MENGALIKAN warna Image, jadi warna asli diatur lewat ColorBlock.
            // (Sprint 3: sebelumnya SurfaceHover x SurfaceHover membuat tombol nyaris hitam.)
            Image img = CreateImage(parent, name, Color.white);
            img.raycastTarget = true;
            Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -index * 92f), new Vector2(460f, 76f));

            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            ColorBlock cb = button.colors;
            cb.normalColor = Hex("#4A3226");
            cb.highlightedColor = Hex("#6B4532");
            cb.selectedColor = Hex("#4A3226");
            cb.pressedColor = Accent;
            cb.disabledColor = new Color(SurfaceHover.r, SurfaceHover.g, SurfaceHover.b, 0.4f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            button.colors = cb;

            if (clickAudio != null) UnityEventTools.AddPersistentListener(button.onClick, clickAudio.PlayUiClick);

            Text text = CreateText(img.transform, "Label", label, 32, TextColor, TextAnchor.MiddleCenter);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return button;
        }
    }
}
