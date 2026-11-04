using UnityEngine;

/// <summary>
/// Nhiệm vụ "trụ được N giây" (chạy lũ, giữ thăng bằng trên ghe...).
/// Có thể kèm điều kiện: cả hai phải ở trong vùng an toàn khi hết giờ.
/// </summary>
public class SurviveObjective : LevelObjective
{
    [SerializeField] private float surviveSeconds = 60f;
    [SerializeField, Tooltip("Để trống = chỉ cần sống sót")] private Collider safeZoneAtEnd;
    [SerializeField, Tooltip("Mỗi lần rơi/chết bị trừ 1 sao")] private bool starPerFall = true;

    public override void EvaluateObjective(float elapsed)
    {
        if (IsComplete) return;
        Progress01 = surviveSeconds > 0f ? Mathf.Clamp01(elapsed / surviveSeconds) : 1f;

        if (starPerFall && LevelManager.Current != null)
            StarScore = Mathf.Clamp(3 - LevelManager.Current.Falls, 1, 3);

        if (elapsed < surviveSeconds) return;

        if (safeZoneAtEnd != null)
        {
            Bounds b = safeZoneAtEnd.bounds;
            var players = SemiActiveRagdollController.All;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && !b.Contains(players[i].transform.position))
                {
                    IsFailed = true;
                    return;
                }
            }
        }
        IsComplete = true;
    }

    public override string GetStatusText()
    {
        if (IsComplete) return $"✔ {label}";
        float left = Mathf.Max(0f, surviveSeconds * (1f - Progress01));
        return $"{label} (còn {Mathf.CeilToInt(left)}s)";
    }
}
