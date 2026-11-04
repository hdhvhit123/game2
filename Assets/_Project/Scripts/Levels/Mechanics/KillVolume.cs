using UnityEngine;

/// <summary>
/// Vùng chết (sông, vực, chảo dầu...): người chơi về checkpoint, vật về chỗ cũ, nồi/bánh thì vỡ.
/// Collider đặt Is Trigger. Chỉ xử lý trên host.
/// </summary>
[RequireComponent(typeof(Collider))]
public class KillVolume : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body == null) return;

        if (body.TryGetComponent(out SemiActiveRagdollController player))
        {
            if (player.Object != null && player.Object.IsValid && player.HasStateAuthority) player.RespawnAtCheckpoint();
        }
        else if (body.TryGetComponent(out FragileCarryObject pot))
        {
            if (pot.Object != null && pot.Object.IsValid && pot.HasStateAuthority) pot.ForceShatter();
        }
        else if (body.TryGetComponent(out Collectible item))
        {
            if (item.Object != null && item.Object.IsValid && item.HasStateAuthority && !item.Collected) item.ResetToSpawn();
        }
    }
}
