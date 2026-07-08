using UnityEngine;

namespace Vamsurlike.Items
{
    // 아이템 픽업 비주얼(자식 오브젝트)에 배치. 서버 권위 위치(루트 Transform/NetworkTransform)는
    // 건드리지 않고, 순수 클라이언트 로컬 연출로 상하 사인파 이동 + 회전만 준다.
    // groundOffset은 메쉬 피벗이 중심에 있어도 바닥 위에 서 있는 것처럼 보이도록 들어올리는 값.
    public sealed class ItemPickupBobber : MonoBehaviour
    {
        [SerializeField] private float groundOffset = 0.5f;
        [SerializeField] private float bobAmplitude = 0.3f;
        [SerializeField] private float bobFrequency = 1.5f;
        [SerializeField] private float spinDegreesPerSecond = 90f;

        // 같은 프레임에 스폰되는 아이템끼리 전부 같은 위상으로 움직이면 부자연스러워
        // 오브젝트마다 랜덤 위상을 준다. RULES.md의 "서버 재현성용 시드 랜덤"은 서버 판정에
        // 적용되는 규칙이라, 이건 순수 클라이언트 시각 연출이므로 대상이 아니다.
        private float phase;
        private Vector3 basePosition;

        private void OnEnable()
        {
            phase = Random.Range(0f, Mathf.PI * 2f);
            basePosition = transform.localPosition;
        }

        private void Update()
        {
            float bobY = Mathf.Sin(Time.time * bobFrequency + phase) * bobAmplitude;
            transform.localPosition = basePosition + new Vector3(0f, groundOffset + bobY, 0f);
            transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
