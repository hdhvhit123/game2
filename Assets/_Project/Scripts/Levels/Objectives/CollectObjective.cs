using UnityEngine;

/// <summary>
/// Nhiệm vụ "đưa đủ N vật vào vùng": bắt gà vào chuồng, xoài vào sọt, xe ba gác lên đỉnh dốc.
/// Vùng là một Collider (Is Trigger) bất kỳ; chỉ dùng bounds nên không phụ thuộc sự kiện trigger.
/// </summary>
public class CollectObjective : LevelObjective
{
    [SerializeField] private string category = "ga";
    [SerializeField] private int targetCount = 5;
    [SerializeField] private Collider zone;
    [SerializeField, Tooltip("Vật đã tính điểm được xếp quanh điểm này (để trống = tâm vùng)")]
    private Transform restPoint;
    [SerializeField] private float restSpread = 0.8f;
    [SerializeField, Tooltip("Phải thả tay ra trong vùng mới tính (gà phải được thả vào chuồng)")]
    private bool mustBeReleased = true;

    private int _collected;

    public override void EvaluateObjective(float elapsed)
    {
        if (zone == null || IsComplete) return;
        Bounds b = zone.bounds;

        var all = Collectible.All;
        for (int i = 0; i < all.Count; i++)
        {
            Collectible c = all[i];
            if (c == null || c.Collected || c.Category != category) continue;
            if (mustBeReleased && c.IsCarried) continue;
            if (!b.Contains(c.transform.position)) continue;

            c.MarkCollected(RestPosition(_collected));
            _collected++;
        }

        int count = 0;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].Collected && all[i].Category == category) count++;

        Progress01 = targetCount > 0 ? Mathf.Clamp01(count / (float)targetCount) : 1f;
        if (count >= targetCount) IsComplete = true;
    }

    private Vector3 RestPosition(int index)
    {
        Vector3 center = restPoint != null ? restPoint.position : zone.bounds.center;
        // Xếp theo hình xoắn ốc để các vật không chồng lên nhau.
        float angle = index * 137.5f * Mathf.Deg2Rad;
        float r = restSpread * Mathf.Sqrt(index / (float)Mathf.Max(1, targetCount));
        return center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
    }

    public override string GetStatusText()
    {
        int count = Mathf.RoundToInt(Progress01 * targetCount);
        return IsComplete ? $"✔ {label} {targetCount}/{targetCount}" : $"{label} {count}/{targetCount}";
    }

    public override void ResetObjective()
    {
        base.ResetObjective();
        _collected = 0;
    }
}
