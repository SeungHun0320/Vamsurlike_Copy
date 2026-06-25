using UnityEngine;
using Vamsurlike.Items;
using Vamsurlike.Network;
using Vamsurlike.Stage;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Core
{
    // Bootstrap·Stage 진입 시 Inspector 참조 및 Resources SO를 일괄 검증한다.
    // 오류를 모두 출력한 뒤 valid 여부를 반환 — 첫 번째 오류에서 멈추지 않음.
    internal static class StartupValidator
    {
        internal static bool ValidateBootstrap(PoolManager poolManager)
        {
            bool valid = true;
            valid &= ValidateCatalogs();
            valid &= ValidatePoolManager(poolManager);

            if (valid)
                Debug.Log("[StartupValidator] Bootstrap 검증 완료.");
            else
                Debug.LogError("[StartupValidator] Bootstrap 검증 실패 — 위 오류를 확인하세요.");

            return valid;
        }

        internal static bool ValidateStage(WaveController waveController, DropManager dropManager)
        {
            bool valid = true;

            if (waveController == null)
            {
                Debug.LogError("[StartupValidator] StageRuntime에 WaveController가 할당되지 않았습니다.");
                valid = false;
            }

            if (dropManager == null)
            {
                Debug.LogError("[StartupValidator] StageRuntime에 DropManager가 할당되지 않았습니다.");
                valid = false;
            }

            if (valid)
                Debug.Log("[StartupValidator] Stage 검증 완료.");
            else
                Debug.LogError("[StartupValidator] Stage 검증 실패 — 위 오류를 확인하세요.");

            return valid;
        }

        private static bool ValidateCatalogs()
        {
            bool valid = true;

            if (UpgradeCatalog.Instance == null)
            {
                Debug.LogError("[StartupValidator] Resources/UpgradeCatalog.asset을 찾을 수 없습니다.");
                valid = false;
            }

            if (CombineRecipeCatalog.Instance == null)
            {
                Debug.LogError("[StartupValidator] Resources/CombineRecipeCatalog.asset을 찾을 수 없습니다.");
                valid = false;
            }

            if (ChestFallbackRewardCatalog.Instance == null)
            {
                Debug.LogError("[StartupValidator] Resources/ChestFallbackRewardCatalog.asset을 찾을 수 없습니다.");
                valid = false;
            }

            return valid;
        }

        private static bool ValidatePoolManager(PoolManager poolManager)
        {
            if (poolManager == null)
            {
                Debug.LogError("[StartupValidator] PoolManager를 찾을 수 없습니다.");
                return false;
            }

            return poolManager.Validate();
        }
    }
}
