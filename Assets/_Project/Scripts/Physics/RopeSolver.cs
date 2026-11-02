using UnityEngine;

/// <summary>
/// Bộ giải dây Verlet bước cố định (fixed timestep), tách khỏi phần render để
/// kết quả không phụ thuộc FPS và có thể kiểm thử độc lập.
/// </summary>
public sealed class RopeSolver
{
    public readonly int NodeCount;
    public readonly float TotalLength;
    public readonly float SegmentLength;
    public readonly Vector3[] Positions;
    private readonly Vector3[] _previous;

    public RopeSolver(int nodeCount, float totalLength)
    {
        NodeCount = Mathf.Max(3, nodeCount);
        TotalLength = Mathf.Max(0.1f, totalLength);
        SegmentLength = TotalLength / (NodeCount - 1);
        Positions = new Vector3[NodeCount];
        _previous = new Vector3[NodeCount];
    }

    /// <summary>Đặt dây thẳng giữa 2 đầu (tránh frame đầu dây bay từ gốc toạ độ 0,0,0).</summary>
    public void Reset(Vector3 a, Vector3 b)
    {
        for (int i = 0; i < NodeCount; i++)
        {
            float t = i / (float)(NodeCount - 1);
            Positions[i] = Vector3.Lerp(a, b, t);
            _previous[i] = Positions[i];
        }
    }

    public void Integrate(float dt, Vector3 gravity, float damping)
    {
        Vector3 gravityStep = gravity * (dt * dt);
        for (int i = 1; i < NodeCount - 1; i++)
        {
            Vector3 velocity = (Positions[i] - _previous[i]) * damping;
            _previous[i] = Positions[i];
            Positions[i] += velocity + gravityStep;
        }
    }

    public void SolveConstraints(Vector3 a, Vector3 b, int iterations)
    {
        int last = NodeCount - 1;
        Positions[0] = a;
        Positions[last] = b;

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int i = 0; i < last; i++)
            {
                Vector3 delta = Positions[i + 1] - Positions[i];
                float dist = delta.magnitude;
                if (dist < 1e-4f) continue;

                float error = (dist - SegmentLength) / dist;
                bool pinnedA = i == 0;
                bool pinnedB = i + 1 == last;

                if (pinnedA && pinnedB) continue;
                if (pinnedA) Positions[i + 1] -= delta * error;
                else if (pinnedB) Positions[i] += delta * error;
                else
                {
                    Vector3 half = delta * (0.5f * error);
                    Positions[i] += half;
                    Positions[i + 1] -= half;
                }
            }
        }
    }

    /// <summary>Đẩy node lên trên mặt va chạm (collision tính ngoài, ví dụ bằng Raycast).</summary>
    public void PushAbove(int index, float surfaceY, float radius)
    {
        if (index <= 0 || index >= NodeCount - 1) return;
        float minY = surfaceY + radius;
        if (Positions[index].y < minY)
        {
            Positions[index].y = minY;
            // Ma sát đơn giản: triệt tiêu phần vận tốc ngang khi chạm đất.
            _previous[index] = Vector3.Lerp(_previous[index], Positions[index], 0.5f);
        }
    }

    /// <summary>Tỉ lệ căng: khoảng cách 2 đầu / chiều dài dây. &gt;= 1 là dây căng thẳng.</summary>
    public static float Tension(Vector3 a, Vector3 b, float length) => Vector3.Distance(a, b) / Mathf.Max(0.0001f, length);

    public bool HasInvalidValues()
    {
        for (int i = 0; i < NodeCount; i++)
        {
            Vector3 p = Positions[i];
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z)) return true;
        }
        return false;
    }
}
