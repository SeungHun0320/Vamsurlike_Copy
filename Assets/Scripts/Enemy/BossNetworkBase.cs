namespace Vamsurlike.Enemy
{
    // 전용 보스 프리팹과의 호환용 타입.
    // 보스 판정과 Clear 전환은 EnemyNetworkBase가 EnemyDataSO.isBoss를 기준으로 처리한다.
    public class BossNetworkBase : EnemyNetworkBase
    {
    }
}
