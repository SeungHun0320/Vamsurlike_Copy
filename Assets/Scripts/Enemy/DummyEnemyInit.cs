using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

// One-shot initializer: calls EnemyBase.Initialize at runtime start,
// then deals damage to the player after a short delay to verify the damage pipeline.
public class DummyEnemyInit : MonoBehaviour
{
    public EnemyDataSO m_data;

    private void Start()
    {
        GetComponent<EnemyBase>().Initialize(m_data);

        // Hit player after 2 seconds to test damage log
        Invoke(nameof(HitPlayer), 2f);
    }

    private void HitPlayer()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) { Debug.LogWarning("[DummyEnemyInit] Player not found."); return; }

        if (player.TryGetComponent(out Vamsurlike.Player.IPlayerFacade facade))
            facade.TakeDamage(m_data.m_fAttackPower);
        else
            Debug.LogWarning("[DummyEnemyInit] IPlayerFacade not found on Player.");
    }
}
