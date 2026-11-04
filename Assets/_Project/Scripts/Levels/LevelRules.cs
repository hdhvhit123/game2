using System;

public enum LevelPhase
{
    Intro = 0,     // hiện tên màn + nhiệm vụ, người chơi chưa điều khiển được
    Playing = 1,
    Won = 2,
    Lost = 3
}

/// <summary>Trạng thái một nhiệm vụ tại một thời điểm (dữ liệu thuần, để LevelRules quyết định).</summary>
public struct ObjectiveState
{
    public bool Required;
    public bool Complete;
    public bool Failed;
    /// <summary>1..3: điểm sao riêng của nhiệm vụ (ví dụ lượng nước lẩu còn lại). 3 nếu không chấm.</summary>
    public int Stars;
}

/// <summary>
/// Luật thắng/thua và chấm sao của một màn, tách khỏi Fusion để kiểm thử được.
/// </summary>
public static class LevelRules
{
    public static LevelPhase Evaluate(LevelPhase phase, float elapsed, float timeLimit, ObjectiveState[] objectives, int count)
    {
        if (phase != LevelPhase.Playing) return phase;

        bool anyRequired = false;
        bool allRequiredDone = true;
        for (int i = 0; i < count; i++)
        {
            ObjectiveState o = objectives[i];
            if (o.Failed && o.Required) return LevelPhase.Lost;
            if (!o.Required) continue;
            anyRequired = true;
            if (!o.Complete) allRequiredDone = false;
        }

        if (anyRequired && allRequiredDone) return LevelPhase.Won;
        if (timeLimit > 0f && elapsed >= timeLimit) return LevelPhase.Lost;
        return LevelPhase.Playing;
    }

    /// <summary>Sao theo thời gian: nhanh hơn threeStarTime được 3, nhanh hơn twoStarTime được 2, còn lại 1.</summary>
    public static int TimeStars(float elapsed, float threeStarTime, float twoStarTime)
    {
        if (threeStarTime > 0f && elapsed <= threeStarTime) return 3;
        if (twoStarTime > 0f && elapsed <= twoStarTime) return 2;
        if (threeStarTime <= 0f && twoStarTime <= 0f) return 3;
        return 1;
    }

    /// <summary>Sao cuối = thấp nhất giữa sao thời gian và sao của các nhiệm vụ bắt buộc; rơi nhiều lần bị trừ.</summary>
    public static int FinalStars(float elapsed, float threeStarTime, float twoStarTime,
                                 ObjectiveState[] objectives, int count, int falls, int fallsPerStarLost)
    {
        int stars = TimeStars(elapsed, threeStarTime, twoStarTime);
        for (int i = 0; i < count; i++)
            if (objectives[i].Required) stars = Math.Min(stars, Clamp(objectives[i].Stars, 1, 3));
        if (fallsPerStarLost > 0) stars -= falls / fallsPerStarLost;
        return Clamp(stars, 1, 3);
    }

    private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
}
