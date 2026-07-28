using DiceBattle.Core;
using DiceBattle.Dice;
using NUnit.Framework;

namespace DiceBattle.Tests
{
    public sealed class DiceCombinationEvaluatorTests
    {
        [TestCase(new[]{1,2,3,5,6},DiceCombinationType.HighRoll,1f)]
        [TestCase(new[]{2,2,3,5,6},DiceCombinationType.OnePair,1.5f)]
        [TestCase(new[]{2,2,5,5,6},DiceCombinationType.TwoPair,2f)]
        [TestCase(new[]{4,4,4,2,6},DiceCombinationType.ThreeOfAKind,2.5f)]
        [TestCase(new[]{1,2,3,4,5},DiceCombinationType.LargeStraight,3f)]
        [TestCase(new[]{2,3,4,5,6},DiceCombinationType.LargeStraight,3f)]
        [TestCase(new[]{3,3,3,6,6},DiceCombinationType.FullHouse,3.5f)]
        [TestCase(new[]{5,5,5,5,2},DiceCombinationType.FourOfAKind,5f)]
        [TestCase(new[]{6,6,6,6,6},DiceCombinationType.FiveOfAKind,10f)]
        public void DetectsHands(int[] values,DiceCombinationType expected,float multiplier)
        {var result=DiceCombinationEvaluator.Evaluate(values);Assert.AreEqual(expected,result.Type);Assert.AreEqual(multiplier,result.Multiplier);}

        [Test] public void FiveKindHasExpectedDamage(){var r=DiceCombinationEvaluator.Evaluate(new[]{6,6,6,6,6});Assert.AreEqual(30,r.DiceSum);Assert.AreEqual(300,r.FinalDamage);}
        [Test] public void FullHouseWinsOverThreeKind()=>Assert.AreEqual(DiceCombinationType.FullHouse,DiceCombinationEvaluator.Evaluate(new[]{3,6,3,6,3}).Type);
        [Test] public void DamageRoundsUsingUnityRule()=>Assert.AreEqual(42,DiceCombinationEvaluator.Evaluate(new[]{2,2,2,5,6}).FinalDamage);
        [Test] public void InputOrderDoesNotMatter()=>Assert.AreEqual(DiceCombinationEvaluator.Evaluate(new[]{6,2,6,6,6}).Type,DiceCombinationEvaluator.Evaluate(new[]{6,6,6,6,2}).Type);
    }
}
