using System;
using System.Collections.Generic;
using DiceBattle.Core;
using UnityEngine;

namespace DiceBattle.Data
{
    [CreateAssetMenu(menuName = "Dice Battle/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] EnemyBehaviorType behavior;
        [SerializeField] int maximumHp = 35;
        [SerializeField] int attackDamage = 5;
        [SerializeField] int startingDistance = 3;
        [SerializeField] int movementAmount = 1;
        [SerializeField] int attackCountdown = 2;
        [SerializeField] Color visualColor = Color.red;
        [SerializeField] float visualScale = 1f;
        public string Id => id; public string DisplayName => displayName; public EnemyBehaviorType Behavior => behavior;
        public int MaximumHp => maximumHp; public int AttackDamage => attackDamage; public int StartingDistance => startingDistance;
        public int MovementAmount => movementAmount; public int AttackCountdown => attackCountdown;
        public Color VisualColor => visualColor; public float VisualScale => visualScale;
#if UNITY_EDITOR
        public void Configure(string newId, string newName, EnemyBehaviorType newBehavior, int hp, int damage, int distance, int movement, int countdown, Color color, float scale)
        { id = newId; displayName = newName; behavior = newBehavior; maximumHp = hp; attackDamage = damage; startingDistance = distance; movementAmount = movement; attackCountdown = countdown; visualColor = color; visualScale = scale; }
#endif
    }

    [Serializable]
    public sealed class EnemySpawnEntry
    {
        public EnemyDefinition definition;
        public int hpOverride;
        public int damageOverride;
        public int distanceOverride;
        [Range(0, 2)] public int spawnLane;
        public float spawnDelay;
    }

}
