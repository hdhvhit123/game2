using UnityEngine;

/// <summary>
/// Quỹ đạo của bệ di động / quạt quay tính thuần theo thời gian mô phỏng của Fusion.
/// Mọi máy tính ra cùng một vị trí ở cùng một tick nên không cần gửi vị trí qua mạng.
/// </summary>
public static class MoverPath
{
    public enum Mode { PingPong, Loop }

    /// <summary>
    /// Trả về vị trí trên đường gấp khúc qua các điểm, chạy với tốc độ đều speed (m/s),
    /// có dừng pauseAtEnds giây ở mỗi đầu (chỉ PingPong).
    /// </summary>
    public static Vector3 Evaluate(Vector3[] points, float time, float speed, Mode mode, float pauseAtEnds = 0f)
    {
        if (points == null || points.Length == 0) return Vector3.zero;
        if (points.Length == 1 || speed <= 0f) return points[0];

        float length = PathLength(points, mode == Mode.Loop);
        if (length < 1e-4f) return points[0];
        float travel = length / speed;

        float distance;
        if (mode == Mode.Loop)
        {
            distance = Repeat(time, travel) / travel * length;
            return PointAtDistance(points, distance, true);
        }

        float cycle = 2f * (travel + pauseAtEnds);
        float t = Repeat(time, cycle);
        if (t < travel) distance = t / travel * length;
        else if (t < travel + pauseAtEnds) distance = length;
        else if (t < 2f * travel + pauseAtEnds) distance = length - (t - travel - pauseAtEnds) / travel * length;
        else distance = 0f;
        return PointAtDistance(points, distance, false);
    }

    /// <summary>Góc quay đều (độ), luôn trong khoảng [0, 360).</summary>
    public static float SpinAngle(float time, float degreesPerSecond, float phaseOffset)
    {
        return Repeat(time * degreesPerSecond + phaseOffset, 360f);
    }

    /// <summary>Góc dao động kiểu con lắc (độ), trong khoảng [-amplitude, amplitude].</summary>
    public static float SwingAngle(float time, float amplitude, float period, float phaseOffset)
    {
        if (period <= 0f) return 0f;
        return amplitude * Mathf.Sin((time / period + phaseOffset / 360f) * 2f * Mathf.PI);
    }

    public static float PathLength(Vector3[] points, bool closed)
    {
        float len = 0f;
        for (int i = 0; i < points.Length - 1; i++) len += Vector3.Distance(points[i], points[i + 1]);
        if (closed) len += Vector3.Distance(points[points.Length - 1], points[0]);
        return len;
    }

    private static Vector3 PointAtDistance(Vector3[] points, float distance, bool closed)
    {
        int segments = closed ? points.Length : points.Length - 1;
        for (int i = 0; i < segments; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[(i + 1) % points.Length];
            float seg = Vector3.Distance(a, b);
            if (distance <= seg || i == segments - 1)
                return seg > 1e-5f ? Vector3.Lerp(a, b, distance / seg) : b;
            distance -= seg;
        }
        return points[points.Length - 1];
    }

    private static float Repeat(float t, float length) => length <= 0f ? 0f : t - Mathf.Floor(t / length) * length;
}
