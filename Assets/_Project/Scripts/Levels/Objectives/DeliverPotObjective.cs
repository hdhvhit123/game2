using UnityEngine;

/// <summary>Nhiệm vụ "khiêng đồ dễ vỡ tới đích" (nồi lẩu, bánh cưới). Sao theo lượng còn nguyên.</summary>
public class DeliverPotObjective : LevelObjective
{
    [SerializeField] private FragileCarryObject target;

    public override void Spawned()
    {
        base.Spawned();
        if (target == null) target = FragileCarryObject.Current;
    }

    public override void EvaluateObjective(float elapsed)
    {
        if (target == null) target = FragileCarryObject.Current;
        if (target == null || target.Object == null || !target.Object.IsValid) return;

        Progress01 = target.IsShattered ? 0f : target.Soup;
        if (target.IsDelivered && !IsComplete)
        {
            IsComplete = true;
            StarScore = Mathf.Max(1, target.Stars);
        }
    }

    public override string GetStatusText()
    {
        if (IsComplete) return $"✔ {label}";
        return $"{label} ({Mathf.RoundToInt(Progress01 * 100f)}%)";
    }
}
