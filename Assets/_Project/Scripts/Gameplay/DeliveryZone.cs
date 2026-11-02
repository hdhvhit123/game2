using UnityEngine;

/// <summary>
/// Đích đến (bàn ăn của khách). Nồi lẩu còn nguyên vào vùng này là thắng; số sao tính theo lượng nước còn lại.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeliveryZone : MonoBehaviour
{
    [SerializeField, Tooltip("Nồi phải tương đối thẳng đứng mới tính là giao thành công")]
    private float maxTiltToDeliver = 35f;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out FragileCarryObject pot)) return;
        if (pot.Object == null || !pot.Object.IsValid || !pot.HasStateAuthority) return;
        if (pot.IsShattered || pot.IsDelivered) return;
        if (Vector3.Angle(pot.transform.up, Vector3.up) > maxTiltToDeliver) return;

        pot.Deliver();
    }
}
