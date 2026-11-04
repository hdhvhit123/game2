using UnityEngine;

/// <summary>Nhiệm vụ "cả hai cùng về đích": tất cả người chơi phải đứng trong vùng cùng lúc.</summary>
public class ReachTogetherObjective : LevelObjective
{
    [SerializeField] private Collider finishZone;
    [SerializeField, Tooltip("Đứng chung trong vùng bao lâu thì tính (tránh lướt qua)")]
    private float holdSeconds = 0.75f;
    [SerializeField] private int requiredPlayers = 2;

    private float _heldTime;

    public override void EvaluateObjective(float elapsed)
    {
        if (finishZone == null || IsComplete) return;
        Bounds b = finishZone.bounds;

        int inside = 0;
        var players = SemiActiveRagdollController.All;
        for (int i = 0; i < players.Count; i++)
            if (players[i] != null && b.Contains(players[i].transform.position)) inside++;

        int needed = Mathf.Max(1, Mathf.Min(requiredPlayers, players.Count));
        Progress01 = Mathf.Clamp01(inside / (float)needed);

        _heldTime = inside >= needed ? _heldTime + Runner.DeltaTime : 0f;
        if (_heldTime >= holdSeconds) IsComplete = true;
    }

    public override string GetStatusText()
    {
        int needed = Mathf.Max(1, requiredPlayers);
        int inside = Mathf.RoundToInt(Progress01 * needed);
        return IsComplete ? $"✔ {label}" : $"{label} ({inside}/{needed} người)";
    }

    public override void ResetObjective()
    {
        base.ResetObjective();
        _heldTime = 0f;
    }
}
