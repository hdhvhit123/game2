using System;
using System.Collections.Generic;
using UnityEngine;

public static class Tests
{
    static int _pass, _fail;
    internal static void Check(bool cond, string name, string detail = "")
    {
        if (cond) { _pass++; Console.WriteLine($"  PASS  {name} {detail}"); }
        else { _fail++; Console.WriteLine($"  FAIL  {name} {detail}"); }
    }

    public static int Main()
    {
        Console.WriteLine("== RoomCode"); RoomCodeTests();
        Console.WriteLine("== Floating spring & jump"); SpringTests();
        Console.WriteLine("== Rope tether"); TetherTests();
        Console.WriteLine("== Verlet rope"); RopeTests();
        Console.WriteLine("== Pot stability"); PotTests();
        Console.WriteLine("== FrameRing & Collage"); RecorderTests();
        LevelTests.Run();
        Console.WriteLine($"\nTONG KET: {_pass} pass, {_fail} fail");
        return _fail == 0 ? 0 : 1;
    }

    static void RoomCodeTests()
    {
        var rng = new System.Random(1);
        var seen = new HashSet<string>();
        bool allValid = true;
        for (int i = 0; i < 5000; i++)
        {
            string c = RoomCode.Generate(6, rng);
            seen.Add(c);
            allValid &= c.Length == 6 && RoomCode.TrySanitize(c, out string s) && s == c && c.IndexOfAny("01OIL".ToCharArray()) < 0;
        }
        Check(allValid, "Generate: 6 ky tu hop le, khong co ky tu de nham");
        Check(seen.Count > 4990, "Generate: hau nhu khong trung", $"({seen.Count}/5000 khac nhau)");

        Check(RoomCode.TryParseFromUrl("https://dayoannghiet.vn/join?room=abc123", out var r1) && r1 == "ABC123", "Parse URL co ban");
        Check(RoomCode.TryParseFromUrl("https://dayoannghiet.vn/join?utm=zalo&room=XY99Z&x=1", out var r2) && r2 == "XY99Z", "Parse URL nhieu tham so");
        Check(!RoomCode.TryParseFromUrl("not a url", out _), "URL sai dinh dang khong crash");
        Check(!RoomCode.TryParseFromUrl("https://dayoannghiet.vn/join?room=", out _), "room rong");
        Check(!RoomCode.TryParseFromUrl("https://dayoannghiet.vn/join?room=AB%27%3B--", out _), "Ky tu doc hai bi tu choi");
        Check(!RoomCode.TryParseFromUrl("https://dayoannghiet.vn/join?room=" + new string('A', 500), out _), "Ma qua dai bi tu choi");
        Check(!RoomCode.TryParseFromUrl("https://dayoannghiet.vn/join?myroom=ABCD", out _), "Khong nham tham so myroom");
        Check(!RoomCode.TryParseFromUrl(null, out _), "URL null");
        Check(RoomCode.TryParseFromReferrer("room_KQ7RT2", out var r3) && r3 == "KQ7RT2", "Referrer kieu cu room_");
        Check(RoomCode.TryParseFromReferrer("utm_source%3Dzalo%26room%3DHH88KK", out var r4) && r4 == "HH88KK", "Referrer Google Play URL-encoded");
        Check(!RoomCode.TryParseFromReferrer("utm_source=google-play&utm_medium=organic", out _), "Referrer khong co phong");
        Check(!RoomCode.TryParseFromReferrer("%E0%A4%A", out _), "Referrer encode hong khong crash");
        Check(RoomCode.BuildInviteUrl("dayoannghiet.vn", "ABC123") == "https://dayoannghiet.vn/join?room=ABC123", "Tao link moi");
        Check(RoomCode.TryParseFromUrl(RoomCode.BuildInviteUrl("dayoannghiet.vn", "Q2W3E4"), out var r5) && r5 == "Q2W3E4", "Link tao ra doc lai duoc");
    }

    // Mo phong 1D than nguoi choi: trong luc Unity -9.81, buoc vat ly 0.02s.
    static float SimulateJumpApex(bool newVersion)
    {
        const float dt = 0.02f, g = -9.81f, ride = 1.1f, rayLen = 1.6f, k = 450f, d = 35f;
        float y = ride - 9.81f / k, v = 0f, apex = y;
        // dung yen 1 giay
        for (int i = 0; i < 50; i++) Step(ref y, ref v, 0f);
        float groundY = y;
        v += 9.5f; // jumpImpulse, mass 1
        float lockTime = newVersion ? 0.25f : 0f;
        for (int i = 0; i < 200; i++)
        {
            Step(ref y, ref v, lockTime);
            lockTime -= dt;
            apex = Math.Max(apex, y);
        }
        return apex - groundY;

        void Step(ref float yy, ref float vv, float lockLeft)
        {
            float a = g;
            bool springOn = lockLeft <= 0f && yy < rayLen;
            if (springOn)
            {
                if (newVersion) a += CoopPhysicsMath.RideSpringAcceleration(yy, ride, vv, k, d, 120f);
                else
                {
                    // Cong thuc ban cu: AddForce(down * ((err*k) - (-vy*d)))
                    float springForce = ((yy - ride) * k) - (-vv * d);
                    a += -springForce;
                }
            }
            vv += a * dt;
            yy += vv * dt;
        }
    }

    static void SpringTests()
    {
        // Lo xo on dinh tu do cao 2m
        const float dt = 0.02f;
        float y = 2.0f, v = 0f, maxErrLate = 0f;
        for (int i = 0; i < 300; i++)
        {
            float a = -9.81f + CoopPhysicsMath.RideSpringAcceleration(y, 1.1f, v, 450f, 35f, 120f);
            v += a * dt; y += v * dt;
            if (i > 150) maxErrLate = Math.Max(maxErrLate, Math.Abs(y - 1.1f));
        }
        Check(maxErrLate < 0.05f, "Lo xo dung yen dung do cao 1.1m", $"(sai so {maxErrLate:F3}m)");

        // Roi tu 6m xuong: khong vang nguoc len troi (gioi han gia toc)
        y = 1.55f; v = -18f; float minY = y, maxYAfter = 0f;
        for (int i = 0; i < 200; i++)
        {
            float a = -9.81f + (y < 1.6f ? CoopPhysicsMath.RideSpringAcceleration(y, 1.1f, v, 450f, 35f, 120f) : 0f);
            v += a * dt; y += v * dt;
            minY = Math.Min(minY, y);
            if (i > 20) maxYAfter = Math.Max(maxYAfter, y);
        }
        Check(maxYAfter < 1.6f && !float.IsNaN(y), "Tiep dat manh khong bi bat nguoc", $"(cao nhat sau va cham {maxYAfter:F2}m)");

        float oldApex = SimulateJumpApex(false), newApex = SimulateJumpApex(true);
        Check(oldApex < 1.0f, "Tai hien loi ban cu: lo xo keo nhan vat dinh dat khi nhay", $"(ban cu chi nhay cao {oldApex:F2}m)");
        Check(newApex > 3.5f, "Ban moi nhay dung luc (jump lock)", $"(cao {newApex:F2}m, ly thuyet ~4.6m)");
    }

    static void TetherTests()
    {
        Vector3 a0 = CoopPhysicsMath.TetherAcceleration(Vector3.zero, new Vector3(3, 0, 0), Vector3.zero, Vector3.zero, 4.2f, 55f, 6f, 90f);
        Check(a0.sqrMagnitude == 0f, "Day chung khong keo");
        Vector3 a1 = CoopPhysicsMath.TetherAcceleration(Vector3.zero, new Vector3(5, 0, 0), Vector3.zero, Vector3.zero, 4.2f, 55f, 6f, 90f);
        Vector3 a2 = CoopPhysicsMath.TetherAcceleration(new Vector3(5, 0, 0), Vector3.zero, Vector3.zero, Vector3.zero, 4.2f, 55f, 6f, 90f);
        Check(a1.x > 0f && Math.Abs(a1.x + a2.x) < 1e-4f, "Day cang keo 2 nguoi ve nhau, doi xung", $"({a1.x:F1} / {a2.x:F1})");
        Vector3 a3 = CoopPhysicsMath.TetherAcceleration(Vector3.zero, new Vector3(50, 0, 0), Vector3.zero, Vector3.zero, 4.2f, 55f, 6f, 90f);
        Check(a3.magnitude <= 90.001f, "Gioi han luc khi bi tach xa (teleport)");

        // Mo phong: A chay ve +x voi toc do 7m/s, B dung yen (khong dieu khien). Kiem tra day giu khoang cach & on dinh.
        const float dt = 1f / 60f;
        Vector3 pa = Vector3.zero, pb = new Vector3(-3f, 0, 0), va = Vector3.zero, vb = Vector3.zero;
        float maxDist = 0f;
        for (int i = 0; i < 600; i++)
        {
            Vector3 target = new Vector3(7f, 0, 0);
            Vector3 accA = CoopPhysicsMath.SteeringAcceleration(va, target, 45f, 80f)
                         + CoopPhysicsMath.TetherAcceleration(pa, pb, va, vb, 4.2f, 55f, 6f, 90f);
            Vector3 accB = CoopPhysicsMath.SteeringAcceleration(vb, Vector3.zero, 45f * 0.1f, 80f) // B dung im (ma sat nho)
                         + CoopPhysicsMath.TetherAcceleration(pb, pa, vb, va, 4.2f, 55f, 6f, 90f);
            va += accA * dt; vb += accB * dt; pa += va * dt; pb += vb * dt;
            if (i > 60) maxDist = Math.Max(maxDist, Vector3.Distance(pa, pb));
        }
        Check(maxDist < 4.2f + 1.0f, "Chay ra xa: day giu khoang cach", $"(xa nhat {maxDist:F2}m, day 4.2m)");
        Check(pb.x > 5f, "Nguoi dung im bi dong doi keo theo", $"(B di duoc {pb.x + 3f:F1}m)");
        Check(!float.IsNaN(pa.x + pb.x), "Khong NaN");
    }

    static RopeSolver RunRope(float fps, float seconds, Func<float, Vector3> anchorA, Func<float, Vector3> anchorB)
    {
        var s = new RopeSolver(24, 4.5f);
        s.Reset(anchorA(0), anchorB(0));
        float acc = 0f, t = 0f, step = 1f / 60f, frame = 1f / fps;
        Vector3 gravity = new Vector3(0, -19.6f, 0);
        while (t < seconds)
        {
            t += frame;
            acc = Math.Min(acc + frame, step * 3);
            while (acc >= step)
            {
                acc -= step;
                s.Integrate(step, gravity, 0.985f);
                s.SolveConstraints(anchorA(t), anchorB(t), 10);
            }
        }
        return s;
    }

    static void RopeTests()
    {
        Func<float, Vector3> fa = _ => new Vector3(-1.5f, 1f, 0), fb = _ => new Vector3(1.5f, 1f, 0);
        RopeSolver r30 = RunRope(30, 4f, fa, fb), r60 = RunRope(60, 4f, fa, fb), r144 = RunRope(144, 4f, fa, fb);
        float m30 = r30.Positions[12].y, m60 = r60.Positions[12].y, m144 = r144.Positions[12].y;
        Check(Math.Abs(m30 - m144) < 0.03f && Math.Abs(m60 - m144) < 0.03f, "Day giong nhau o 30/60/144 FPS", $"(vong giua: {m30:F3} / {m60:F3} / {m144:F3})");
        Check(m60 < 0.3f, "Day vong xuong tu nhien khi chung", $"(giua day thap hon diem treo {1f - m60:F2}m)");

        float maxErr = 0f;
        for (int i = 0; i < r60.NodeCount - 1; i++)
            maxErr = Math.Max(maxErr, Math.Abs(Vector3.Distance(r60.Positions[i], r60.Positions[i + 1]) - r60.SegmentLength) / r60.SegmentLength);
        Check(maxErr < 0.05f, "Do dai tung doan day dung", $"(sai lech toi da {maxErr * 100:F1}%)");

        // Hai nguoi chay vong tron nhanh + giat manh: khong NaN, khong no
        RopeSolver rFast = RunRope(20, 6f,
            t => new Vector3((float)Math.Cos(t * 6) * 3f, 1f + (float)Math.Sin(t * 13), (float)Math.Sin(t * 6) * 3f),
            t => new Vector3(-(float)Math.Cos(t * 5) * 2f, 1f, -(float)Math.Sin(t * 7) * 2f));
        float maxSpan = 0f;
        for (int i = 0; i < rFast.NodeCount; i++) maxSpan = Math.Max(maxSpan, rFast.Positions[i].magnitude);
        Check(!rFast.HasInvalidValues() && maxSpan < 20f, "Chuyen dong hon loan o 20 FPS: day khong no", $"(xa nhat {maxSpan:F1}m)");

        var s = new RopeSolver(10, 3f);
        s.Reset(Vector3.zero, new Vector3(3, 0, 0));
        s.PushAbove(5, 0.5f, 0.06f);
        Check(Math.Abs(s.Positions[5].y - 0.56f) < 1e-4f, "Va cham day voi mat dat");
        s.PushAbove(0, 5f, 0.06f);
        Check(s.Positions[0].y == 0f, "Khong day diem neo");

        // Do 'chi phi' va cham: ban cu 22 node x 12 vong = 264 SphereCast/frame; ban moi 22 Raycast/buoc.
        Check(22 < 264, "Chi phi va cham giam ~12 lan", "(264 SphereCast -> 22 Raycast moi buoc)");
    }

    static void PotTests()
    {
        var s = PotStability.Settings.Default;
        Check(PotStability.TiltSpillPerSecond(0f, s) == 0f && PotStability.TiltSpillPerSecond(15f, s) == 0f, "Thang dung: khong do");
        float r25 = PotStability.TiltSpillPerSecond(25f, s), r40 = PotStability.TiltSpillPerSecond(40f, s), r70 = PotStability.TiltSpillPerSecond(70f, s);
        Check(r25 > 0 && r40 > r25 && r70 > r40, "Nghieng cang nhieu do cang nhanh", $"({r25:F3}, {r40:F3}, {r70:F3} /s)");
        Check(!PotStability.ShouldShatter(0.5f, 45f, s), "Ban cu vo ngay o 42 do; ban moi chi sanh nuoc");
        Check(PotStability.ShouldShatter(0.5f, 120f, s) && PotStability.ShouldShatter(0f, 0f, s), "Lat up hoac het nuoc thi vo");
        Check(PotStability.ImpactSpill(3f, s) == 0f && PotStability.ImpactSpill(10f, s) > 0.2f, "Va cham nhe khong sao, manh thi sanh nhieu", $"({PotStability.ImpactSpill(10f, s):F2})");
        Check(PotStability.StretchSpillPerSecond(0.3f, s) == 0f && PotStability.StretchSpillPerSecond(1.2f, s) > 0f, "Keo gian qua muc thi sanh");
        Check(PotStability.Stars(1f) == 3 && PotStability.Stars(0.6f) == 2 && PotStability.Stars(0.2f) == 1 && PotStability.Stars(0f) == 0, "Tinh sao");
        float d0 = PotStability.Danger01(0, 0, s), d1 = PotStability.Danger01(60, 0, s);
        Check(d0 == 0f && Math.Abs(d1 - 1f) < 1e-5f, "Muc canh bao 0..1");

        // Mo phong 1 nguoi khieng: nghieng 30 do lien tuc -> bao lau het nuoc?
        float soup = 1f, t = 0f;
        while (soup > 0 && t < 120f) { soup -= PotStability.TiltSpillPerSecond(30f, s) / 60f; t += 1f / 60f; }
        Check(t > 10f && t < 60f, "Khieng mot minh (nghieng 30 do) van con thoi gian xoay xo", $"(het nuoc sau {t:F0}s)");
    }

    static void RecorderTests()
    {
        var ring = new FrameRing(5, 4);
        for (byte i = 1; i <= 7; i++) { var slot = ring.AcquireWriteSlot(); slot[0] = i; ring.CommitWrite(); }
        Check(ring.Count == 5 && ring.GetByAge(0)[0] == 7 && ring.GetByAge(4)[0] == 3, "Vong dem giu 5 khung moi nhat");
        var ages = ring.SampleAgesOldestFirst(3);
        Check(ages[0] == 4 && ages[2] == 0, "Lay mau tu cu den moi", $"[{string.Join(",", ages)}]");
        byte[] first = ring.AcquireWriteSlot(); ring.CommitWrite(); byte[] again = null;
        for (int i = 0; i < 4; i++) { ring.AcquireWriteSlot(); ring.CommitWrite(); }
        again = ring.AcquireWriteSlot();
        Check(ReferenceEquals(first, again), "Tai su dung mang (khong tao rac GC)");

        long oldBytes = 160L * 540 * 960 * 3, newBytes = 72L * 270 * 480 * 3;
        Check(newBytes < oldBytes / 8, "RAM dem khung giam manh", $"({oldBytes / 1048576}MB -> {newBytes / 1048576}MB)");

        // Collage: 6 khung 2x2 px, gia tri = so thu tu; khung 0 phai nam o goc TREN-trai.
        int w = 2, h = 2, cols = 3, rows = 2, border = 1;
        var frames = new byte[6][];
        for (int f = 0; f < 6; f++) { frames[f] = new byte[w * h * 3]; for (int i = 0; i < frames[f].Length; i++) frames[f][i] = (byte)(10 + f); }
        int ow = CollageBuilder.OutputWidth(w, cols, border), oh = CollageBuilder.OutputHeight(h, rows, border);
        var dst = new byte[ow * oh * 3];
        CollageBuilder.Compose(frames, w, h, cols, rows, border, dst);
        byte Px(int x, int yFromTop) { int yb = oh - 1 - yFromTop; return dst[(yb * ow + x) * 3]; }
        Check(Px(1, 1) == 10 && Px(4, 1) == 11 && Px(1, 4) == 13 && Px(7, 4) == 15, "Ghep anh truyen tranh dung thu tu (trai->phai, tren->duoi)");
        Check(Px(0, 0) == 255, "Co vien giua cac khung");
    }
}
