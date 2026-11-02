using UnityEngine;

/// <summary>
/// Các công thức vật lý thuần (không phụ thuộc Rigidbody) để dễ kiểm thử và tinh chỉnh.
/// </summary>
public static class CoopPhysicsMath
{
    /// <summary>
    /// Gia tốc theo phương thẳng đứng (dương = hướng lên) của lò xo "Floating Capsule".
    /// hitDistance: khoảng cách từ tâm capsule tới mặt đất. relativeVelY: vận tốc dọc của
    /// nhân vật trừ vận tốc dọc của vật đang đứng lên.
    /// </summary>
    public static float RideSpringAcceleration(float hitDistance, float rideHeight, float relativeVelY,
                                               float strength, float damper, float maxAcceleration)
    {
        float error = hitDistance - rideHeight;
        float accel = -(error * strength) - (relativeVelY * damper);
        return Mathf.Clamp(accel, -maxAcceleration, maxAcceleration);
    }

    /// <summary>
    /// Gia tốc kéo của sợi dây trói lên "self". Dây chỉ kéo (không đẩy) khi bị căng
    /// quá chiều dài, có giảm chấn theo vận tốc tách ra nhau để không bị nảy.
    /// </summary>
    public static Vector3 TetherAcceleration(Vector3 selfPos, Vector3 otherPos, Vector3 selfVel, Vector3 otherVel,
                                             float ropeLength, float stiffness, float damping, float maxAcceleration)
    {
        Vector3 delta = otherPos - selfPos;
        float dist = delta.magnitude;
        if (dist <= ropeLength || dist < 1e-4f) return Vector3.zero;

        Vector3 dir = delta / dist;
        float stretch = dist - ropeLength;
        // separatingSpeed > 0 nghĩa là hai người đang chạy xa nhau.
        float separatingSpeed = Vector3.Dot(selfVel - otherVel, dir) * -1f;
        float magnitude = stretch * stiffness + Mathf.Max(0f, separatingSpeed) * damping;
        magnitude = Mathf.Clamp(magnitude, 0f, maxAcceleration);
        return dir * magnitude;
    }

    /// <summary>Lực điều khiển ngang: kéo vận tốc hiện tại về vận tốc mong muốn, có giới hạn.</summary>
    public static Vector3 SteeringAcceleration(Vector3 currentHorizontalVel, Vector3 targetVel, float responsiveness, float maxAcceleration)
    {
        Vector3 accel = (targetVel - currentHorizontalVel) * responsiveness;
        return Vector3.ClampMagnitude(accel, maxAcceleration);
    }
}
