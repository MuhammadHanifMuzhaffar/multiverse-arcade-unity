using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Membaca Input Manager lama (standar proyek) lalu meneruskannya ke Hunter, combat, kamera.
    /// WASD gerak | Shift sprint | Space dodge | LMB combo | RMB serangan berat |
    /// E panah | R potion | Tab / MMB lock-on | Esc pause (ditangani manager).
    /// </summary>
    public class HunterInput : MonoBehaviour
    {
        [SerializeField] private HunterController hunter;
        [SerializeField] private HunterCombat combat;
        [SerializeField] private ThirdPersonCamera cameraRig;
        [SerializeField] private DragonHunterManager manager;

        [Header("Tombol")]
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode dodgeKey = KeyCode.Space;
        [SerializeField] private KeyCode arrowKey = KeyCode.E;
        [SerializeField] private KeyCode potionKey = KeyCode.R;
        [SerializeField] private KeyCode lockOnKey = KeyCode.Tab;

        private void Update()
        {
            bool canPlay = manager == null || manager.AcceptsGameplayInput;

            if (!canPlay)
            {
                if (hunter != null)
                {
                    hunter.SetMoveInput(Vector2.zero);
                    hunter.SetSprint(false);
                }
                if (cameraRig != null) cameraRig.SetLookInput(Vector2.zero);
                return;
            }

            if (cameraRig != null)
            {
                cameraRig.SetLookInput(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")));
                if (Input.GetKeyDown(lockOnKey) || Input.GetMouseButtonDown(2)) cameraRig.ToggleLockOn();
            }

            if (hunter == null) return;

            hunter.SetMoveInput(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
            hunter.SetSprint(Input.GetKey(sprintKey));

            if (Input.GetKeyDown(dodgeKey))
            {
                // Dodge boleh membatalkan fase recovery serangan (dodge-cancel), tidak saat windup/aktif.
                bool combatAllows = combat == null || !combat.IsAttacking || combat.InRecovery;
                if (combatAllows && hunter.TryDodge() && combat != null) combat.CancelAttack();
            }

            if (Input.GetKeyDown(potionKey) && (combat == null || !combat.IsAttacking)) hunter.TryDrinkPotion();

            if (combat == null) return;
            if (Input.GetMouseButtonDown(0)) combat.TryLightAttack();
            if (Input.GetMouseButtonDown(1)) combat.TryHeavyAttack();
            if (Input.GetKeyDown(arrowKey)) combat.TryShootArrow();
        }
    }
}
