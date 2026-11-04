using Fusion;
using UnityEngine;

/// <summary>
/// Một nhiệm vụ của màn chơi. Đặt các component con (DeliverPotObjective, CollectObjective,
/// ReachTogetherObjective, SurviveObjective...) lên scene object có NetworkObject.
/// Trạng thái chỉ được host cập nhật rồi đồng bộ, nên HUD trên mọi máy đọc giống nhau.
/// </summary>
public abstract class LevelObjective : NetworkBehaviour
{
    [SerializeField, Tooltip("Dòng hiển thị trên HUD, ví dụ: Bắt gà vào chuồng")]
    protected string label = "Nhiệm vụ";
    [SerializeField, Tooltip("Bỏ chọn nếu đây là nhiệm vụ phụ (chỉ để lấy thêm điểm)")]
    private bool required = true;
    [SerializeField] private int displayOrder;

    [Networked] public NetworkBool IsComplete { get; set; }
    [Networked] public NetworkBool IsFailed { get; set; }
    [Networked] public float Progress01 { get; set; }
    [Networked] public int StarScore { get; set; }

    public bool Required => required;
    public int DisplayOrder => displayOrder;

    public override void Spawned()
    {
        if (HasStateAuthority) StarScore = 3;
        LevelManager.Register(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        LevelManager.Unregister(this);
    }

    /// <summary>Chỉ gọi trên host khi màn đang ở pha Playing.</summary>
    public abstract void EvaluateObjective(float elapsed);

    /// <summary>Dòng chữ cho HUD, ví dụ "Bắt gà vào chuồng 3/5".</summary>
    public virtual string GetStatusText() => IsComplete ? $"✔ {label}" : label;

    /// <summary>Gọi khi màn được chơi lại từ đầu (không tải lại scene).</summary>
    public virtual void ResetObjective()
    {
        IsComplete = false;
        IsFailed = false;
        Progress01 = 0f;
        StarScore = 3;
    }

    public ObjectiveState ToState() => new ObjectiveState
    {
        Required = required,
        Complete = IsComplete,
        Failed = IsFailed,
        Stars = StarScore
    };
}
