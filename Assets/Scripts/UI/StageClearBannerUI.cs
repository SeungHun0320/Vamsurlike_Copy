using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // Clear 전용 배너: 클리어 문구와 결과 진입 버튼만 렌더링한다.
    public sealed class StageClearBannerUI : MonoBehaviour
    {
        [SerializeField] private GameObject backdrop;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text   messageText;
        [SerializeField] private Button     continueButton;

        [Header("Messages")]
        [SerializeField] private string clearMessage = "STAGE CLEAR";
        [SerializeField] private float  clearDelay   = 1f;

        private Coroutine                 showCoroutine;
        private StageClearBannerViewModel viewModel;

        private void Awake()
        {
            viewModel = new StageClearBannerViewModel();
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void Start()
        {
            ApplyStateImmediate(new StageClearBannerViewState(StageClearBannerDisplayMode.Hidden));
        }

        private void OnEnable()
        {
            viewModel.StateChanged += ApplyState;
            viewModel.Bind();
        }

        private void OnDisable()
        {
            viewModel.StateChanged -= ApplyState;
            viewModel.Unbind();

            if (showCoroutine != null) StopCoroutine(showCoroutine);
        }

        private void OnDestroy()
        {
            if (continueButton != null)
                continueButton.onClick.RemoveListener(OnContinueClicked);
            viewModel?.Dispose();
        }

        private void ApplyState(StageClearBannerViewState state)
        {
            if (showCoroutine != null) StopCoroutine(showCoroutine);

            if (state.Mode == StageClearBannerDisplayMode.Clear)
            {
                showCoroutine = StartCoroutine(ApplyStateAfterDelay(state));
                return;
            }

            ApplyStateImmediate(state);
        }

        private IEnumerator ApplyStateAfterDelay(StageClearBannerViewState state)
        {
            yield return new WaitForSecondsRealtime(clearDelay);
            showCoroutine = null;
            ApplyStateImmediate(state);
        }

        private void ApplyStateImmediate(StageClearBannerViewState state)
        {
            if (messageText != null) messageText.text = clearMessage;
            if (backdrop != null) backdrop.SetActive(state.BackdropVisible);
            if (panel != null) panel.SetActive(state.IsVisible);
            if (continueButton != null) continueButton.gameObject.SetActive(state.ContinueVisible);
        }

        private void OnContinueClicked()
        {
            viewModel.ContinueToResult();
        }
    }
}
