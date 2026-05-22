using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vamsurlike.Network
{
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "Stage_01";
        [SerializeField] private Vector3[] spawnPositions =
        {
            new(-2f, 1f, -2f),
            new( 2f, 1f, -2f),
            new(-2f, 1f,  2f),
            new( 2f, 1f,  2f)
        };

        private bool isSubscribedToSceneEvents;
        private string spawnedSceneName;

        private void Start()
        {
            TrySubscribeToSceneEvents();
        }

        private void Update()
        {
            TrySubscribeToSceneEvents();

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (spawnedSceneName == activeSceneName) return;
            if (!CanServerSpawnInScene(activeSceneName)) return;
            if (!HasUnspawnedConnectedClient()) return;

            SpawnPlayersForConnectedClients();
            spawnedSceneName = activeSceneName;
        }

        private void OnDestroy()
        {
            UnsubscribeFromSceneEvents();
        }

        private void TrySubscribeToSceneEvents()
        {
            if (isSubscribedToSceneEvents) return;
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.SceneManager == null) return;

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += HandleSynchronizeComplete;
            isSubscribedToSceneEvents = true;
            Debug.Log($"[{nameof(NetworkPlayerSpawner)}] NetworkSceneManager 이벤트 구독 완료.");
        }

        private void UnsubscribeFromSceneEvents()
        {
            if (!isSubscribedToSceneEvents) return;
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.SceneManager == null) return;

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete -= HandleSynchronizeComplete;
            isSubscribedToSceneEvents = false;
        }

        private void HandleLoadEventCompleted(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (!CanServerSpawnInScene(sceneName)) return;
            SpawnPlayersForConnectedClients();
            spawnedSceneName = sceneName;
        }

        private void HandleSynchronizeComplete(ulong clientId)
        {
            if (!CanServerSpawnInScene(SceneManager.GetActiveScene().name)) return;
            SpawnPlayerForClient(clientId);
        }

        public void SpawnPlayersForConnectedClients()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            var clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            foreach (ulong clientId in clientIds)
                SpawnPlayerForClient(clientId);
        }

        public void SpawnPlayerForClient(ulong clientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            if (client.PlayerObject != null) return;

            GameObject playerPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            if (playerPrefab == null)
            {
                Debug.LogError($"[{nameof(NetworkPlayerSpawner)}] NetworkConfig.PlayerPrefab이 설정되지 않았습니다.", this);
                return;
            }

            Vector3 spawnPosition = GetSpawnPosition(clientId);
            GameObject player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            if (!player.TryGetComponent(out NetworkObject networkObject))
            {
                Debug.LogError($"[{nameof(NetworkPlayerSpawner)}] PlayerPrefab에 NetworkObject가 없습니다.", player);
                Destroy(player);
                return;
            }

            networkObject.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            Debug.Log($"[{nameof(NetworkPlayerSpawner)}] clientId {clientId} 플레이어 스폰 완료. position={spawnPosition}");
        }

        private bool CanServerSpawnInScene(string sceneName)
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsServer
                && sceneName == gameplaySceneName;
        }

        private Vector3 GetSpawnPosition(ulong clientId)
        {
            if (spawnPositions == null || spawnPositions.Length == 0)
                return Vector3.zero;

            int index = (int)(clientId % (ulong)spawnPositions.Length);
            return spawnPositions[index];
        }

        private bool HasUnspawnedConnectedClient()
        {
            if (NetworkManager.Singleton == null) return false;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                    continue;

                if (client.PlayerObject == null)
                    return true;
            }

            return false;
        }
    }
}
