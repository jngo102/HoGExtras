using System;

namespace HoGExtras
{
    [Serializable]
    public class LocalSettings
    {
        private BossStatue.Completion _statueStateHollowKnight;
        private BossStatue.Completion _statueStateRadiance;

        public BossStatue.Completion StatueStateHollowKnight { get => _statueStateHollowKnight; set => _statueStateHollowKnight = value; }
        public BossStatue.Completion StatueStateRadiance { get => _statueStateRadiance; set => _statueStateRadiance = value; }
    }
}
