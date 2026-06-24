using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Vamsurlike.Network;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerNetworkController))]
    [RequireComponent(typeof(Skills.SkillManager))]
    public class PlayerNetworkInput : OwnerBehaviour
    {
        private const float ReviveInteractRadius = 2.5f;

        private PlayerNetworkController controller;
        private Skills.SkillManager     skillManager;
        private PlayerNetworkStats      playerStats;
        private Vector2                 lastSentDir;
        private PlayerReviveHandler     currentReviveTarget;

        private void Awake()
        {
            controller   = GetComponent<PlayerNetworkController>();
            skillManager = GetComponent<Skills.SkillManager>();
            playerStats  = GetComponent<PlayerNetworkStats>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame && skillManager != null)
                skillManager.ActivateFirstManualSkillServerRpc();

            if (keyboard.eKey.wasPressedThisFrame)
                TryStartRevive();
            else if (keyboard.eKey.wasReleasedThisFrame)
                StopRevive();
        }

        private void TryStartRevive()
        {
            // 본인이 다운 상태면 타인 부활 시도 불가
            if (playerStats != null && !playerStats.CanAct) return;

            var all = PlayerReviveHandler.All;
            Debug.Log($"[TryStartRevive] E 입력. 등록된 핸들러 수={all.Count}");

            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                var ps = h.GetComponent<PlayerNetworkStats>();
                float dist = Vector3.Distance(transform.position, h.transform.position);
                bool isDowned = ps != null && ps.IsDowned.Value;
                Debug.Log($"  [{i}] owner={h.NetworkObject.OwnerClientId}, local={NetworkManager.LocalClientId}, " +
                          $"isDowned={isDowned}, dist={dist:F2}, radius={ReviveInteractRadius}");
            }

            var target = FindDownedPlayerInRange();
            if (target == null) return;
            currentReviveTarget = target;
            target.BeginReviveServerRpc();
        }

        private void StopRevive()
        {
            if (currentReviveTarget == null) return;
            currentReviveTarget.CancelReviveServerRpc();
            currentReviveTarget = null;
        }

        private PlayerReviveHandler FindDownedPlayerInRange()
        {
            var all = PlayerReviveHandler.All;
            for (int i = 0; i < all.Count; i++)
            {
                var handler = all[i];
                if (!handler.IsSpawned) continue;
                if (handler.NetworkObject.OwnerClientId == NetworkManager.LocalClientId) continue;

                var pStats = handler.GetComponent<PlayerNetworkStats>();
                if (pStats == null || !pStats.IsDowned.Value) continue;

                if (Vector3.Distance(transform.position, handler.transform.position) <= ReviveInteractRadius)
                    return handler;
            }
            return null;
        }

        private void FixedUpdate()
        {
            if (controller == null) return;

            Vector2 raw      = ReadMoveInput();
            Vector2 worldDir = ToCameraRelative(raw);

            if (worldDir == lastSentDir) return;
            lastSentDir = worldDir;
            controller.SubmitMoveInputServerRpc(worldDir);
        }

        private static Vector2 ReadMoveInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;

            return Vector2.ClampMagnitude(input, 1f);
        }

        // 카메라 forward/right를 XZ 평면에 투영해 월드 방향으로 변환
        private static Vector2 ToCameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f) return Vector2.zero;

            Camera cam = Camera.main;
            if (cam == null) return input;

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return input;
            forward.Normalize();

            Vector3 right = cam.transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 worldDir = forward * input.y + right * input.x;
            return Vector2.ClampMagnitude(new Vector2(worldDir.x, worldDir.z), 1f);
        }
    }
}
