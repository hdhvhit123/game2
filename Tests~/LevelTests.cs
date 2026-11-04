using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Kiểm thử khung màn chơi: luật thắng/thua, chấm sao, quỹ đạo bệ di động, gió, lưu tiến độ.</summary>
public static class LevelTests
{
    static void Check(bool c, string n, string d = "") => Tests.Check(c, n, d);

    sealed class MemoryStore : IProgressStore
    {
        public readonly Dictionary<string, int> Data = new Dictionary<string, int>();
        public int Saves;
        public int GetInt(string k, int d) => Data.TryGetValue(k, out int v) ? v : d;
        public void SetInt(string k, int v) => Data[k] = v;
        public void Save() => Saves++;
    }

    public static void Run()
    {
        Console.WriteLine("== Luat man choi"); Rules();
        Console.WriteLine("== Cham sao"); StarTests();
        Console.WriteLine("== Be di dong / quat quay"); MoverTests();
        Console.WriteLine("== Gio giat"); WindTests();
        Console.WriteLine("== Danh sach man & tien do"); CampaignTests();
    }

    static ObjectiveState O(bool req, bool done, bool failed = false, int stars = 3) =>
        new ObjectiveState { Required = req, Complete = done, Failed = failed, Stars = stars };

    static void Rules()
    {
        var a = new[] { O(true, false), O(false, false) };
        Check(LevelRules.Evaluate(LevelPhase.Playing, 10, 60, a, 2) == LevelPhase.Playing, "Chua xong nhiem vu: van choi");
        a[0] = O(true, true);
        Check(LevelRules.Evaluate(LevelPhase.Playing, 10, 60, a, 2) == LevelPhase.Won, "Xong nhiem vu bat buoc la thang (nhiem vu phu khong can)");
        var b = new[] { O(true, true), O(true, false) };
        Check(LevelRules.Evaluate(LevelPhase.Playing, 10, 60, b, 2) == LevelPhase.Playing, "Can xong TAT CA nhiem vu bat buoc");
        Check(LevelRules.Evaluate(LevelPhase.Playing, 60, 60, b, 2) == LevelPhase.Lost, "Het gio thi thua");
        Check(LevelRules.Evaluate(LevelPhase.Playing, 9999, 0, b, 2) == LevelPhase.Playing, "timeLimit = 0: khong gioi han");
        var c = new[] { O(true, false, failed: true) };
        Check(LevelRules.Evaluate(LevelPhase.Playing, 1, 60, c, 1) == LevelPhase.Lost, "Nhiem vu bat buoc that bai thi thua ngay");
        var d = new[] { O(false, false, failed: true), O(true, false) };
        Check(LevelRules.Evaluate(LevelPhase.Playing, 1, 60, d, 2) == LevelPhase.Playing, "Nhiem vu phu that bai khong sao");
        var e = new[] { O(false, true) };
        Check(LevelRules.Evaluate(LevelPhase.Playing, 1, 60, e, 1) == LevelPhase.Playing, "Man khong co nhiem vu bat buoc thi khong tu thang");
        Check(LevelRules.Evaluate(LevelPhase.Intro, 999, 60, b, 2) == LevelPhase.Intro, "Pha gioi thieu khong bi danh gia");
        Check(LevelRules.Evaluate(LevelPhase.Won, 999, 60, c, 1) == LevelPhase.Won, "Da thang thi khong doi ket qua");
        Check(LevelRules.Evaluate(LevelPhase.Playing, 1, 60, b, 1) == LevelPhase.Won, "Chi xet so luong count truyen vao");
    }

    static void StarTests()
    {
        Check(LevelRules.TimeStars(80, 90, 140) == 3 && LevelRules.TimeStars(120, 90, 140) == 2 && LevelRules.TimeStars(200, 90, 140) == 1, "Sao theo thoi gian");
        Check(LevelRules.TimeStars(500, 0, 0) == 3, "Man khong cham thoi gian");
        var objs = new[] { O(true, true, stars: 2), O(false, true, stars: 1) };
        Check(LevelRules.FinalStars(50, 90, 140, objs, 2, 0, 3) == 2, "Sao = thap nhat cua nhiem vu bat buoc (bo qua nhiem vu phu)");
        var full = new[] { O(true, true) };
        Check(LevelRules.FinalStars(50, 90, 140, full, 1, 3, 3) == 2, "Roi 3 lan mat 1 sao");
        Check(LevelRules.FinalStars(200, 90, 140, full, 1, 99, 3) == 1, "Toi thieu van 1 sao khi da qua man");
        Check(LevelRules.FinalStars(50, 90, 140, full, 1, 99, 0) == 3, "fallsPerStarLost = 0: khong tru");
    }

    static void MoverTests()
    {
        var pts = new[] { Vector3.zero, new Vector3(0, 0, 6) };
        Vector3 p0 = MoverPath.Evaluate(pts, 0f, 2f, MoverPath.Mode.PingPong, 1f);
        Vector3 p1 = MoverPath.Evaluate(pts, 1.5f, 2f, MoverPath.Mode.PingPong, 1f);
        Vector3 pEnd = MoverPath.Evaluate(pts, 3.5f, 2f, MoverPath.Mode.PingPong, 1f);
        Vector3 pBack = MoverPath.Evaluate(pts, 5.5f, 2f, MoverPath.Mode.PingPong, 1f);
        Check(Near(p0.z, 0) && Near(p1.z, 3) && Near(pEnd.z, 6) && Near(pBack.z, 3), "PingPong: di, dung o dau, quay ve", $"({p1.z:F2}, {pEnd.z:F2}, {pBack.z:F2})");
        Vector3 pCycle = MoverPath.Evaluate(pts, 8f + 1.5f, 2f, MoverPath.Mode.PingPong, 1f);
        Check(Near(pCycle.z, 3), "PingPong lap lai dung chu ky 8s");

        var square = new[] { Vector3.zero, new Vector3(4, 0, 0), new Vector3(4, 0, 4), new Vector3(0, 0, 4) };
        Vector3 l = MoverPath.Evaluate(square, 5f, 2f, MoverPath.Mode.Loop);
        Vector3 lWrap = MoverPath.Evaluate(square, 8f + 5f, 2f, MoverPath.Mode.Loop);
        Check(Near(l.x, 2) && Near(l.z, 4) && Near(lWrap.x, 2) && Near(lWrap.z, 4), "Loop: chay vong kin", $"({l.x:F1},{l.z:F1})");
        Check(Near(MoverPath.PathLength(square, true), 16), "Do dai duong vong");

        bool continuous = true;
        Vector3 prev = MoverPath.Evaluate(square, 0, 2f, MoverPath.Mode.Loop);
        for (float t = 0.01f; t < 20f; t += 0.01f)
        {
            Vector3 cur = MoverPath.Evaluate(square, t, 2f, MoverPath.Mode.Loop);
            if (Vector3.Distance(cur, prev) > 0.05f) continuous = false;
            prev = cur;
        }
        Check(continuous, "Be di dong khong bi giat cuc (lien tuc moi 10ms)");
        Check(MoverPath.Evaluate(new[] { new Vector3(1, 2, 3) }, 5f, 2f, MoverPath.Mode.Loop).y == 2f, "Mot diem: dung yen");
        Check(MoverPath.Evaluate(pts, 5f, 0f, MoverPath.Mode.Loop).z == 0f, "Toc do 0: dung yen");

        float s1 = MoverPath.SpinAngle(1f, 90f, 0f), s5 = MoverPath.SpinAngle(5f, 90f, 0f), sNeg = MoverPath.SpinAngle(-1f, 90f, 0f);
        Check(Near(s1, 90) && Near(s5, 90) && Near(sNeg, 270), "Quat quay deu, goc trong [0,360)");
        float w0 = MoverPath.SwingAngle(0f, 60f, 4f, 0f), w1 = MoverPath.SwingAngle(1f, 60f, 4f, 0f), w3 = MoverPath.SwingAngle(3f, 60f, 4f, 0f);
        Check(Near(w0, 0) && Near(w1, 60) && Near(w3, -60), "Bua con lac dao dong +-60 do");
    }

    static void WindTests()
    {
        Check(WindVolume.GustAt(0.5f, 5f, 0.4f) == 0f, "Dau chu ky gio lang");
        float peak = WindVolume.GustAt(3.5f, 5f, 0.4f);
        Check(Near(peak, 1f), "Giua con gio manh nhat", $"({peak:F2})");
        Check(WindVolume.GustAt(8.5f, 5f, 0.4f) > 0.9f, "Chu ky lap lai");
        Check(WindVolume.GustAt(-1.5f, 5f, 0.4f) >= 0f, "Thoi gian am khong loi");
    }

    static void CampaignTests()
    {
        var store = new MemoryStore();
        CampaignProgress.Store = store;
        var levels = CampaignCatalog.Levels;

        var ids = new HashSet<string>();
        bool unique = true, paths = true;
        foreach (var l in levels)
        {
            unique &= ids.Add(l.Id);
            paths &= l.ScenePath.StartsWith("Assets/") && l.ScenePath.EndsWith(".unity") && !string.IsNullOrEmpty(l.Title);
        }
        Check(levels.Count >= 8 && unique && paths, "Danh sach man hop le", $"({levels.Count} man)");
        Check(CampaignCatalog.Next(levels[0].Id) == levels[1] && CampaignCatalog.Next(levels[levels.Count - 1].Id) == null, "Man ke tiep / man cuoi");

        Check(CampaignProgress.IsUnlocked(0) && !CampaignProgress.IsUnlocked(1), "Ban dau chi mo man 1");
        Check(CampaignProgress.Record(levels[0].Id, 2) && CampaignProgress.IsUnlocked(1), "Qua man 1 thi mo man 2");
        Check(!CampaignProgress.Record(levels[0].Id, 1) && CampaignProgress.BestStars(levels[0].Id) == 2, "Chi giu ky luc cao nhat");
        Check(CampaignProgress.Record(levels[0].Id, 5) && CampaignProgress.BestStars(levels[0].Id) == 3, "Gioi han 3 sao");
        Check(!CampaignProgress.Record(levels[1].Id, 0) && !CampaignProgress.IsUnlocked(2), "Thua (0 sao) khong mo man");
        Check(!CampaignProgress.IsUnlocked(levels.Count), "Chi so ngoai danh sach");
        Check(CampaignProgress.TotalStars() == 3 && store.Saves == 2, "Tong sao va so lan ghi", $"(ghi {store.Saves} lan)");
    }

    static bool Near(float a, float b) => Math.Abs(a - b) < 0.02f;
}
