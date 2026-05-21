using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

// One-shot initializer: calls EnemyBase.Initialize at runtime start,
// then deals damage to the player after a short delay to verify the damage pipeline.
public class DummyEnemyInit : MonoBehaviour
{
    public EnemyDataSO enemyData;

    private void Start()
    {
        GetComponent<EnemyBase>().Initialize(enemyData);

        Invoke(nameof(HitPlayer), 2f);
    }

    private void HitPlayer()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) { Debug.LogWarning("[DummyEnemyInit] Player not found."); return; }

        if (player.TryGetComponent(out Vamsurlike.Core.IDamageable target))
            target.TakeDamage(enemyData.attackPower);
        else
            Debug.LogWarning("[DummyEnemyInit] IDamageable not found on Player.");
    }
}
