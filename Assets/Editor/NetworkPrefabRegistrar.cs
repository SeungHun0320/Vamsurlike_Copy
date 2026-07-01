using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// LobbyPlayerState 프리팹을 NetworkManager의 NetworkPrefabs 리스트에 자동 등록.
// 이 파일을 삭제하거나 #if 를 제거하면 메뉴에서 수동 실행도 가능.
[InitializeOnLoad]
public static class NetworkPrefabRegistrar
{
    private const string LobbyStatePrefabPath = "Assets/Prefabs/Network/LobbyPlayerState.prefab";
    private const string BootstrapScenePath   = "Assets/Scenes/Bootstrap.unity";

    static NetworkPrefabRegistrar()
    {
        EditorApplication.delayCall += TryRegister;
    }

    [MenuItem("Tools/Vamsurlike/Register Network Prefabs")]
    public static void TryRegister()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyStatePrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[NetworkPrefabRegistrar] 프리팹을 찾을 수 없습니다: {LobbyStatePrefabPath}");
            return;
        }

        // Bootstrap 씬을 추가 로드(이미 열려 있으면 무시됨)
        var scene = EditorSceneManager.GetSceneByPath(BootstrapScenePath);
        if (!scene.isLoaded)
        {
            Debug.LogWarning("[NetworkPrefabRegistrar] Bootstrap 씬이 로드되지 않았습니다. 씬을 먼저 열어주세요.");
            return;
        }

        var nmGo = GameObject.Find("NetworkManager");
        if (nmGo == null) return;

        var nm = nmGo.GetComponent<NetworkManager>();
        if (nm == null) return;

        var prefabsList = nm.NetworkConfig.Prefabs;
        foreach (var entry in prefabsList.Prefabs)
        {
            if (entry.Prefab == prefab) return; // 이미 등록됨
        }

        prefabsList.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(nm);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[NetworkPrefabRegistrar] LobbyPlayerState 프리팹 등록 완료.");
    }
}
