using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vamsurlike.Core;
using Vamsurlike.Player;

namespace Vamsurlike.Network
{
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "Stage_01";

        private string GameplaySceneName => SceneConfigSO.Instance != null ? SceneConfigSO.Instance.stageSceneName : gameplaySceneName;
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
            StartCoroutine(SpawnPlayersOverFrames(clientIds));
        }

        // 재시작 등으로 여러 클라이언트가 동시에 씬 동기화를 끝내면, 한 프레임에 플레이어 전원을
        // Instantiate + SpawnAsPlayerObject 하게 되어 그 프레임 하나가 무거워진다 — 한 명씩 한 프레임에
        // 나눠 스폰해서 그 순간의 부하를 줄인다.
        private IEnumerator SpawnPlayersOverFrames(List<ulong> clientIds)
        {
            foreach (ulong clientId in clientIds)
            {
                SpawnPlayerForClient(clientId);
                yield return null;
            }
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

            if (LobbyPlayerState.All.TryGetValue(clientId, out var lobbyState))
            {
                string playerName = ResolvePlayerName(clientId, lobbyState);
                if (player.TryGetComponent(out PlayerMatchStats matchStats))
                    matchStats.SetPlayerName(playerName);

                if (player.TryGetComponent(out PlayerColorSync colorSync))
                    colorSync.SetColorIndex(lobbyState.ColorIndex.Value);
            }
            else if (NetworkSessionService.PendingPlayerNames.TryGetValue(clientId, out string playerName))
            {
                if (player.TryGetComponent(out PlayerMatchStats matchStats))
                    matchStats.SetPlayerName(playerName);
                NetworkSessionService.PendingPlayerNames.Remove(clientId);
            }
        }

        private static string ResolvePlayerName(ulong clientId, LobbyPlayerState lobbyState)
        {
            string playerName = null;
            if (NetworkSessionService.PendingPlayerNames.TryGetValue(clientId, out string pendingName))
            {
                playerName = pendingName;
                NetworkSessionService.PendingPlayerNames.Remove(clientId);
            }

            string lobbyName = lobbyState.DisplayName.Value.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(lobbyName)
                && (string.IsNullOrWhiteSpace(playerName) || string.Equals(playerName, "Player", System.StringComparison.Ordinal)))
            {
                playerName = lobbyName;
            }

            return string.IsNullOrWhiteSpace(playerName) ? $"Player {clientId}" : playerName.Trim();
        }

        private bool CanServerSpawnInScene(string sceneName)
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsServer
                && sceneName == GameplaySceneName;
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
