using System.Linq;
using System.Reflection;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Vamsurlike.Editor
{
    // Enemy D의 FBX 서브 클립을 보스 패턴용 Animator 상태로 연결한다.
    public static class SetupBossAnimator
    {
        private const string ControllerPath = "Assets/Resources/Animations/AC_Enemy_D.controller";
        private const string ModelPath      = "Assets/Resources/QuarterView 3D Action BE5/Models/Simple Enemy D.fbx";
        private const string PrefabPath     = "Assets/Prefabs/Enemies/Enemy D.prefab";
        private const string TauntName      = "Taunt";

        [MenuItem("Vamsurlike/Phase 7/Setup Boss Taunt Animation")]
        public static void Run()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || controller.layers.Length == 0)
            {
                Debug.LogError($"[{nameof(SetupBossAnimator)}] AnimatorController를 찾을 수 없습니다: {ControllerPath}");
                return;
            }

            AnimationClip tauntClip = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == TauntName);
            if (tauntClip == null)
            {
                Debug.LogError($"[{nameof(SetupBossAnimator)}] '{TauntName}' 클립을 찾을 수 없습니다: {ModelPath}");
                return;
            }

            EnsureTrigger(controller, TauntName);
            EnsureTauntState(controller.layers[0].stateMachine, tauntClip);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ControllerPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            if (!EnsureNetworkAnimatorParameter()) return;

            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(SetupBossAnimator)}] Enemy D에 Taunt 상태와 NetworkAnimator 동기화를 연결했습니다.");
        }

        private static void EnsureTrigger(AnimatorController controller, string parameterName)
        {
            if (controller.parameters.Any(parameter => parameter.name == parameterName)) return;
            controller.AddParameter(parameterName, AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureTauntState(AnimatorStateMachine stateMachine, AnimationClip clip)
        {
            AnimatorState tauntState = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == TauntName);
            if (tauntState == null)
                tauntState = stateMachine.AddState(TauntName, new Vector3(390f, 65f));
            tauntState.motion = clip;

            bool hasEntry = stateMachine.anyStateTransitions.Any(transition =>
                transition.destinationState == tauntState &&
                transition.conditions.Any(condition => condition.parameter == TauntName));
            if (!hasEntry)
            {
                AnimatorStateTransition entry = stateMachine.AddAnyStateTransition(tauntState);
                entry.AddCondition(AnimatorConditionMode.If, 0f, TauntName);
                entry.hasExitTime = false;
                entry.duration    = 0.05f;
            }

            AnimatorState idleState = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == "Idle");
            if (idleState == null || tauntState.transitions.Any(transition => transition.destinationState == idleState)) return;

            AnimatorStateTransition exit = tauntState.AddTransition(idleState);
            exit.hasExitTime = true;
            exit.exitTime    = 1f;
            exit.duration    = 0.1f;
        }

        private static bool EnsureNetworkAnimatorParameter()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return false;

            try
            {
                NetworkAnimator networkAnimator = root.GetComponentInChildren<NetworkAnimator>(true);
                if (networkAnimator == null)
                {
                    Debug.LogError($"[{nameof(SetupBossAnimator)}] NetworkAnimator가 없습니다: {PrefabPath}");
                    return false;
                }

                // Controller 변경 직후 NetworkAnimator의 에디터 캐시를 명시적으로 재구축한다.
                MethodInfo processEntries = typeof(NetworkAnimator).GetMethod(
                    "ProcessParameterEntries",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                processEntries?.Invoke(networkAnimator, null);
                EditorUtility.SetDirty(networkAnimator);

                var serializedObject = new SerializedObject(networkAnimator);
                SerializedProperty entries = serializedObject.FindProperty("AnimatorParameterEntries")
                    ?.FindPropertyRelative("ParameterEntries");
                if (entries == null)
                {
                    Debug.LogError($"[{nameof(SetupBossAnimator)}] NetworkAnimator 파라미터 목록을 찾지 못했습니다.");
                    return false;
                }

                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("name").stringValue == TauntName)
                        return true;
                }

                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty newEntry = entries.GetArrayElementAtIndex(index);
                newEntry.FindPropertyRelative("name").stringValue        = TauntName;
                newEntry.FindPropertyRelative("NameHash").intValue      = Animator.StringToHash(TauntName);
                newEntry.FindPropertyRelative("Synchronize").boolValue  = true;
                newEntry.FindPropertyRelative("ParameterType").intValue = (int)AnimatorControllerParameterType.Trigger;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
