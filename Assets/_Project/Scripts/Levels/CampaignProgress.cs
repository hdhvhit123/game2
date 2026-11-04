using UnityEngine;

/// <summary>Nơi lưu tiến độ; mặc định là PlayerPrefs, test dùng bộ nhớ tạm.</summary>
public interface IProgressStore
{
    int GetInt(string key, int defaultValue);
    void SetInt(string key, int value);
    void Save();
}

public sealed class PlayerPrefsStore : IProgressStore
{
    public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
    public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
    public void Save() => PlayerPrefs.Save();
}

/// <summary>Số sao tốt nhất của từng màn và màn nào đã mở khoá (qua màn trước là mở màn sau).</summary>
public static class CampaignProgress
{
    public static IProgressStore Store = new PlayerPrefsStore();

    private static string Key(string levelId) => "stars_" + levelId;

    public static int BestStars(string levelId) => Store.GetInt(Key(levelId), 0);

    public static bool IsUnlocked(int index)
    {
        if (index <= 0) return true;
        if (index >= CampaignCatalog.Levels.Count) return false;
        return BestStars(CampaignCatalog.Levels[index - 1].Id) > 0;
    }

    /// <summary>Ghi kết quả; chỉ giữ số sao cao nhất. Trả về true nếu là kỷ lục mới.</summary>
    public static bool Record(string levelId, int stars)
    {
        if (string.IsNullOrEmpty(levelId) || stars <= 0) return false;
        stars = Mathf.Clamp(stars, 1, 3);
        if (stars <= BestStars(levelId)) return false;
        Store.SetInt(Key(levelId), stars);
        Store.Save();
        return true;
    }

    public static int TotalStars()
    {
        int total = 0;
        foreach (LevelInfo l in CampaignCatalog.Levels) total += BestStars(l.Id);
        return total;
    }
}
