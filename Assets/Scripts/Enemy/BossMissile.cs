using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;

namespace Vamsurlike.Enemy
{
    // 보스 전용 미사일. NetworkObject + NetworkTransform으로 모든 클라이언트에 표시됨.
    // 서버만 이동·충돌을 처리한다.
    [RequireComponent(typeof(NetworkObject))]
    public sealed class BossMissile : NetworkBehaviour
    {
        [SerializeField] private float hitRadius = 1.5f;

        private Vector3 moveDirection;
        private float   speed;
        private float   damage;
        private float   remainingLife;
        private bool    active;

        // BossPatternController에서 Spawn() 직후 호출
        public void InitLinear(Vector3 direction, float missilSpeed, float missilDamage, float lifetime)
        {
            moveDirection = direction.normalized;
            speed         = missilSpeed;
            damage        = missilDamage;
            remainingLife = lifetime;
            active        = true;

            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        private void Update()
        {
            if (!IsServer || !active) return;

            remainingLife -= Time.deltaTime;
            if (remainingLife <= 0f) { DespawnSelf(); return; }

            transform.position += moveDirection * speed * Time.deltaTime;
            CheckHit();
        }

        private void CheckHit()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            float sqrR = hitRadius * hitRadius;
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if ((client.PlayerObject.transform.position - transform.position).sqrMagnitude > sqrR) continue;
                if (client.PlayerObject.TryGetComponent(out PlayerNetworkStats stats))
                    stats.TakeDamage(damage);
                DespawnSelf();
                return;
            }
        }

        private void DespawnSelf()
        {
            active = false;
            if (IsSpawned) NetworkObject.Despawn(true);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            active = false;
        }
    }
}
