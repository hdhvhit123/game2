using System;
using UnityEngine;

/// <summary>
/// Vùng checkpoint (Collider đặt Is Trigger). Khi nồi lẩu đi qua, host lưu vị trí và lượng nước;
/// nồi vỡ hoặc người chơi rơi khỏi map sẽ hồi về đây.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CheckpointZone : MonoBehaviour
{
    public static CheckpointZone Current { get; private set; }
    public static event Action<CheckpointZone> OnCheckpointReached;

    [SerializeField] private int order;
    [SerializeField] private Transform potRespawnPoint;
    [SerializeField] private Transform[] playerSpawnPoints;
    [SerializeField] private GameObject activatedVisual;

    private bool _activated;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (activatedVisual != null) activatedVisual.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out FragileCarryObject pot)) return;
        if (pot.Object == null || !pot.Object.IsValid || pot.IsShattered) return;
        // Không lùi checkpoint khi nồi bị khiêng ngược lại.
        if (Current != null && Current.order > order) return;
        if (Current == this && _activated) return;

        Current = this;
        _activated = true;
        if (activatedVisual != null) activatedVisual.SetActive(true);

        Transform p = potRespawnPoint != null ? potRespawnPoint : transform;
        if (pot.HasStateAuthority) pot.SetCheckpoint(p.position, p.rotation);
        OnCheckpointReached?.Invoke(this);
    }

    public Vector3 GetPlayerSpawn(int playerIndex)
    {
        if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
        {
            Transform t = playerSpawnPoints[Mathf.Abs(playerIndex) % playerSpawnPoints.Length];
            if (t != null) return t.position;
        }
        Transform basis = potRespawnPoint != null ? potRespawnPoint : transform;
        return basis.position + basis.right * (playerIndex % 2 == 0 ? -1.5f : 1.5f) + Vector3.up * 0.5f;
    }
}
