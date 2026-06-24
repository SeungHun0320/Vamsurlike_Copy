namespace Vamsurlike.Stage
{
    // 스테이지의 콘텐츠 진행 축. 전투 일시정지 여부와 독립적이다.
    public enum StagePhase
    {
        Waves,
        Boss
    }

    // 게임 시뮬레이션/UI 흐름 축. Gameplay가 아니면 전투 시뮬레이션을 멈춘다.
    public enum GameFlowState
    {
        Gameplay,
        LevelingUp,
        ChestOpening,
        Clear,
        GameOver
    }
}
