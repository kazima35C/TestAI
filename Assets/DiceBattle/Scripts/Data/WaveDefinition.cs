using System.Collections.Generic;
using UnityEngine;

namespace DiceBattle.Data
{
    [CreateAssetMenu(menuName = "Dice Battle/Wave Definition")]
    public sealed class WaveDefinition : ScriptableObject
    {
        [SerializeField] int waveIndex;
        [SerializeField] List<EnemySpawnEntry> enemies = new();

        public int WaveIndex => waveIndex;
        public IReadOnlyList<EnemySpawnEntry> Enemies => enemies;
    }
}
