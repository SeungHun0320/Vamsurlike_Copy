using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI
{
    // 획득 로그 UI — AcquisitionLogRequested 이벤트를 받아 화면 왼쪽에 메시지를 스택으로 표시.
    // ViewModel 불필요(fire-and-forget). container 에 동적으로 행을 생성 후 fadeout하고 제거.
    public sealed class AcquisitionLogUI : MonoBehaviour
    {
        [SerializeField] private Transform container;
        [SerializeField] private int        maxRows      = 5;
        [SerializeField] private float      fadeFraction = 0.3f;
        [SerializeField] private float      fontSize     = 22f;

        private void OnEnable()
        {
            if (UIEventHub.Instance != null)
                UIEventHub.Instance.Stage.AcquisitionLogRequested += Show;
        }

        private void OnDisable()
        {
            if (UIEventHub.Instance != null)
                UIEventHub.Instance.Stage.AcquisitionLogRequested -= Show;
        }

        private void Show(AcquisitionLogPayload p)
        {
            if (container == null) return;

            // 최대 개수 초과 시 가장 오래된 행 제거
            if (container.childCount >= maxRows && container.GetChild(0) is Transform oldest)
                Destroy(oldest.gameObject);

            var rowGO = new GameObject("LogRow");
            rowGO.transform.SetParent(container, false);

            var rt = rowGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 32f);

            var tmp = rowGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = p.Message;
            tmp.color     = p.Color != default ? p.Color : Color.white;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = FontStyles.Bold;

            var cg = rowGO.AddComponent<CanvasGroup>();

            StartCoroutine(FadeAndDestroy(rowGO, cg, p.Duration > 0f ? p.Duration : 2.5f));
        }

        private IEnumerator FadeAndDestroy(GameObject row, CanvasGroup cg, float duration)
        {
            float showTime = duration * (1f - fadeFraction);
            yield return new WaitForSecondsRealtime(showTime);

            float fadeTime = duration * fadeFraction;
            float elapsed  = 0f;
            while (elapsed < fadeTime && row != null)
            {
                elapsed  += Time.unscaledDeltaTime;
                cg.alpha  = 1f - elapsed / fadeTime;
                yield return null;
            }

            if (row != null) Destroy(row);
        }
    }
}
