using UnityEngine;
using UnityEngine.UI;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// HUD pertarungan. Teks hanya diperbarui saat nilainya berubah (tanpa alokasi string tiap frame).
    /// </summary>
    public class DragonHunterHUD : MonoBehaviour
    {
        [Header("Bar")]
        [SerializeField] private UIBar hunterHealthBar;
        [SerializeField] private UIBar hunterStaminaBar;
        [SerializeField] private UIBar dragonHealthBar;

        [Header("Teks")]
        [SerializeField] private Text dragonStateText;
        [SerializeField] private Text potionText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text lockOnText;

        [Header("Pesan")]
        [SerializeField] private float messageFade = 0.5f;
        [SerializeField] private string openingMessage = "Hunt the Emberscale Wyvern!";

        private DragonHunterManager manager;
        private HunterController hunter;
        private DragonBoss dragon;
        private ThirdPersonCamera cameraRig;

        private int lastSeconds = -1;
        private int lastPotions = -1;
        private int lockOnState = -1;
        private float messageHideTime;
        private Color messageBaseColor = Color.white;

        public string CurrentMessage => messageText != null ? messageText.text : string.Empty;
        public string CurrentStateLabel => dragonStateText != null ? dragonStateText.text : string.Empty;

        public void Bind(DragonHunterManager owner, HunterController hunterRef, DragonBoss dragonRef, ThirdPersonCamera cam)
        {
            Unbind();
            manager = owner;
            hunter = hunterRef;
            dragon = dragonRef;
            cameraRig = cam;

            if (messageText != null) messageBaseColor = messageText.color;

            if (hunter != null)
            {
                hunter.Health.Changed += HandleHunterHealth;
                hunter.PotionUsed += HandlePotionUsed;
                SetBar(hunterHealthBar, hunter.Health.Fraction, true);
                SetBar(hunterStaminaBar, hunter.Stamina.Fraction, true);
            }

            if (dragon != null)
            {
                dragon.Health.Changed += HandleDragonHealth;
                dragon.StateChanged += HandleDragonState;
                dragon.AttackTelegraphed += HandleTelegraphed;
                dragon.Staggered += HandleStaggered;
                dragon.EnrageStarted += HandleEnrage;
                SetBar(dragonHealthBar, dragon.Health.Fraction, true);
                HandleDragonState(dragon.State);
            }

            ShowMessage(openingMessage, 2.5f);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (hunter != null)
            {
                hunter.Health.Changed -= HandleHunterHealth;
                hunter.PotionUsed -= HandlePotionUsed;
            }
            if (dragon != null)
            {
                dragon.Health.Changed -= HandleDragonHealth;
                dragon.StateChanged -= HandleDragonState;
                dragon.AttackTelegraphed -= HandleTelegraphed;
                dragon.Staggered -= HandleStaggered;
                dragon.EnrageStarted -= HandleEnrage;
            }
        }

        public void ShowMessage(string message, float duration)
        {
            if (messageText == null) return;
            messageText.text = message;
            messageText.color = messageBaseColor;
            messageHideTime = Time.unscaledTime + Mathf.Max(0.1f, duration);
        }

        private void Update()
        {
            if (hunter != null)
            {
                SetBar(hunterStaminaBar, hunter.Stamina.Fraction);
                if (potionText != null && hunter.PotionsLeft != lastPotions)
                {
                    lastPotions = hunter.PotionsLeft;
                    potionText.text = "Potion x" + lastPotions + "  [R]";
                }
            }

            if (manager != null && timerText != null)
            {
                int seconds = Mathf.FloorToInt(manager.ElapsedTime);
                if (seconds != lastSeconds)
                {
                    lastSeconds = seconds;
                    timerText.text = FormatTime(seconds);
                }
            }

            if (cameraRig != null && lockOnText != null)
            {
                int state = cameraRig.IsLockedOn ? 1 : 0;
                if (state != lockOnState)
                {
                    lockOnState = state;
                    lockOnText.text = state == 1 ? "LOCK-ON  [Tab]" : "Free cam  [Tab]";
                }
            }

            UpdateMessageFade();
        }

        private void UpdateMessageFade()
        {
            if (messageText == null || messageText.text.Length == 0) return;

            float remaining = messageHideTime - Time.unscaledTime;
            if (remaining <= 0f)
            {
                messageText.text = string.Empty;
                return;
            }
            if (remaining < messageFade)
            {
                Color c = messageBaseColor;
                c.a *= remaining / messageFade;
                messageText.color = c;
            }
        }

        public static string FormatTime(float totalSeconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(totalSeconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        // ------------------------------------------------------------------ Event
        private void HandleHunterHealth(float current, float max) => SetBar(hunterHealthBar, current / max);
        private void HandleDragonHealth(float current, float max) => SetBar(dragonHealthBar, current / max);

        private void HandlePotionUsed(float healed)
        {
            ShowMessage("Potion +" + Mathf.RoundToInt(healed) + " HP", 1.2f);
        }

        private void HandleTelegraphed(DragonAttackDefinition attack)
        {
            SetStateLabel((dragon.IsEnraged ? "[ENRAGED] " : string.Empty) + "Winding up: " + attack.displayName);
        }

        private void HandleDragonState(DragonState state)
        {
            string prefix = dragon != null && dragon.IsEnraged ? "[ENRAGED] " : string.Empty;
            switch (state)
            {
                case DragonState.Idle:
                    SetStateLabel(prefix + "Stalking");
                    break;
                case DragonState.TelegraphAttack:
                    if (dragon != null && dragon.CurrentAttack != null)
                        SetStateLabel(prefix + "Winding up: " + dragon.CurrentAttack.displayName);
                    break;
                case DragonState.Attacking:
                    SetStateLabel(prefix + (dragon != null && dragon.CurrentAttack != null ? dragon.CurrentAttack.displayName : "Attacking"));
                    break;
                case DragonState.Vulnerable:
                    SetStateLabel(prefix + "VULNERABLE  (x1.5 damage)");
                    break;
                case DragonState.Enraged:
                    SetStateLabel("ENRAGED!");
                    break;
            }
        }

        private void HandleStaggered() => ShowMessage("Dragon staggered! Strike now!", 2f);
        private void HandleEnrage() => ShowMessage("The Emberscale is ENRAGED!", 2.5f);

        private static void SetBar(UIBar bar, float value, bool instant = false)
        {
            if (bar != null) bar.SetValue(value, instant);
        }

        private void SetStateLabel(string label)
        {
            if (dragonStateText != null) dragonStateText.text = label;
        }
    }
}
