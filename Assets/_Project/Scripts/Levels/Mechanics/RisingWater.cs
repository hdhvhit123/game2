using Fusion;
using UnityEngine;

/// <summary>
/// Nước lũ dâng dần (màn Chạy lũ). Độ cao tính từ thời gian đã chơi của LevelManager,
/// nên mọi máy giống nhau. Người chơi chìm hẳn thì về checkpoint nếu checkpoint còn trên mặt nước,
/// nếu không thì thua màn. Vật Collectible chìm thì về chỗ cũ.
/// </summary>
public class RisingWater : NetworkBehaviour
{
    [SerializeField] private Transform surface;
    [SerializeField] private float startY = -1f;
    [SerializeField] private float maxY = 12f;
    [SerializeField, Tooltip("Mét mỗi giây")] private float riseSpeed = 0.12f;
    [SerializeField, Tooltip("Cho người chơi chạy trước bao nhiêu giây")] private float startDelay = 5f;
    [SerializeField, Tooltip("Ngập sâu bao nhiêu so với tâm người thì tính là chìm")] private float drownDepth = 0.6f;
    [SerializeField, Tooltip("Lực nổi đẩy người chơi lên khi lội nước (m/s²)")] private float buoyancy = 6f;
    [SerializeField] private float waterDrag = 3f;

    public float CurrentHeight { get; private set; }

    private void Awake()
    {
        CurrentHeight = startY;
    }

    private float ComputeHeight()
    {
        LevelManager lm = LevelManager.Current;
        float elapsed = lm != null ? lm.Elapsed : 0f;
        return Mathf.Min(maxY, startY + Mathf.Max(0f, elapsed - startDelay) * riseSpeed);
    }

    public override void FixedUpdateNetwork()
    {
        CurrentHeight = ComputeHeight();
        if (!HasStateAuthority) return;

        var players = SemiActiveRagdollController.All;
        for (int i = 0; i < players.Count; i++)
        {
            SemiActiveRagdollController p = players[i];
            if (p == null) continue;
            float depth = CurrentHeight - p.transform.position.y;
            if (depth <= -0.5f) continue;

            // Lội nước: nổi lên một chút và bị cản, khó chạy.
            p.ApplyExternalAcceleration(Vector3.up * buoyancy * Mathf.Clamp01(depth + 0.5f) - p.Velocity * waterDrag * 0.3f);

            if (depth > drownDepth) Drown(p);
        }

        var items = Collectible.All;
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null && !items[i].Collected && items[i].transform.position.y < CurrentHeight - 1f)
                items[i].ResetToSpawn();
    }

    private void Drown(SemiActiveRagdollController p)
    {
        int index = SemiActiveRagdollController.All.IndexOf(p);
        Vector3 respawn = CheckpointZone.Current != null ? CheckpointZone.Current.GetPlayerSpawn(index)
                        : LevelManager.Current != null ? LevelManager.Current.GetPlayerSpawn(index)
                        : p.transform.position;

        if (respawn.y > CurrentHeight + 1f) p.RespawnAtCheckpoint();
        else if (LevelManager.Current != null) LevelManager.Current.FailLevel();
        else p.RespawnAtCheckpoint();
    }

    public override void Render()
    {
        float h = ComputeHeight();
        if (surface != null)
        {
            Vector3 pos = surface.position;
            // Gợn sóng nhẹ cho đẹp (chỉ hình ảnh).
            pos.y = h + Mathf.Sin(Time.time * 1.3f) * 0.04f;
            surface.position = pos;
        }
    }
}
