using System;
using UnityEngine;
using Fusion;

/// <summary>
/// Nồi lẩu hai quai. Người chơi giữ nút Nắm gần quai sẽ tự bám vào (bản cũ phải gán sẵn
/// carrierLeft/Right trong Inspector, nhưng người chơi được spawn lúc chạy nên luôn null).
/// Nước lẩu sánh ra dần khi nghiêng / kéo giãn / va đập; hết nước hoặc lật úp mới vỡ.
/// Sau khi vỡ, nồi tự hồi lại ở checkpoint gần nhất.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FragileCarryObject : NetworkBehaviour, ISlapReceiver
{
    public static FragileCarryObject Current { get; private set; }

    /// <summary>Giữ tên cũ để tương thích: bắn trên mọi máy khi nồi vỡ.</summary>
    public static event Action OnObjectShatteredEvent;
    public static event Action<FragileCarryObject, float> OnSoupSpilled;   // (nồi, lượng nước còn lại)
    public static event Action<FragileCarryObject> OnPotReset;
    public static event Action<FragileCarryObject, int> OnPotDelivered;   // (nồi, số sao)

    public Transform handleLeft;
    public Transform handleRight;

    [Header("Grab")]
    [SerializeField] private float grabRange = 1.1f;
    [SerializeField] private float releaseDistance = 2.4f;

    [Header("PD Controller")]
    [SerializeField] private float pullSpring = 320f;
    [SerializeField] private float pullDamper = 25f;
    [SerializeField] private float uprightTorque = 18f;

    [Header("Soup & Fail Rules")]
    [SerializeField] private PotStability.Settings stability = PotStability.Settings.Default;
    [SerializeField, Range(0f, 1f)] private float minSoupAfterReset = 0.4f;
    [SerializeField] private float resetDelay = 3f;
    [SerializeField] private float killY = -20f;

    [Header("Visuals")]
    [SerializeField] private GameObject intactVisual;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private Transform soupSurface;
    [SerializeField] private float soupSurfaceEmptyOffset = -0.25f;

    [Networked] public NetworkBool IsShattered { get; set; }
    [Networked] public NetworkBool IsDelivered { get; set; }
    [Networked] public float Soup { get; set; }
    [Networked] public float Danger { get; set; }
    [Networked] public int Stars { get; set; }
    [Networked] private NetworkId CarrierLeftId { get; set; }
    [Networked] private NetworkId CarrierRightId { get; set; }
    [Networked] private TickTimer ResetTimer { get; set; }
    [Networked] private int SpillCount { get; set; }
    [Networked] private int ResetCount { get; set; }

    public float ResetSecondsLeft => Object != null && Object.IsValid ? (ResetTimer.RemainingTime(Runner) ?? 0f) : 0f;

    private Rigidbody _rb;
    private Vector3 _checkpointPos;
    private Quaternion _checkpointRot;
    private float _checkpointSoup = 1f;
    private float _pendingImpactSpill;
    private float _spillSinceLastEvent;
    private bool _insuranceFullSoup;

    private int _seenSpillCount;
    private int _seenResetCount;
    private bool _seenShattered;
    private bool _seenDelivered;
    private Vector3 _soupSurfaceBase;

    public override void Spawned()
    {
        _rb = GetComponent<Rigidbody>();
        Current = this;
        _checkpointPos = transform.position;
        _checkpointRot = transform.rotation;
        if (soupSurface != null) _soupSurfaceBase = soupSurface.localPosition;

        if (HasStateAuthority)
        {
            Soup = 1f;
            _checkpointSoup = 1f;
        }

        _seenSpillCount = SpillCount;
        _seenResetCount = ResetCount;
        _seenShattered = IsShattered;
        _seenDelivered = IsDelivered;
        ApplyVisualState();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Current == this) Current = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (IsShattered)
        {
            if (ResetTimer.Expired(Runner)) ResetToCheckpoint(_insuranceFullSoup);
            return;
        }
        if (IsDelivered) return;

        float dt = Runner.DeltaTime;

        SemiActiveRagdollController left = Resolve(CarrierLeftId);
        SemiActiveRagdollController right = Resolve(CarrierRightId);
        CarrierLeftId = UpdateCarrier(left, handleLeft, right, out left);
        CarrierRightId = UpdateCarrier(right, handleRight, left, out right);

        if (left != null) ApplyHandleForce(handleLeft.position, left.HandPosition);
        if (right != null) ApplyHandleForce(handleRight.position, right.HandPosition);

        // Khi có người khiêng thì nồi tự giữ thăng bằng một chút: dễ chơi hơn mà vẫn hỗn loạn.
        if (left != null || right != null)
        {
            Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
            float strength = (left != null && right != null) ? uprightTorque : uprightTorque * 0.35f;
            _rb.AddTorque(axis * strength - _rb.angularVelocity * 2f, ForceMode.Acceleration);
        }

        float tilt = Vector3.Angle(transform.up, Vector3.up);
        float stretch = 0f;
        if (left != null && right != null)
        {
            float carrierDist = Vector3.Distance(left.HandPosition, right.HandPosition);
            float handleDist = Vector3.Distance(handleLeft.position, handleRight.position);
            stretch = Mathf.Max(0f, carrierDist - handleDist);
        }

        float spill = (PotStability.TiltSpillPerSecond(tilt, stability) + PotStability.StretchSpillPerSecond(stretch, stability)) * dt
                      + _pendingImpactSpill;
        _pendingImpactSpill = 0f;
        Danger = PotStability.Danger01(tilt, stretch, stability);

        if (spill > 0f)
        {
            Soup = Mathf.Max(0f, Soup - spill);
            _spillSinceLastEvent += spill;
            if (_spillSinceLastEvent >= 0.04f)
            {
                _spillSinceLastEvent = 0f;
                SpillCount++;
            }
        }

        if (PotStability.ShouldShatter(Soup, tilt, stability) || transform.position.y < killY)
            Shatter();
    }

    private NetworkId UpdateCarrier(SemiActiveRagdollController current, Transform handle,
                                    SemiActiveRagdollController otherCarrier, out SemiActiveRagdollController result)
    {
        result = current;
        if (handle == null) { result = null; return default; }

        if (current != null)
        {
            bool tooFar = Vector3.Distance(current.HandPosition, handle.position) > releaseDistance;
            if (!current.IsGrabbing || current.IsStunned || tooFar)
            {
                current.IsCarrying = false;
                result = null;
            }
        }

        if (result == null)
        {
            float best = grabRange;
            var all = SemiActiveRagdollController.All;
            for (int i = 0; i < all.Count; i++)
            {
                SemiActiveRagdollController p = all[i];
                if (p == null || p == otherCarrier || !p.IsGrabbing || p.IsStunned || p.IsCarrying) continue;
                float d = Vector3.Distance(p.HandPosition, handle.position);
                if (d < best) { best = d; result = p; }
            }
            if (result != null) result.IsCarrying = true;
        }

        return result != null ? result.Object.Id : default;
    }

    private SemiActiveRagdollController Resolve(NetworkId id)
    {
        if (!id.IsValid) return null;
        if (Runner.TryFindObject(id, out NetworkObject obj) && obj != null &&
            obj.TryGetComponent(out SemiActiveRagdollController c)) return c;
        return null;
    }

    private void ApplyHandleForce(Vector3 handlePos, Vector3 targetHandPos)
    {
        Vector3 error = targetHandPos - handlePos;
        Vector3 pointVel = _rb.GetPointVelocity(handlePos);
        Vector3 force = (error * pullSpring) - (pointVel * pullDamper);
        _rb.AddForceAtPosition(force, handlePos, ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Object == null || !Object.IsValid || !HasStateAuthority || IsShattered) return;
        // Người khiêng va vào nồi không tính (họ dính sát nồi suốt).
        if (collision.rigidbody != null && collision.rigidbody.TryGetComponent(out SemiActiveRagdollController _)) return;
        _pendingImpactSpill += PotStability.ImpactSpill(collision.relativeVelocity.magnitude, stability);
    }

    public void ReceiveSlap(Vector3 impulse, SemiActiveRagdollController slapper)
    {
        if (!HasStateAuthority || IsShattered) return;
        _rb.AddForce(impulse * 0.5f, ForceMode.Impulse);
        _pendingImpactSpill += stability.slapSpill;
    }

    /// <summary>Vỡ ngay (ví dụ rơi vào vùng chết). Chỉ host.</summary>
    public void ForceShatter()
    {
        if (HasStateAuthority && !IsDelivered) Shatter();
    }

    private void Shatter()
    {
        if (IsShattered) return;
        IsShattered = true;
        Danger = 0f;
        ResetTimer = TickTimer.CreateFromSeconds(Runner, resetDelay);
        ReleaseCarriers();
    }

    private void ReleaseCarriers()
    {
        SemiActiveRagdollController l = Resolve(CarrierLeftId), r = Resolve(CarrierRightId);
        if (l != null) l.IsCarrying = false;
        if (r != null) r.IsCarrying = false;
        CarrierLeftId = default;
        CarrierRightId = default;
    }

    // ------------------------------------------------------------------ Checkpoint / reset / giao hàng

    /// <summary>Gọi bởi CheckpointZone (trên host) khi nồi đi qua.</summary>
    public void SetCheckpoint(Vector3 position, Quaternion rotation)
    {
        if (!HasStateAuthority || IsShattered) return;
        _checkpointPos = position;
        _checkpointRot = rotation;
        _checkpointSoup = Soup;
    }

    /// <summary>
    /// Hồi nồi lẩu. fullSoup = true khi người chơi xem quảng cáo "bảo hiểm nồi lẩu".
    /// Có thể gọi từ bất kỳ máy nào.
    /// </summary>
    public void RequestReset(bool fullSoup)
    {
        if (Object == null || !Object.IsValid) return;
        if (HasStateAuthority) ResetToCheckpoint(fullSoup);
        else RPC_RequestReset(fullSoup);
    }

    /// <summary>Đặt trước: lần hồi nồi tiếp theo sẽ đầy nước (phần thưởng quảng cáo).</summary>
    public void GrantInsurance()
    {
        if (Object == null || !Object.IsValid) return;
        if (HasStateAuthority) _insuranceFullSoup = true;
        else RPC_GrantInsurance();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestReset(NetworkBool fullSoup) => ResetToCheckpoint(fullSoup);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_GrantInsurance() => _insuranceFullSoup = true;

    private void ResetToCheckpoint(bool fullSoup)
    {
        ReleaseCarriers();
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.position = _checkpointPos;
        _rb.rotation = _checkpointRot;
        transform.SetPositionAndRotation(_checkpointPos, _checkpointRot);

        Soup = fullSoup ? 1f : Mathf.Max(_checkpointSoup, minSoupAfterReset);
        _insuranceFullSoup = false;
        _pendingImpactSpill = 0f;
        IsShattered = false;
        IsDelivered = false;
        Danger = 0f;
        ResetTimer = TickTimer.None;
        ResetCount++;

        var all = SemiActiveRagdollController.All;
        for (int i = 0; i < all.Count; i++)
        {
            Vector3 spawn = CheckpointZone.Current != null
                ? CheckpointZone.Current.GetPlayerSpawn(i)
                : _checkpointPos + new Vector3(i == 0 ? -1.5f : 1.5f, 0.5f, 0f);
            all[i].TeleportTo(spawn);
        }
    }

    /// <summary>Gọi bởi DeliveryZone (trên host).</summary>
    public void Deliver()
    {
        if (!HasStateAuthority || IsShattered || IsDelivered) return;
        IsDelivered = true;
        Stars = PotStability.Stars(Soup);
        ReleaseCarriers();
    }

    // ------------------------------------------------------------------ Hiển thị (mọi máy)

    public override void Render()
    {
        if (SpillCount != _seenSpillCount)
        {
            _seenSpillCount = SpillCount;
            OnSoupSpilled?.Invoke(this, Soup);
        }

        if (IsShattered != _seenShattered)
        {
            _seenShattered = IsShattered;
            if (_seenShattered) OnObjectShatteredEvent?.Invoke();
            ApplyVisualState();
        }

        if (ResetCount != _seenResetCount)
        {
            _seenResetCount = ResetCount;
            ApplyVisualState();
            OnPotReset?.Invoke(this);
        }

        if (IsDelivered != _seenDelivered)
        {
            _seenDelivered = IsDelivered;
            if (_seenDelivered) OnPotDelivered?.Invoke(this, Stars);
        }

        if (soupSurface != null)
        {
            soupSurface.localPosition = _soupSurfaceBase + Vector3.up * (soupSurfaceEmptyOffset * (1f - Soup));
            soupSurface.gameObject.SetActive(!IsShattered && Soup > 0.01f);
        }
    }

    private void ApplyVisualState()
    {
        bool shattered = Object != null && Object.IsValid && IsShattered;
        if (intactVisual != null) intactVisual.SetActive(!shattered);
        if (brokenVisual != null) brokenVisual.SetActive(shattered);
    }
}
