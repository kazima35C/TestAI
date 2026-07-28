using System;
using System.Collections.Generic;
using System.Linq;
using DiceBattle.Core;
using UnityEngine;

namespace DiceBattle.Dice
{
    [Serializable]
    public sealed class DiceCombinationResult
    {
        public DiceCombinationType Type;
        public string DisplayName;
        public float Multiplier;
        public int DiceSum;
        public int FinalDamage;
    }

    /// <summary>Pure evaluator for five standard dice. It has no scene dependencies.</summary>
    public static class DiceCombinationEvaluator
    {
        public static DiceCombinationResult Evaluate(IReadOnlyList<int> dice, float heroAttackMultiplier = 1f)
        {
            if (dice == null || dice.Count != 5) throw new ArgumentException("Exactly five dice are required.");
            if (dice.Any(v => v < 1 || v > 6)) throw new ArgumentOutOfRangeException(nameof(dice), "Dice must be 1..6.");

            var groups = dice.GroupBy(v => v).Select(g => g.Count()).OrderByDescending(v => v).ToArray();
            var unique = dice.Distinct().OrderBy(v => v).ToArray();
            DiceCombinationType type;
            float multiplier;
            string name;

            if (groups[0] == 5) { type = DiceCombinationType.FiveOfAKind; multiplier = 10f; name = "Five of a Kind"; }
            else if (groups[0] == 4) { type = DiceCombinationType.FourOfAKind; multiplier = 5f; name = "Four of a Kind"; }
            else if (groups[0] == 3 && groups.Length == 2) { type = DiceCombinationType.FullHouse; multiplier = 3.5f; name = "Full House"; }
            else if (unique.Length == 5 && unique[4] - unique[0] == 4) { type = DiceCombinationType.LargeStraight; multiplier = 3f; name = "Large Straight"; }
            else if (groups[0] == 3) { type = DiceCombinationType.ThreeOfAKind; multiplier = 2.5f; name = "Three of a Kind"; }
            else if (groups.Count(v => v == 2) == 2) { type = DiceCombinationType.TwoPair; multiplier = 2f; name = "Two Pair"; }
            else if (groups[0] == 2) { type = DiceCombinationType.OnePair; multiplier = 1.5f; name = "One Pair"; }
            else { type = DiceCombinationType.HighRoll; multiplier = 1f; name = "High Roll"; }

            int sum = dice.Sum();
            return new DiceCombinationResult
            {
                Type = type, DisplayName = name, Multiplier = multiplier, DiceSum = sum,
                FinalDamage = Mathf.RoundToInt(sum * multiplier * heroAttackMultiplier)
            };
        }
    }

    public static class CombatDamageCalculator
    {
        public static int Calculate(int diceSum, float combinationMultiplier, float heroMultiplier)
            => Mathf.RoundToInt(diceSum * combinationMultiplier * heroMultiplier);
    }
}
