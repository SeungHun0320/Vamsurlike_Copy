using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.Core;
using Vamsurlike.Network;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // Result UI: 결과 제목과 플레이어 통계만 렌더링한다.
    // Clear 배너와 계속 버튼은 StageClearBannerUI가 담당한다.
    public class StageResultUI : MonoBehaviour
    {
        [Header("Shared")]
        [SerializeField] private GameObject backdrop;

        [Header("Title")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text   resultText;

        [Header("Stats")]
        [SerializeField] private GameObject        statsPanel;
        [SerializeField] private Transform         rowContainer;
        [SerializeField] private PlayerResultRowUI rowPrefab;

        [Header("Buttons")]
        [SerializeField] private Button mainMenuButton;

        [Header("Messages")]
        [SerializeField] private string resultMessage    = "RESULT";
        [SerializeField] private string gameOverMessage  = "GAME OVER";
        [SerializeField] private float  gameOverDelay    = 3f;
        [SerializeField] private string mainMenuScene    = "MainMenu";

        private Coroutine            showCoroutine;
        private StageResultViewModel viewModel;
        private PlayerResultViewModel[] pendingVMs;

        private void Awake()
        {
            viewModel = new StageResultViewModel();
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        private void Start()
        {
            ApplyStateImmediate(new StageResultViewState(StageResultDisplayMode.Hidden));
        }

        private void OnEnable()
        {
            viewModel.StateChanged    += ApplyState;
            viewModel.OnPlayerResults += OnPlayerResults;
            viewModel.Bind();
        }

        private void OnDisable()
        {
            viewModel.StateChanged    -= ApplyState;
            viewModel.OnPlayerResults -= OnPlayerResults;
            viewModel.Unbind();

            if (showCoroutine != null) StopCoroutine(showCoroutine);
        }

        private void OnDestroy() => viewModel?.Dispose();

        private void ApplyState(StageResultViewState state)
        {
            if (showCoroutine != null) StopCoroutine(showCoroutine);

            if (state.Mode == StageResultDisplayMode.GameOver)
            {
                showCoroutine = StartCoroutine(ApplyStateAfterDelay(state, gameOverDelay));
                return;
            }

            ApplyStateImmediate(state);
        }

        private void OnPlayerResults(PlayerResultViewModel[] vms)
        {
            pendingVMs = vms;
            if (statsPanel != null && statsPanel.activeSelf)
                PopulateRows(vms);
        }

        private IEnumerator ApplyStateAfterDelay(StageResultViewState state, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            showCoroutine = null;
            ApplyStateImmediate(state);
        }

        private void OnMainMenuClicked()
        {
            if (GameNetworkManager.Instance != null) GameNetworkManager.Instance.Disconnect();
            if (SceneLoader.Instance != null)        SceneLoader.Instance.LoadSceneLocal(mainMenuScene);
        }

        private void ApplyStateImmediate(StageResultViewState state)
        {
            if (backdrop      != null) backdrop.SetActive(state.BackdropVisible);
            if (resultPanel   != null) resultPanel.SetActive(state.IsVisible);
            if (statsPanel    != null) statsPanel.SetActive(state.StatsVisible);
            if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(state.IsVisible);

            if (resultText != null)
            {
                resultText.text = state.Mode == StageResultDisplayMode.GameOver
                    ? gameOverMessage
                    : resultMessage;
            }

            if (state.StatsVisible && pendingVMs != null)
                PopulateRows(pendingVMs);
        }

        private void PopulateRows(PlayerResultViewModel[] vms)
        {
            if (rowContainer == null || rowPrefab == null) return;

            foreach (Transform child in rowContainer)
                Destroy(child.gameObject);

            foreach (var vm in vms)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                row.Bind(vm);
            }
        }
    }
}
