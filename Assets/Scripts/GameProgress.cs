public enum LevelGrade
{
    None,
    Purple,
    Orange,
    Green
}

public static class GameProgress
{
    public static bool[,] unlocked = new bool[3, 3];
    public static LevelGrade[,] grades = new LevelGrade[3, 3];

    static GameProgress()
    {
        ResetProgress();
    }

    public static void ResetProgress()
    {
        for (int c = 0; c < 3; c++)
        {
            for (int l = 0; l < 3; l++)
            {
                unlocked[c, l] = true; // GEÇİCİ OLARAK TEST İÇİN TÜM LEVELLERİ AÇTIK
                grades[c, l] = LevelGrade.None;
            }
        }
    }

    public static bool IsUnlocked(int chapter, int level)
    {
        return unlocked[chapter - 1, level - 1];
    }

    public static LevelGrade GetGrade(int chapter, int level)
    {
        return grades[chapter - 1, level - 1];
    }

    public static void CompleteLevel(int chapter, int level, float time)
    {
        LevelGrade newGrade = GetGradeFromTime(time);
        LevelGrade oldGrade = grades[chapter - 1, level - 1];

        // Keep best result only
        if ((int)newGrade > (int)oldGrade)
        {
            grades[chapter - 1, level - 1] = newGrade;
        }

        UnlockNext(chapter, level);
    }

    private static LevelGrade GetGradeFromTime(float time)
    {
        if (time < 15f) return LevelGrade.Green;
        if (time <= 35f) return LevelGrade.Orange;
        return LevelGrade.Purple;
    }

    private static void UnlockNext(int chapter, int level)
    {
        if (level < 3)
        {
            unlocked[chapter - 1, level] = true;
        }
        else if (chapter < 3)
        {
            unlocked[chapter, 0] = true;
        }
    }
}