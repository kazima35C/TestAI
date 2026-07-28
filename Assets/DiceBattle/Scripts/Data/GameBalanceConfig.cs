using UnityEngine;

namespace DiceBattle.Data
{
    [CreateAssetMenu(menuName = "Dice Battle/Game Balance")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        public int playerMaximumHp = 120;
        public float heroAttackMultiplier = 1f;
        public int rerollsPerTurn = 3;
        public float diceRollDuration = .1f;
        public float enemyActionGap = .3f;
        public float waveIntroDuration = 1.1f;
    }
}
