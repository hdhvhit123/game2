using System.Collections.Generic;

/// <summary>Thông tin hiển thị một màn trong chế độ chiến dịch.</summary>
public sealed class LevelInfo
{
    public readonly string Id;
    public readonly string Title;
    public readonly string Tagline;
    /// <summary>Đường dẫn scene, phải có trong File &gt; Build Profiles (Scene List).</summary>
    public readonly string ScenePath;

    public LevelInfo(string id, string title, string tagline, string scenePath)
    {
        Id = id; Title = title; Tagline = tagline; ScenePath = scenePath;
    }
}

/// <summary>
/// Danh sách màn theo thứ tự chơi. Kịch bản chi tiết từng màn nằm trong LEVELS.md.
/// Thêm màn mới: tạo scene, đặt LevelManager với levelId trùng Id ở đây, thêm 1 dòng vào danh sách.
/// </summary>
public static class CampaignCatalog
{
    private const string Dir = "Assets/_Project/Scenes/";

    public static readonly IReadOnlyList<LevelInfo> Levels = new[]
    {
        new LevelInfo("L01_KhiengLau",  "Khiêng lẩu đầu năm",   "Đưa nồi lẩu nóng tới bàn khách, đừng để sánh giọt nào.", Dir + "L01_KhiengLau.unity"),
        new LevelInfo("L02_BatGa",      "Bắt gà khai trương",   "Gà sổng chuồng! Bắt đủ 5 con trước giờ khách tới.",       Dir + "L02_BatGa.unity"),
        new LevelInfo("L03_CauKhi",     "Qua cầu khỉ miền Tây", "Cầu lắc lư, ghe chạy ngang. Cả hai phải sang bờ bên kia.", Dir + "L03_CauKhi.unity"),
        new LevelInfo("L04_CuaHaiChia", "Cửa hai chìa",          "Mỗi cánh cổng cần cả hai cùng đứng lên bàn đạp.",         Dir + "L04_CuaHaiChia.unity"),
        new LevelInfo("L05_ChayLu",     "Chạy lũ",               "Nước đang lên! Leo lên mái nhà trước khi bị cuốn trôi.",  Dir + "L05_ChayLu.unity"),
        new LevelInfo("L06_BanhCuoi",   "Bánh cưới bảy tầng",   "Khiêng bánh cưới qua dãy quạt trần đang quay.",           Dir + "L06_BanhCuoi.unity"),
        new LevelInfo("L07_GomXoai",    "Gom xoài mùa gió",     "Gió giật từng cơn, hứng 12 trái xoài vào sọt.",           Dir + "L07_GomXoai.unity"),
        new LevelInfo("L08_XeBaGac",    "Đẩy xe ba gác lên dốc", "Xe nặng, một người kéo không nổi. Đưa xe lên đỉnh dốc.",   Dir + "L08_XeBaGac.unity"),
    };

    public static int IndexOf(string id)
    {
        for (int i = 0; i < Levels.Count; i++) if (Levels[i].Id == id) return i;
        return -1;
    }

    public static LevelInfo Next(string id)
    {
        int i = IndexOf(id);
        return i >= 0 && i + 1 < Levels.Count ? Levels[i + 1] : null;
    }
}
