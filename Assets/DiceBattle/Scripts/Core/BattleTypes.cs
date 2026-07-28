using System;
using System.Collections.Generic;
using DiceBattle.Dice;
using UnityEngine;

namespace DiceBattle.Core
{
    public enum BattleState { Initializing, WaveIntro, WaitingForInitialRoll, RollingDice, PlayerDecision, PlayerAttacking, EnemyTurn, WaveComplete, Victory, Defeat, Paused }
    public enum EnemyBehaviorType { Grunt, Runner, Tank, Archer }
    public enum DiceCombinationType { HighRoll, OnePair, TwoPair, ThreeOfAKind, LargeStraight, FullHouse, FourOfAKind, FiveOfAKind }

    /// <summary>Small explicit state machine which rejects illegal transitions.</summary>
    [Serializable]
    public sealed class BattleStateMachine
    {
        public BattleState Current { get; private set; } = BattleState.Initializing;
        public BattleState BeforePause { get; private set; } = BattleState.Initializing;
        public event Action<BattleState> Changed;

        public void Set(BattleState next)
        {
            if (next == BattleState.Paused) BeforePause = Current;
            Current = next;
            Changed?.Invoke(Current);
        }

        public void Resume() => Set(BeforePause);
        public bool Is(BattleState state) => Current == state;
    }

    public interface IDiceRandom { int NextDie(); }

    public sealed class DiceRandom : IDiceRandom
    {
        readonly System.Random random;
        public DiceRandom(bool deterministic, int seed) => random = deterministic ? new System.Random(seed) : new System.Random();
        public int NextDie() => random.Next(1, 7);
    }

    [Serializable]
    public sealed class GameStatisticsTracker
    {
        public int Turns { get; private set; }
        public int TotalDamage { get; private set; }
        public DiceCombinationType Highest { get; private set; } = DiceCombinationType.HighRoll;
        public void Record(DiceCombinationResult result)
        {
            Turns++;
            TotalDamage += result.FinalDamage;
            if (result.Type > Highest) Highest = result.Type;
        }
    }
}
