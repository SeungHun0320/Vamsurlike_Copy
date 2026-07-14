namespace Vamsurlike.Network
{
    // 서버 프로세스가 UGS PlayerId 기준으로 디스크에 저장하는 진행도 스냅샷.
    [System.Serializable]
    internal class PlayerSaveData
    {
        public int Version;
        public int Gold;
        public int[] UpgradeLevels;
    }
}
