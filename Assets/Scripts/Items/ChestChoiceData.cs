using Unity.Netcode;

namespace Vamsurlike.Items
{
    public enum ChestChoiceType : byte
    {
        UpgradeOption, // UpgradeCatalog 인덱스
        Evolution      // CombineRecipeCatalog 인덱스
    }

    // RPC로 전송되는 상자 카드 1장의 직렬화 구조체
    public struct ChestChoiceData : INetworkSerializable
    {
        public ChestChoiceType type;
        public int             index;

        public ChestChoiceData(ChestChoiceType type, int index)
        {
            this.type  = type;
            this.index = index;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref index);
        }
    }
}
