namespace Content.Shared._Stalker_EN.FarkleDice;

/// <summary>
/// Farkle scoring rules: 1s=100, 5s=50, three-of-a-kind=value*100 (1s=1000),
/// additional matching dice double the score. Straights score 500-1500.
/// </summary>
public static class STFarkleDiceScoring
{
    public static int CalculateScore(int[] diceValues)
    {
        if (diceValues.Length == 0)
            return 0;

        var counts = new int[7];
        foreach (var value in diceValues)
        {
            if (value >= 1 && value <= 6)
                counts[value]++;
        }

        var score = 0;

        if (diceValues.Length == 6 && IsFullStraight(counts))
            return 1500;

        if (diceValues.Length == 5 && IsPartialStraight(counts, out var partialScore))
            return partialScore;

        for (var dieValue = 1; dieValue <= 6; dieValue++)
        {
            var count = counts[dieValue];

            if (count >= 3)
            {
                var baseScore = dieValue == 1 ? 1000 : dieValue * 100;

                // Each die beyond 3 doubles the score
                var multiplier = 1 << (count - 3);
                score += baseScore * multiplier;

                counts[dieValue] = 0;
            }
        }

        score += counts[1] * 100;
        score += counts[5] * 50;

        return score;
    }

    public static bool IsValidSelection(int[] diceValues)
    {
        return AreAllDiceScoring(diceValues);
    }

    /// <summary>
    /// Returns true if every die in the selection contributes to the total score.
    /// A selection like [1, 4, 4] returns false because the 4s do not contribute.
    /// This prevents players from "hiding" non-scoring dice in a valid selection.
    /// </summary>
    public static bool AreAllDiceScoring(int[] diceValues)
    {
        if (diceValues.Length == 0)
            return false;

        var totalScore = CalculateScore(diceValues);
        if (totalScore == 0)
            return false;

        for (var i = 0; i < diceValues.Length; i++)
        {
            var reduced = new int[diceValues.Length - 1];
            var idx = 0;
            for (var j = 0; j < diceValues.Length; j++)
            {
                if (j != i)
                    reduced[idx++] = diceValues[j];
            }

            if (CalculateScore(reduced) == totalScore)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a mask indicating which available dice can contribute to a scoring combination.
    /// Used for visual hints to help players identify valid moves.
    /// </summary>
    public static bool[] GetScoringDice(int[] allDiceValues, bool[] keptDice)
    {
        var result = new bool[6];

        // Count available dice (not kept)
        var availableCounts = new int[7];
        var availableIndices = new List<int>[7];
        for (var i = 0; i < 7; i++)
            availableIndices[i] = new List<int>();

        for (var i = 0; i < 6; i++)
        {
            if (keptDice[i])
                continue;

            var value = allDiceValues[i];
            if (value >= 1 && value <= 6)
            {
                availableCounts[value]++;
                availableIndices[value].Add(i);
            }
        }

        // Check for full straight among available dice
        var availableCount = 0;
        for (var i = 1; i <= 6; i++)
            availableCount += availableCounts[i];

        if (availableCount == 6 && IsFullStraight(availableCounts))
        {
            // All available dice are part of the straight
            for (var i = 0; i < 6; i++)
            {
                if (!keptDice[i])
                    result[i] = true;
            }
            return result;
        }

        // Check for partial straights (exactly 5 available dice)
        if (availableCount == 5 && IsPartialStraight(availableCounts, out _))
        {
            for (var i = 0; i < 6; i++)
            {
                if (!keptDice[i])
                    result[i] = true;
            }
            return result;
        }

        // Mark dice that are part of three-or-more-of-a-kind
        for (var dieValue = 1; dieValue <= 6; dieValue++)
        {
            if (availableCounts[dieValue] >= 3)
            {
                foreach (var index in availableIndices[dieValue])
                    result[index] = true;
            }
        }

        // Mark single 1s and 5s as scoring (if not already part of a triplet)
        foreach (var index in availableIndices[1])
        {
            result[index] = true;
        }

        foreach (var index in availableIndices[5])
        {
            result[index] = true;
        }

        return result;
    }

    private static bool IsFullStraight(int[] counts)
    {
        for (var i = 1; i <= 6; i++)
        {
            if (counts[i] != 1)
                return false;
        }
        return true;
    }

    private static bool IsPartialStraight(int[] counts, out int score)
    {
        score = 0;

        var total = 0;
        for (var i = 1; i <= 6; i++)
        {
            // Duplicates disqualify partial straight
            if (counts[i] > 1)
                return false;
            total += counts[i];
        }

        if (total != 5)
            return false;

        // Low straight (1-2-3-4-5)
        if (counts[1] == 1 && counts[2] == 1 && counts[3] == 1 && counts[4] == 1 && counts[5] == 1 && counts[6] == 0)
        {
            score = 500;
            return true;
        }

        // High straight (2-3-4-5-6)
        if (counts[1] == 0 && counts[2] == 1 && counts[3] == 1 && counts[4] == 1 && counts[5] == 1 && counts[6] == 1)
        {
            score = 750;
            return true;
        }

        return false;
    }
}
