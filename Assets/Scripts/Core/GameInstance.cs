using UnityEngine;

namespace Vamsurlike.Core
{
    public class GameInstance : MonoBehaviour
    {
        public static GameInstance I { get; private set; }

        private ICoreFacade coreFacade;
        private IWorldFacade worldFacade;

        public ICoreFacade Core => coreFacade;

        // IWorldFacade는 서버에서만 실질적으로 동작
        public IWorldFacade World => worldFacade;

        private void Awake()
        {
            if (I != null)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            DontDestroyOnLoad(gameObject);
        }

        public void RegisterCore(ICoreFacade facade)
        {
            if (facade == null)
            {
                Debug.LogError($"[{nameof(GameInstance)}] ICoreFacade 등록 실패: null 전달됨.", this);
                return;
            }
            coreFacade = facade;
        }

        public void RegisterWorld(IWorldFacade facade)
        {
            if (facade == null)
            {
                Debug.LogError($"[{nameof(GameInstance)}] IWorldFacade 등록 실패: null 전달됨.", this);
                return;
            }
            worldFacade = facade;
        }
    }
}
