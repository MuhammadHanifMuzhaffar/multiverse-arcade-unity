using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Semua audio Dragon Hunter: musik battle dua lapis (normal / Enraged) yang diputar sinkron
    /// lalu di-crossfade, SFX Hunter (2D), dan suara naga (3D di posisi naga).
    /// Hanya mendengarkan event gameplay sehingga logika permainan tidak bergantung pada audio.
    /// </summary>
    public class DragonHunterAudio : MonoBehaviour
    {
        [System.Serializable]
        public class ClipSet
        {
            [Header("Hunter")]
            public AudioClip swordSwing1;
            public AudioClip swordSwing2;
            public AudioClip swordHit;
            public AudioClip heavyHit;
            public AudioClip arrowShoot;
            public AudioClip arrowHit;
            public AudioClip dodge;
            public AudioClip hunterHurt;
            public AudioClip potion;

            [Header("Naga")]
            public AudioClip dragonGrowl;
            public AudioClip dragonRoar;
            public AudioClip dragonHurt;
            public AudioClip dragonDeath;
            public AudioClip biteSnap;
            public AudioClip tailWhoosh;
            public AudioClip fireBreath;
            public AudioClip chargeStomp;
            public AudioClip novaBlast;

            [Header("Musik & UI")]
            public AudioClip musicBattle;
            public AudioClip musicEnraged;
            public AudioClip victory;
            public AudioClip defeat;
            public AudioClip uiClick;
        }

        [SerializeField] private ClipSet clips = new ClipSet();

        [Header("Referensi")]
        [SerializeField] private DragonHunterManager manager;
        [SerializeField] private HunterController hunter;
        [SerializeField] private HunterCombat combat;
        [SerializeField] private DragonBoss dragon;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;
        [SerializeField] private float musicCrossfadeSeconds = 2.5f;
        [SerializeField] private float dragonHurtCooldown = 0.9f;

        private AudioSource musicA;
        private AudioSource musicB;
        private AudioSource hunterSource;
        private AudioSource uiSource;
        private AudioSource dragonVoice;
        private AudioSource dragonSfx;
        private AudioSource fireLoop;

        private float musicBlend;       // 0 = normal, 1 = enraged
        private float musicBlendTarget;
        private float musicMaster = 1f;
        private float musicMasterTarget = 1f;
        private float nextDragonHurtTime;
        private float chargeStompEndTime = -1f;
        private float nextStompTime;
        private bool bound;

        public ClipSet Clips => clips;
        public AudioSource MusicNormal => musicA;
        public AudioSource MusicEnraged => musicB;
        public float MusicBlend => musicBlend;

        private void Awake()
        {
            musicA = CreateSource("Music_Battle", transform, 0f, loop: true);
            musicB = CreateSource("Music_Enraged", transform, 0f, loop: true);
            hunterSource = CreateSource("Hunter_Sfx", transform, 0f);
            uiSource = CreateSource("UI_Sfx", transform, 0f);
            uiSource.ignoreListenerPause = true; // klik tombol tetap terdengar saat pause

            Transform dragonRoot = dragon != null ? dragon.transform : transform;
            dragonVoice = CreateSource("Dragon_Voice", dragonRoot, 0.6f);
            dragonSfx = CreateSource("Dragon_Sfx", dragonRoot, 0.6f);
            fireLoop = CreateSource("Dragon_FireLoop", dragonRoot, 0.6f, loop: true);
        }

        private void Start()
        {
            Bind();

            musicA.clip = clips.musicBattle;
            musicB.clip = clips.musicEnraged;
            ApplyMusicVolumes();
            if (musicA.clip != null) musicA.Play();
            if (musicB.clip != null) musicB.Play(); // sinkron dengan lapis normal, volume awal 0
        }

        private void OnDestroy() => Unbind();

        private static AudioSource CreateSource(string name, Transform parent, float spatialBlend, bool loop = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 8f;
            source.maxDistance = 60f;
            source.dopplerLevel = 0f;
            return source;
        }

        // ------------------------------------------------------------------ Event
        private void Bind()
        {
            if (bound) return;
            bound = true;

            if (manager != null) manager.PhaseChanged += HandlePhase;
            if (hunter != null)
            {
                hunter.Damaged += HandleHunterDamaged;
                hunter.DodgeStarted += HandleDodge;
                hunter.DrinkStarted += HandleDrink;
            }
            if (combat != null)
            {
                combat.SwingStarted += HandleSwing;
                combat.HitLanded += HandleHit;
                combat.ArrowShot += HandleArrowShot;
                combat.ArrowLanded += HandleArrowLanded;
            }
            if (dragon != null)
            {
                dragon.AttackTelegraphed += HandleTelegraph;
                dragon.AttackActivated += HandleAttack;
                dragon.StateChanged += HandleDragonState;
                dragon.Damaged += HandleDragonDamaged;
                dragon.Staggered += HandleStagger;
                dragon.EnrageStarted += HandleEnrage;
                dragon.Died += HandleDragonDied;
            }
        }

        private void Unbind()
        {
            if (!bound) return;
            bound = false;

            if (manager != null) manager.PhaseChanged -= HandlePhase;
            if (hunter != null)
            {
                hunter.Damaged -= HandleHunterDamaged;
                hunter.DodgeStarted -= HandleDodge;
                hunter.DrinkStarted -= HandleDrink;
            }
            if (combat != null)
            {
                combat.SwingStarted -= HandleSwing;
                combat.HitLanded -= HandleHit;
                combat.ArrowShot -= HandleArrowShot;
                combat.ArrowLanded -= HandleArrowLanded;
            }
            if (dragon != null)
            {
                dragon.AttackTelegraphed -= HandleTelegraph;
                dragon.AttackActivated -= HandleAttack;
                dragon.StateChanged -= HandleDragonState;
                dragon.Damaged -= HandleDragonDamaged;
                dragon.Staggered -= HandleStagger;
                dragon.EnrageStarted -= HandleEnrage;
                dragon.Died -= HandleDragonDied;
            }
        }

        private void HandleSwing(bool heavy, int comboIndex)
        {
            Play(hunterSource, comboIndex % 2 == 0 ? clips.swordSwing1 : clips.swordSwing2, heavy ? 1f : 0.8f, heavy ? 0.85f : Random.Range(0.95f, 1.08f));
        }

        private void HandleHit(float damage, Vector3 point, bool heavy)
        {
            Play(hunterSource, heavy ? clips.heavyHit : clips.swordHit, 1f, Random.Range(0.93f, 1.07f));
        }

        private void HandleArrowShot() => Play(hunterSource, clips.arrowShoot, 0.8f, Random.Range(0.96f, 1.05f));
        private void HandleArrowLanded(Vector3 point) => PlayAt(dragonSfx, clips.arrowHit, 0.9f, Random.Range(0.9f, 1.1f));
        private void HandleDodge() => Play(hunterSource, clips.dodge, 0.8f, Random.Range(0.95f, 1.05f));
        private void HandleDrink() => Play(hunterSource, clips.potion, 0.8f, 1f);
        private void HandleHunterDamaged(float damage) => Play(hunterSource, clips.hunterHurt, 1f, Random.Range(0.92f, 1.04f));

        private void HandleTelegraph(DragonAttackDefinition attack)
        {
            float pitch = attack.id == "nova" ? 0.8f : attack.id == "fire" ? 0.9f : 1.05f;
            PlayAt(dragonVoice, clips.dragonGrowl, 0.85f, pitch * (dragon != null && dragon.IsEnraged ? 1.08f : 1f));
        }

        private void HandleAttack(DragonAttackDefinition attack)
        {
            switch (attack.id)
            {
                case "bite":
                    PlayAt(dragonSfx, clips.biteSnap, 1f, Random.Range(0.95f, 1.05f));
                    break;
                case "tail":
                    PlayAt(dragonSfx, clips.tailWhoosh, 1f, 0.95f);
                    break;
                case "fire":
                    if (fireLoop != null && clips.fireBreath != null)
                    {
                        fireLoop.clip = clips.fireBreath;
                        fireLoop.volume = sfxVolume;
                        fireLoop.Play();
                    }
                    break;
                case "charge":
                    chargeStompEndTime = Time.time + attack.active;
                    nextStompTime = Time.time;
                    break;
                case "nova":
                    PlayAt(dragonSfx, clips.novaBlast, 1f, 1f);
                    break;
            }
        }

        private void HandleDragonState(DragonState state)
        {
            if (state != DragonState.Attacking && fireLoop != null && fireLoop.isPlaying) fireLoop.Stop();
        }

        private void HandleDragonDamaged(float damage, bool vulnerable)
        {
            if (Time.time < nextDragonHurtTime) return;
            if (damage < 25f && !vulnerable) return; // pukulan kecil tidak selalu memicu erangan
            nextDragonHurtTime = Time.time + dragonHurtCooldown;
            PlayAt(dragonVoice, clips.dragonHurt, 0.8f, Random.Range(0.92f, 1.08f));
        }

        private void HandleStagger() => PlayAt(dragonVoice, clips.dragonHurt, 1f, 0.8f);

        private void HandleEnrage()
        {
            PlayAt(dragonVoice, clips.dragonRoar, 1f, 1f);
            musicBlendTarget = 1f;
        }

        private void HandleDragonDied()
        {
            if (fireLoop != null) fireLoop.Stop();
            PlayAt(dragonVoice, clips.dragonDeath, 1f, 1f);
        }

        private void HandlePhase(DragonHunterManager.BattlePhase phase)
        {
            switch (phase)
            {
                case DragonHunterManager.BattlePhase.Victory:
                    musicMasterTarget = 0f;
                    Play(uiSource, clips.victory, 0.9f, 1f, delay: 1.2f);
                    break;
                case DragonHunterManager.BattlePhase.Defeat:
                    musicMasterTarget = 0f;
                    Play(uiSource, clips.defeat, 0.9f, 1f, delay: 0.6f);
                    break;
            }
        }

        /// <summary>Dipasang ke tombol UI oleh scene builder.</summary>
        public void PlayUiClick() => Play(uiSource, clips.uiClick, 0.7f, 1f);

        // ------------------------------------------------------------------ Loop
        private void Update()
        {
            float dt = Time.deltaTime;
            if (musicCrossfadeSeconds > 0f)
                musicBlend = Mathf.MoveTowards(musicBlend, musicBlendTarget, dt / musicCrossfadeSeconds);
            musicMaster = Mathf.MoveTowards(musicMaster, musicMasterTarget, dt / 1.5f);
            ApplyMusicVolumes();

            if (Time.time < chargeStompEndTime && Time.time >= nextStompTime)
            {
                nextStompTime = Time.time + 0.2f;
                PlayAt(dragonSfx, clips.chargeStomp, 0.9f, Random.Range(0.9f, 1.05f));
            }
        }

        private void ApplyMusicVolumes()
        {
            if (musicA == null || musicB == null) return;
            // Crossfade daya-konstan agar tidak ada lubang volume di tengah transisi.
            musicA.volume = musicVolume * musicMaster * Mathf.Cos(musicBlend * Mathf.PI * 0.5f);
            musicB.volume = musicVolume * musicMaster * Mathf.Sin(musicBlend * Mathf.PI * 0.5f);
        }

        private void Play(AudioSource source, AudioClip clip, float volume, float pitch, float delay = 0f)
        {
            if (source == null || clip == null) return;
            if (delay > 0f)
            {
                source.clip = clip;
                source.pitch = pitch;
                source.volume = volume * sfxVolume;
                source.PlayDelayed(delay);
                return;
            }
            source.pitch = pitch;
            source.PlayOneShot(clip, volume * sfxVolume);
        }

        private void PlayAt(AudioSource source, AudioClip clip, float volume, float pitch) => Play(source, clip, volume, pitch);
    }
}
