using UnityEngine;

namespace Vamsurlike.Core
{
    public enum GameState { Playing, Paused, GameOver }

    public class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; } = GameState.Playing;

        public void SetState(GameState state)
        {
            if (State == state) return;
            State = state;

            switch (state)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    Debug.Log("[GameManager] Game Over.");
                    break;
            }
        }

        public void Pause()   => SetState(GameState.Paused);
        public void Resume()  => SetState(GameState.Playing);
        public void GameOver() => SetState(GameState.GameOver);
    }
}
