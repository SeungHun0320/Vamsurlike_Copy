using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vamsurlike.Network;
using Vamsurlike.Player;

namespace Vamsurlike.EditorTools
{
    public static class Phase2NetworkPlayerSetup
    {
        private const string PrefabDir = "Assets/Prefabs/Player";
        private const string PrefabPath = "Assets/Prefabs/Player/NetworkedPlayer.prefab";
        private const string MaterialPath = "Assets/Resources/Materials/Player_Prototype.mat";

        [MenuItem("Vamsurlike/Phase 2/Setup Network Player")]
        public static void Setup()
        {
            EnsureFolders();
            GameObject playerPrefab = CreatePlayerPrefab();
            ConfigureBootstrap(playerPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase2NetworkPlayerSetup] NetworkedPlayer prefab and Bootstrap NetworkManager configured.");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets", "Prefabs");
            CreateFolderIfMissing("Assets/Prefabs", "Player");
            CreateFolderIfMissing("Assets", "Resources");
            CreateFolderIfMissing("Assets/Resources", "Materials");
        }

        private static void CreateFolderIfMissing(string parent, string folder)
        {
            string path = $"{parent}/{folder}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folder);
        }

        private static GameObject CreatePlayerPrefab()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.65f, 1f, 1f) };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            GameObject root = new GameObject("NetworkedPlayer");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                SetLayerRecursively(root, playerLayer);
            root.tag = "Player";

            root.AddComponent<NetworkObject>().SpawnWithObservers = true;
            root.AddComponent<NetworkTransform>().UseUnreliableDeltas = true;

            CharacterController characterController = root.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.4f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.3f;
            characterController.slopeLimit = 45f;
            characterController.skinWidth = 0.08f;

            root.AddComponent<PlayerNetworkStats>();
            root.AddComponent<PlayerNetworkController>();
            root.AddComponent<PlayerNetworkInput>();
            root.AddComponent<PlayerNetworkAnimator>();
            root.AddComponent<LocalPlayerCameraBinder>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;
            if (playerLayer >= 0)
                SetLayerRecursively(visual, playerLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ConfigureBootstrap(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            NetworkManager networkManager = Object.FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("[Phase2NetworkPlayerSetup] Bootstrap scene NetworkManager not found.");
                return;
            }

            if (networkManager.GetComponent<NetworkPlayerSpawner>() == null)
                networkManager.gameObject.AddComponent<NetworkPlayerSpawner>();

            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(networkManager.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
