using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace Vamsurlike.Network
{
    // Bootstrap 씬에서 UGS 초기화 및 서버 빌드 자동 시작 담당
    public class NetworkBootstrapper : MonoBehaviour
    {
        private static NetworkBootstrapper instance;

        public static bool IsUgsReady { get; private set; }
        public static event Action OnUgsReady;

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(this);
                return;
            }
            instance = this;
            IsUgsReady = false;
            OnUgsReady = null;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private async void Start()
        {
            await InitializeUgsAsync();

#if UNITY_SERVER
            // 전용 서버 빌드: 자동으로 서버 모드 시작
            GameNetworkManager.Instance?.StartAsServer();
#endif
        }

        private async Task InitializeUgsAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[{nameof(NetworkBootstrapper)}] UGS 인증 완료. PlayerId: {AuthenticationService.Instance.PlayerId}");
                IsUgsReady = true;
                OnUgsReady?.Invoke();
            }
            catch (Exception e)
            {
                // 오프라인 환경에서도 로컬 플레이는 가능
                Debug.LogWarning($"[{nameof(NetworkBootstrapper)}] UGS 인증 실패 (로컬 전용 모드): {e.Message}");
            }
        }
    }
}
