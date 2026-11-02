using System;
using UnityEngine;

/// <summary>
/// Mô hình "nước lẩu sánh ra ngoài": thay vì vỡ ngay khi nghiêng 42° như bản cũ,
/// nồi mất dần nước lẩu khi nghiêng, bị kéo giãn hoặc va đập. Hết nước hoặc lật úp mới toang.
/// </summary>
public static class PotStability
{
    [Serializable]
    public struct Settings
    {
        [Tooltip("Nghiêng dưới góc này thì không đổ giọt nào")] public float safeTilt;
        [Tooltip("Ở góc này nước đổ với tốc độ tối đa")] public float maxTilt;
        [Tooltip("Lật quá góc này là đổ sạch, vỡ ngay")] public float flipTilt;
        [Tooltip("Lượng nước mất mỗi giây khi nghiêng tới maxTilt (1 = cả nồi)")] public float tiltSpillPerSecond;
        public float safeStretch;
        public float maxStretch;
        public float stretchSpillPerSecond;
        [Tooltip("Va chạm nhẹ hơn tốc độ này thì không sao")] public float impactSafeSpeed;
        [Tooltip("Nước mất thêm cho mỗi m/s vượt ngưỡng va chạm")] public float impactSpillPerSpeed;
        public float slapSpill;

        public static Settings Default => new Settings
        {
            safeTilt = 16f,
            maxTilt = 50f,
            flipTilt = 100f,
            tiltSpillPerSecond = 0.35f,
            safeStretch = 0.5f,
            maxStretch = 1.6f,
            stretchSpillPerSecond = 0.3f,
            impactSafeSpeed = 3.5f,
            impactSpillPerSpeed = 0.035f,
            slapSpill = 0.06f
        };
    }

    public static float TiltSpillPerSecond(float tiltDegrees, in Settings s)
    {
        if (tiltDegrees <= s.safeTilt) return 0f;
        float t = Mathf.InverseLerp(s.safeTilt, s.maxTilt, tiltDegrees);
        float rate = s.tiltSpillPerSecond * t * t;
        if (tiltDegrees > s.maxTilt)
            rate *= 1f + (tiltDegrees - s.maxTilt) / 15f; // vượt quá thì đổ nhanh hơn nữa
        return rate;
    }

    public static float StretchSpillPerSecond(float stretch, in Settings s)
    {
        if (stretch <= s.safeStretch) return 0f;
        float t = Mathf.InverseLerp(s.safeStretch, s.maxStretch, stretch);
        float rate = s.stretchSpillPerSecond * t;
        if (stretch > s.maxStretch) rate *= 1f + (stretch - s.maxStretch) * 2f;
        return rate;
    }

    public static float ImpactSpill(float impactSpeed, in Settings s)
    {
        if (impactSpeed <= s.impactSafeSpeed) return 0f;
        return (impactSpeed - s.impactSafeSpeed) * s.impactSpillPerSpeed;
    }

    public static bool ShouldShatter(float soup, float tiltDegrees, in Settings s)
    {
        return soup <= 0f || tiltDegrees >= s.flipTilt;
    }

    /// <summary>0..1, dùng cho cảnh báo (rung, đổi màu hơi nước, HUD nhấp nháy).</summary>
    public static float Danger01(float tiltDegrees, float stretch, in Settings s)
    {
        float tiltDanger = Mathf.InverseLerp(s.safeTilt * 0.6f, s.maxTilt, tiltDegrees);
        float stretchDanger = Mathf.InverseLerp(s.safeStretch * 0.6f, s.maxStretch, stretch);
        return Mathf.Clamp01(Mathf.Max(tiltDanger, stretchDanger));
    }

    public static int Stars(float soup)
    {
        if (soup >= 0.8f) return 3;
        if (soup >= 0.5f) return 2;
        return soup > 0f ? 1 : 0;
    }
}
