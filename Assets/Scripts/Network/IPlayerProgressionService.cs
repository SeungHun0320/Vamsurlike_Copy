using System;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Network
{
    internal interface IPlayerProgressionService : IDisposable
    {
        void RegisterMessageHandler();
        void UnregisterMessageHandler();
        void HandleClientConnected(ulong clientId);
        void HandleClientDisconnected(ulong clientId);

        // 서버 전용 — 매치 종료 시 서버가 직접 골드를 지급한다(클라이언트 자가 신고 아님).
        void CreditGold(ulong clientId, int amount);

        // 클라이언트 전용 — 서버에 구매 요청을 보낸다. 결과는 동기화 메시지로 비동기 반영된다.
        void RequestPurchase(PermanentUpgradeType type);

        // 클라이언트 전용, 디버그 빌드에서만 유효 — 서버가 존재를 확인하고 무시 여부를 결정한다.
        void RequestDebugGrantGold(int amount);
        void RequestDebugResetUpgrades();
        void RequestDebugResetGold();
    }
}
