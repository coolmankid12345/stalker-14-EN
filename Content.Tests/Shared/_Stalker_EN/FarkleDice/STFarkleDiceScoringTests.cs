using System.Collections.Generic;
using Content.Shared._Stalker_EN.FarkleDice;
using NUnit.Framework;

namespace Content.Tests.Shared._Stalker_EN.FarkleDice;

[TestFixture]
[TestOf(typeof(STFarkleDiceScoring))]
[Parallelizable(ParallelScope.All)]
public sealed class STFarkleDiceScoringTests
{
    #region Single Die Scoring Tests

    [Test]
    [TestCase(new[] { 1 }, ExpectedResult = 100)]
    [TestCase(new[] { 1, 2 }, ExpectedResult = 100)]
    [TestCase(new[] { 1, 3 }, ExpectedResult = 100)]
    [TestCase(new[] { 1, 4 }, ExpectedResult = 100)]
    [TestCase(new[] { 1, 6 }, ExpectedResult = 100)]
    public int TestSingleOne(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    [Test]
    [TestCase(new[] { 5 }, ExpectedResult = 50)]
    [TestCase(new[] { 5, 2 }, ExpectedResult = 50)]
    [TestCase(new[] { 5, 3 }, ExpectedResult = 50)]
    [TestCase(new[] { 5, 4 }, ExpectedResult = 50)]
    [TestCase(new[] { 5, 6 }, ExpectedResult = 50)]
    public int TestSingleFive(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    [Test]
    [TestCase(new[] { 1, 5 }, ExpectedResult = 150)]
    [TestCase(new[] { 1, 1 }, ExpectedResult = 200)]
    [TestCase(new[] { 5, 5 }, ExpectedResult = 100)]
    [TestCase(new[] { 1, 1, 5 }, ExpectedResult = 250)]
    [TestCase(new[] { 1, 5, 5 }, ExpectedResult = 200)]
    public int TestMultipleSingles(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    #endregion

    #region Three of a Kind Tests

    [Test]
    public void TestThreeOnes()
    {
        var dice = new[] { 1, 1, 1 };
        Assert.That(STFarkleDiceScoring.CalculateScore(dice), Is.EqualTo(1000));
    }

    [Test]
    [TestCase(new[] { 2, 2, 2 }, ExpectedResult = 200)]
    [TestCase(new[] { 3, 3, 3 }, ExpectedResult = 300)]
    [TestCase(new[] { 4, 4, 4 }, ExpectedResult = 400)]
    [TestCase(new[] { 5, 5, 5 }, ExpectedResult = 500)]
    [TestCase(new[] { 6, 6, 6 }, ExpectedResult = 600)]
    public int TestThreeOfAKind(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    [Test]
    [TestCase(new[] { 2, 2, 2, 3 }, ExpectedResult = 200)]
    [TestCase(new[] { 3, 3, 3, 4 }, ExpectedResult = 300)]
    [TestCase(new[] { 4, 4, 4, 2 }, ExpectedResult = 400)]
    [TestCase(new[] { 6, 6, 6, 2, 3 }, ExpectedResult = 600)]
    public int TestThreeOfAKindWithNonScoringExtras(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    #endregion

    #region Four/Five/Six of a Kind Tests (Doubling)

    [Test]
    [TestCase(new[] { 1, 1, 1, 1 }, ExpectedResult = 2000)]
    [TestCase(new[] { 2, 2, 2, 2 }, ExpectedResult = 400)]
    [TestCase(new[] { 3, 3, 3, 3 }, ExpectedResult = 600)]
    [TestCase(new[] { 4, 4, 4, 4 }, ExpectedResult = 800)]
    [TestCase(new[] { 5, 5, 5, 5 }, ExpectedResult = 1000)]
    [TestCase(new[] { 6, 6, 6, 6 }, ExpectedResult = 1200)]
    public int TestFourOfAKind(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    [Test]
    [TestCase(new[] { 1, 1, 1, 1, 1 }, ExpectedResult = 4000)]
    [TestCase(new[] { 2, 2, 2, 2, 2 }, ExpectedResult = 800)]
    [TestCase(new[] { 3, 3, 3, 3, 3 }, ExpectedResult = 1200)]
    [TestCase(new[] { 4, 4, 4, 4, 4 }, ExpectedResult = 1600)]
    [TestCase(new[] { 5, 5, 5, 5, 5 }, ExpectedResult = 2000)]
    [TestCase(new[] { 6, 6, 6, 6, 6 }, ExpectedResult = 2400)]
    public int TestFiveOfAKind(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    [Test]
    [TestCase(new[] { 1, 1, 1, 1, 1, 1 }, ExpectedResult = 8000)]
    [TestCase(new[] { 2, 2, 2, 2, 2, 2 }, ExpectedResult = 1600)]
    [TestCase(new[] { 3, 3, 3, 3, 3, 3 }, ExpectedResult = 2400)]
    [TestCase(new[] { 4, 4, 4, 4, 4, 4 }, ExpectedResult = 3200)]
    [TestCase(new[] { 5, 5, 5, 5, 5, 5 }, ExpectedResult = 4000)]
    [TestCase(new[] { 6, 6, 6, 6, 6, 6 }, ExpectedResult = 4800)]
    public int TestSixOfAKind(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    #endregion

    #region Straight Tests

    [Test]
    public void TestFullStraight()
    {
        var dice = new[] { 1, 2, 3, 4, 5, 6 };
        Assert.That(STFarkleDiceScoring.CalculateScore(dice), Is.EqualTo(1500));
    }

    [Test]
    [TestCase(new[] { 6, 5, 4, 3, 2, 1 }, ExpectedResult = 1500)]
    [TestCase(new[] { 3, 1, 4, 6, 2, 5 }, ExpectedResult = 1500)]
    [TestCase(new[] { 2, 4, 6, 1, 3, 5 }, ExpectedResult = 1500)]
    public int TestFullStraightAnyOrder(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    [Test]
    public void TestLowPartialStraight()
    {
        var dice = new[] { 1, 2, 3, 4, 5 };
        Assert.That(STFarkleDiceScoring.CalculateScore(dice), Is.EqualTo(500));
    }

    [Test]
    public void TestHighPartialStraight()
    {
        var dice = new[] { 2, 3, 4, 5, 6 };
        Assert.That(STFarkleDiceScoring.CalculateScore(dice), Is.EqualTo(750));
    }

    [Test]
    [TestCase(new[] { 5, 4, 3, 2, 1 }, ExpectedResult = 500)]
    [TestCase(new[] { 3, 1, 5, 2, 4 }, ExpectedResult = 500)]
    [TestCase(new[] { 6, 5, 4, 3, 2 }, ExpectedResult = 750)]
    [TestCase(new[] { 4, 2, 6, 3, 5 }, ExpectedResult = 750)]
    public int TestPartialStraightsAnyOrder(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    [Test]
    public void TestInvalidPartialStraight()
    {
        // 1,2,3,4,6 is NOT a valid partial straight (missing 5)
        var dice = new[] { 1, 2, 3, 4, 6 };
        // Should only score the single 1 = 100
        Assert.That(STFarkleDiceScoring.CalculateScore(dice), Is.EqualTo(100));
    }

    #endregion

    #region Mixed Combination Tests

    [Test]
    [TestCase(new[] { 2, 2, 2, 1 }, ExpectedResult = 300)]
    [TestCase(new[] { 2, 2, 2, 5 }, ExpectedResult = 250)]
    [TestCase(new[] { 2, 2, 2, 1, 5 }, ExpectedResult = 350)]
    [TestCase(new[] { 3, 3, 3, 1, 1 }, ExpectedResult = 500)]
    [TestCase(new[] { 4, 4, 4, 5, 5 }, ExpectedResult = 500)]
    [TestCase(new[] { 6, 6, 6, 1, 5 }, ExpectedResult = 750)]
    public int TestThreeOfAKindPlusSingles(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    [Test]
    [TestCase(new[] { 2, 2, 2, 3, 3, 3 }, ExpectedResult = 500)]
    [TestCase(new[] { 4, 4, 4, 6, 6, 6 }, ExpectedResult = 1000)]
    [TestCase(new[] { 1, 1, 1, 5, 5, 5 }, ExpectedResult = 1500)]
    public int TestTwoTriples(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    [Test]
    [TestCase(new[] { 2, 2, 2, 2, 1 }, ExpectedResult = 500)]
    [TestCase(new[] { 3, 3, 3, 3, 5 }, ExpectedResult = 650)]
    [TestCase(new[] { 4, 4, 4, 4, 1, 5 }, ExpectedResult = 950)]
    public int TestFourOfAKindPlusSingles(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    #endregion

    #region Farkle (No Score) Tests

    [Test]
    [TestCase(new[] { 2 }, ExpectedResult = 0)]
    [TestCase(new[] { 3 }, ExpectedResult = 0)]
    [TestCase(new[] { 4 }, ExpectedResult = 0)]
    [TestCase(new[] { 6 }, ExpectedResult = 0)]
    [TestCase(new[] { 2, 3 }, ExpectedResult = 0)]
    [TestCase(new[] { 2, 4 }, ExpectedResult = 0)]
    [TestCase(new[] { 2, 6 }, ExpectedResult = 0)]
    [TestCase(new[] { 3, 4 }, ExpectedResult = 0)]
    [TestCase(new[] { 3, 6 }, ExpectedResult = 0)]
    [TestCase(new[] { 4, 6 }, ExpectedResult = 0)]
    [TestCase(new[] { 2, 3, 4 }, ExpectedResult = 0)]
    [TestCase(new[] { 2, 3, 6 }, ExpectedResult = 0)]
    [TestCase(new[] { 2, 4, 6 }, ExpectedResult = 0)]
    [TestCase(new[] { 3, 4, 6 }, ExpectedResult = 0)]
    [TestCase(new[] { 2, 3, 4, 6 }, ExpectedResult = 0)]
    [TestCase(new[] { 2, 2, 3, 3, 4, 6 }, ExpectedResult = 0)]
    [TestCase(new[] { 2, 2, 4, 4, 6, 6 }, ExpectedResult = 0)]
    public int TestFarkle(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void TestEmptyArray()
    {
        var dice = System.Array.Empty<int>();
        Assert.That(STFarkleDiceScoring.CalculateScore(dice), Is.EqualTo(0));
    }

    [Test]
    [TestCase(new[] { 2 }, ExpectedResult = 0)]
    [TestCase(new[] { 3 }, ExpectedResult = 0)]
    [TestCase(new[] { 4 }, ExpectedResult = 0)]
    [TestCase(new[] { 6 }, ExpectedResult = 0)]
    public int TestSingleNonScoringDie(int[] dice)
    {
        return STFarkleDiceScoring.CalculateScore(dice);
    }

    #endregion

    #region IsValidSelection Tests

    [Test]
    [TestCase(new[] { 1 }, ExpectedResult = true)]
    [TestCase(new[] { 5 }, ExpectedResult = true)]
    [TestCase(new[] { 1, 5 }, ExpectedResult = true)]
    [TestCase(new[] { 1, 1, 1 }, ExpectedResult = true)]
    [TestCase(new[] { 2, 2, 2 }, ExpectedResult = true)]
    [TestCase(new[] { 1, 2, 3, 4, 5, 6 }, ExpectedResult = true)]
    public bool TestIsValidSelection_Valid(int[] dice)
    {
        return STFarkleDiceScoring.IsValidSelection(dice);
    }

    [Test]
    [TestCase(new[] { 2 }, ExpectedResult = false)]
    [TestCase(new[] { 3 }, ExpectedResult = false)]
    [TestCase(new[] { 4 }, ExpectedResult = false)]
    [TestCase(new[] { 6 }, ExpectedResult = false)]
    [TestCase(new[] { 2, 3 }, ExpectedResult = false)]
    [TestCase(new[] { 2, 4, 6 }, ExpectedResult = false)]
    public bool TestIsValidSelection_Invalid(int[] dice)
    {
        return STFarkleDiceScoring.IsValidSelection(dice);
    }

    [Test]
    public void TestIsValidSelection_Empty()
    {
        var dice = System.Array.Empty<int>();
        Assert.That(STFarkleDiceScoring.IsValidSelection(dice), Is.False);
    }

    #endregion

    #region GetScoringDice Tests

    [Test]
    public void TestGetScoringDice_Ones()
    {
        var dice = new[] { 1, 2, 3, 4, 6, 2 };
        var kept = new[] { false, false, false, false, false, false };

        var result = STFarkleDiceScoring.GetScoringDice(dice, kept);

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.True, "Die at index 0 (value 1) should be scoring");
            Assert.That(result[1], Is.False, "Die at index 1 (value 2) should not be scoring");
            Assert.That(result[2], Is.False, "Die at index 2 (value 3) should not be scoring");
            Assert.That(result[3], Is.False, "Die at index 3 (value 4) should not be scoring");
            Assert.That(result[4], Is.False, "Die at index 4 (value 6) should not be scoring");
            Assert.That(result[5], Is.False, "Die at index 5 (value 2) should not be scoring");
        });
    }

    [Test]
    public void TestGetScoringDice_Fives()
    {
        var dice = new[] { 2, 5, 3, 5, 6, 4 };
        var kept = new[] { false, false, false, false, false, false };

        var result = STFarkleDiceScoring.GetScoringDice(dice, kept);

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.False, "Die at index 0 (value 2) should not be scoring");
            Assert.That(result[1], Is.True, "Die at index 1 (value 5) should be scoring");
            Assert.That(result[2], Is.False, "Die at index 2 (value 3) should not be scoring");
            Assert.That(result[3], Is.True, "Die at index 3 (value 5) should be scoring");
            Assert.That(result[4], Is.False, "Die at index 4 (value 6) should not be scoring");
            Assert.That(result[5], Is.False, "Die at index 5 (value 4) should not be scoring");
        });
    }

    [Test]
    public void TestGetScoringDice_ThreeOfAKind()
    {
        var dice = new[] { 2, 2, 2, 3, 4, 6 };
        var kept = new[] { false, false, false, false, false, false };

        var result = STFarkleDiceScoring.GetScoringDice(dice, kept);

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.True, "Die at index 0 (value 2) should be scoring (part of triple)");
            Assert.That(result[1], Is.True, "Die at index 1 (value 2) should be scoring (part of triple)");
            Assert.That(result[2], Is.True, "Die at index 2 (value 2) should be scoring (part of triple)");
            Assert.That(result[3], Is.False, "Die at index 3 (value 3) should not be scoring");
            Assert.That(result[4], Is.False, "Die at index 4 (value 4) should not be scoring");
            Assert.That(result[5], Is.False, "Die at index 5 (value 6) should not be scoring");
        });
    }

    [Test]
    public void TestGetScoringDice_KeptDiceExcluded()
    {
        // Using dice {1, 1, 2, 3, 4, 6} to avoid forming a partial straight
        // Available dice will be {1, 2, 3, 4, 6} which is NOT a valid straight
        var dice = new[] { 1, 1, 2, 3, 4, 6 };
        var kept = new[] { true, false, false, false, false, false };

        var result = STFarkleDiceScoring.GetScoringDice(dice, kept);

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.False, "Die at index 0 should be excluded (kept)");
            Assert.That(result[1], Is.True, "Die at index 1 (value 1) should be scoring");
            Assert.That(result[2], Is.False, "Die at index 2 (value 2) should not be scoring");
            Assert.That(result[3], Is.False, "Die at index 3 (value 3) should not be scoring");
            Assert.That(result[4], Is.False, "Die at index 4 (value 4) should not be scoring");
            Assert.That(result[5], Is.False, "Die at index 5 (value 6) should not be scoring");
        });
    }

    [Test]
    public void TestGetScoringDice_FullStraight()
    {
        var dice = new[] { 3, 1, 4, 6, 2, 5 };
        var kept = new[] { false, false, false, false, false, false };

        var result = STFarkleDiceScoring.GetScoringDice(dice, kept);

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.True, "Die at index 0 should be scoring (part of straight)");
            Assert.That(result[1], Is.True, "Die at index 1 should be scoring (part of straight)");
            Assert.That(result[2], Is.True, "Die at index 2 should be scoring (part of straight)");
            Assert.That(result[3], Is.True, "Die at index 3 should be scoring (part of straight)");
            Assert.That(result[4], Is.True, "Die at index 4 should be scoring (part of straight)");
            Assert.That(result[5], Is.True, "Die at index 5 should be scoring (part of straight)");
        });
    }

    [Test]
    public void TestGetScoringDice_Farkle()
    {
        var dice = new[] { 2, 3, 4, 6, 2, 3 };
        var kept = new[] { false, false, false, false, false, false };

        var result = STFarkleDiceScoring.GetScoringDice(dice, kept);

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.False);
            Assert.That(result[1], Is.False);
            Assert.That(result[2], Is.False);
            Assert.That(result[3], Is.False);
            Assert.That(result[4], Is.False);
            Assert.That(result[5], Is.False);
        });
    }

    #endregion

    #region Comprehensive Data-Driven Tests

    public static IEnumerable<object[]> ComprehensiveScoringData()
    {
        // Singles
        yield return new object[] { new[] { 1 }, 100, "Single 1" };
        yield return new object[] { new[] { 5 }, 50, "Single 5" };
        yield return new object[] { new[] { 1, 1 }, 200, "Two 1s" };
        yield return new object[] { new[] { 5, 5 }, 100, "Two 5s" };
        yield return new object[] { new[] { 1, 5 }, 150, "One 1 and one 5" };

        // Three of a kind
        yield return new object[] { new[] { 1, 1, 1 }, 1000, "Three 1s" };
        yield return new object[] { new[] { 2, 2, 2 }, 200, "Three 2s" };
        yield return new object[] { new[] { 3, 3, 3 }, 300, "Three 3s" };
        yield return new object[] { new[] { 4, 4, 4 }, 400, "Three 4s" };
        yield return new object[] { new[] { 5, 5, 5 }, 500, "Three 5s" };
        yield return new object[] { new[] { 6, 6, 6 }, 600, "Three 6s" };

        // Four of a kind (doubled)
        yield return new object[] { new[] { 1, 1, 1, 1 }, 2000, "Four 1s" };
        yield return new object[] { new[] { 2, 2, 2, 2 }, 400, "Four 2s" };
        yield return new object[] { new[] { 5, 5, 5, 5 }, 1000, "Four 5s" };

        // Five of a kind (4x)
        yield return new object[] { new[] { 1, 1, 1, 1, 1 }, 4000, "Five 1s" };
        yield return new object[] { new[] { 3, 3, 3, 3, 3 }, 1200, "Five 3s" };

        // Six of a kind (8x)
        yield return new object[] { new[] { 1, 1, 1, 1, 1, 1 }, 8000, "Six 1s" };
        yield return new object[] { new[] { 4, 4, 4, 4, 4, 4 }, 3200, "Six 4s" };

        // Straights
        yield return new object[] { new[] { 1, 2, 3, 4, 5, 6 }, 1500, "Full straight" };
        yield return new object[] { new[] { 1, 2, 3, 4, 5 }, 500, "Low partial straight" };
        yield return new object[] { new[] { 2, 3, 4, 5, 6 }, 750, "High partial straight" };

        // Mixed combinations
        yield return new object[] { new[] { 1, 1, 1, 5 }, 1050, "Three 1s + single 5" };
        yield return new object[] { new[] { 2, 2, 2, 1, 5 }, 350, "Three 2s + 1 + 5" };
        yield return new object[] { new[] { 3, 3, 3, 4, 4, 4 }, 700, "Two separate triples" };
        yield return new object[] { new[] { 1, 1, 1, 2, 2, 2 }, 1200, "Triple 1s + triple 2s" };

        // Farkle (no score)
        yield return new object[] { new[] { 2, 3, 4, 6 }, 0, "Farkle - no scoring dice" };
        yield return new object[] { new[] { 2, 2, 3, 3, 4, 4 }, 0, "Farkle - three pairs" };
    }

    [Test]
    [TestCaseSource(nameof(ComprehensiveScoringData))]
    public void TestComprehensiveScoring(int[] dice, int expectedScore, string description)
    {
        var result = STFarkleDiceScoring.CalculateScore(dice);
        Assert.That(result, Is.EqualTo(expectedScore), $"Failed for: {description}");
    }

    #endregion
}
