using TMPro;
using UnityEngine;
using Vamsurlike.Stage;

namespace Vamsurlike.UI
{
    // Stage 씬에 배치. Inspector에서 resultPanel, resultText를 연결.
    // GameFlowCoordinator.CurrentFlow 변경을 구독해 결과 화면 표시.
    // Phase 8에서 애니메이션·버튼·귀환 로직을 추가한다.
    public class StageResultUI : MonoBehaviour
    {
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text   resultText;

        private void Start()
        {
            if (resultPanel != null) resultPanel.SetActive(false);

            if (GameFlowCoordinator.Instance != null)
                GameFlowCoordinator.Instance.CurrentFlow.OnValueChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (GameFlowCoordinator.Instance != null)
                GameFlowCoordinator.Instance.CurrentFlow.OnValueChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameFlowState _, GameFlowState next)
        {
            if (next == GameFlowState.Clear)
                ShowResult("STAGE CLEAR");
            else if (next == GameFlowState.GameOver)
                ShowResult("GAME OVER");
            else if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        private void ShowResult(string message)
        {
            if (resultText != null) resultText.text = message;
            if (resultPanel != null) resultPanel.SetActive(true);
        }
    }
}
