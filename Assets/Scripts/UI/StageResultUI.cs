using System.Collections;
using TMPro;
using UnityEngine;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // Stage 씬에 배치. Inspector에서 resultPanel, resultText를 연결.
    // Phase 8에서 애니메이션·버튼·귀환 로직을 추가한다.
    public class StageResultUI : MonoBehaviour
    {
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text   resultText;
        [SerializeField] private string     clearMessage    = "STAGE CLEAR";
        [SerializeField] private string     gameOverMessage = "GAME OVER";
        [SerializeField] private float      gameOverDelay   = 3f;
        [SerializeField] private float      clearDelay      = 1f;

        private Coroutine          showCoroutine;
        private StageResultViewModel viewModel;

        private void Awake()
        {
            viewModel = new StageResultViewModel();
        }

        private void Start()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        private void OnEnable()
        {
            viewModel.OnShowClear    += ShowClear;
            viewModel.OnShowGameOver += ShowGameOver;
            viewModel.OnHide         += HideResult;
            viewModel.Bind();
        }

        private void OnDisable()
        {
            viewModel.OnShowClear    -= ShowClear;
            viewModel.OnShowGameOver -= ShowGameOver;
            viewModel.OnHide         -= HideResult;
            viewModel.Unbind();

            if (showCoroutine != null) StopCoroutine(showCoroutine);
        }

        private void ShowClear()    => ScheduleShow(clearMessage,    clearDelay);
        private void ShowGameOver() => ScheduleShow(gameOverMessage, gameOverDelay);

        private void HideResult()
        {
            if (showCoroutine != null) { StopCoroutine(showCoroutine); showCoroutine = null; }
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        private void ScheduleShow(string message, float delay)
        {
            if (showCoroutine != null) StopCoroutine(showCoroutine);
            showCoroutine = StartCoroutine(ShowDelayed(message, delay));
        }

        // WaitForSecondsRealtime: Clear(timeScale=0)과 GameOver(timeScale=1) 모두 대응
        private IEnumerator ShowDelayed(string message, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            showCoroutine = null;
            if (resultText  != null) resultText.text = message;
            if (resultPanel != null) resultPanel.SetActive(true);
        }
    }
}
