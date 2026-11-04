using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Sinh vật phẩm theo thời gian (xoài rụng từ cây). Chỉ chạy khi màn đang Playing.
/// Prefab cần NetworkObject + Collectible (bật despawnOnFall) và phải nằm trong danh sách prefab của Fusion.
/// </summary>
public class ItemSpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject itemPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float jitterRadius = 1.2f;
    [SerializeField] private float interval = 2.5f;
    [SerializeField, Tooltip("Số vật chưa nhặt tối đa cùng lúc")] private int maxAlive = 6;
    [SerializeField, Tooltip("0 = không giới hạn")] private int totalLimit = 0;

    [Networked] private TickTimer NextSpawn { get; set; }
    [Networked] private int SpawnedTotal { get; set; }

    private readonly List<NetworkObject> _alive = new List<NetworkObject>();
    private System.Random _rng;

    public override void Spawned()
    {
        _rng = new System.Random(GetHashCode());
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || itemPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;
        if (LevelManager.Current != null && LevelManager.Current.Phase != LevelPhase.Playing) return;
        if (!NextSpawn.ExpiredOrNotRunning(Runner)) return;
        NextSpawn = TickTimer.CreateFromSeconds(Runner, interval);

        _alive.RemoveAll(o => o == null || !o.IsValid || (o.TryGetComponent(out Collectible c) && c.Collected));
        if (_alive.Count >= maxAlive) return;
        if (totalLimit > 0 && SpawnedTotal >= totalLimit) return;

        Transform point = spawnPoints[_rng.Next(spawnPoints.Length)];
        if (point == null) return;
        float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
        float r = (float)_rng.NextDouble() * jitterRadius;
        Vector3 pos = point.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);

        NetworkObject obj = Runner.Spawn(itemPrefab, pos, Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f));
        if (obj != null)
        {
            _alive.Add(obj);
            SpawnedTotal++;
        }
    }
}
