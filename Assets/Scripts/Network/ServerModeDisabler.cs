using UnityEngine;

namespace Vamsurlike.Network
{
    // 서버 모드(전용 서버 빌드 또는 에디터 MPM "Server" 태그)에서
    // 이 컴포넌트가 붙은 GameObject를 비활성화한다.
    // Stage_01 씬의 UI Canvas 루트 등 서버에서 불필요한 오브젝트에 부착.
    public class ServerModeDisabler : MonoBehaviour
    {
        private void Awake()
        {
            if (NetworkBootstrapper.IsServerMode())
                gameObject.SetActive(false);
        }
    }
}
