using UnityEngine;

namespace Vamsurlike.Core
{
    public class GameInstance : MonoBehaviour
    {
        public static GameInstance Instance { get; private set; }

        public ICoreFacade  Core    { get; private set; }
        public IWorldFacade World   { get; private set; }
        public GameManager  Manager { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Core    = GetComponentInChildren<CoreFacade>();
            World   = GetComponentInChildren<WorldFacade>();
            Manager = GetComponentInChildren<GameManager>();

            if (Core    == null) Debug.LogWarning("[GameInstance] CoreFacade not found.");
            if (Manager == null) Debug.LogWarning("[GameInstance] GameManager not found.");
        }

        public void SetWorld(IWorldFacade world) => World = world;
    }
}
