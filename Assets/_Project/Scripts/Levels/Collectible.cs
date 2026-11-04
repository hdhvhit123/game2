using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Vật có thể nắm và mang đi: gà (tự chạy trốn, giãy khỏi tay), xoài, xe ba gác (nặng, cần 2 người).
/// Đưa vào vùng của CollectObjective có cùng category là được tính điểm.
/// Chỉ host mô phỏng; gắn thêm NetworkRigidbody3D để đồng bộ vị trí.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Collectible : NetworkBehaviour, ISlapReceiver
{
    public static readonly List<Collectible> All = new List<Collectible>();

    [SerializeField, Tooltip("Ví dụ: ga, xoai, xe. CollectObjective chỉ đếm đúng loại của nó")]
    private string category = "ga";

    [Header("Nắm & mang")]
    [SerializeField, Range(1, 2)] private int maxCarriers = 1;
    [SerializeField] private float grabRange = 1.2f;
    [SerializeField] private float releaseDistance = 2.6f;
    [SerializeField] private float holdForward = 0.35f;
    [SerializeField] private float pullSpring = 260f;
    [SerializeField] private float pullDamper = 22f;
    [SerializeField, Tooltip("Lực kéo tối đa của MỖI người (N). Vật nặng mà một người kéo không nổi thì cần cả hai")]
    private float maxPullForcePerCarrier = 400f;
    [SerializeField, Range(0f, 1f), Tooltip("Phần phản lực dội lại người kéo: vật càng nặng càng ghì người lại")]
    private float reactionShare = 0.6f;

    [Header("Chạy trốn (gà)")]
    [SerializeField, Tooltip("0 = không chạy trốn")] private float fleeRadius = 0f;
    [SerializeField] private float fleeSpeed = 5.5f;
    [SerializeField] private float wanderSpeed = 1.5f;
    [SerializeField] private float steerAcceleration = 25f;
    [SerializeField] private float hopImpulse = 3.5f;
    [SerializeField, Tooltip("Xác suất mỗi giây giãy thoát khi đang bị mang")] private float escapeChancePerSecond = 0.08f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Rơi khỏi map")]
    [SerializeField] private float killY = -20f;
    [SerializeField, Tooltip("Vật do ItemSpawner sinh ra thì huỷ luôn thay vì đưa về chỗ cũ")]
    private bool despawnOnFall;

    [Networked] public NetworkBool Collected { get; set; }
    [Networked] private NetworkId CarrierA { get; set; }
    [Networked] private NetworkId CarrierB { get; set; }
    [Networked] private TickTimer DecisionTimer { get; set; }
    [Networked] private Vector3 WanderDirection { get; set; }
    [Networked] private int CollectCount { get; set; }

    public string Category => category;
    public bool IsCarried => CarrierA.IsValid || CarrierB.IsValid;

    /// <summary>Âm thanh/hiệu ứng: vật vừa được tính điểm.</summary>
    public static event System.Action<Collectible> OnCollected;

    private Rigidbody _rb;
    private Vector3 _spawnPos;
    private Quaternion _spawnRot;
    private System.Random _rng;
    private int _seenCollectCount;

    public override void Spawned()
    {
        _rb = GetComponent<Rigidbody>();
        _spawnPos = transform.position;
        _spawnRot = transform.rotation;
        _rng = new System.Random(Object.Id.GetHashCode() ^ 7919);
        _seenCollectCount = CollectCount;
        if (!All.Contains(this)) All.Add(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        All.Remove(this);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || Collected) return;

        if (transform.position.y < killY)
        {
            ReleaseAll();
            if (despawnOnFall) { Runner.Despawn(Object); return; }
            ResetToSpawn();
            return;
        }

        SemiActiveRagdollController a = Resolve(CarrierA);
        SemiActiveRagdollController b = Resolve(CarrierB);
        CarrierA = UpdateSlot(a, b, out a);
        CarrierB = maxCarriers >= 2 ? UpdateSlot(b, a, out b) : default;
        if (maxCarriers < 2) b = null;

        if (a != null) Pull(a);
        if (b != null) Pull(b);

        if (fleeRadius > 0f)
        {
            if (a != null || b != null) TryEscape();
            else RunAround();
        }
    }

    // ------------------------------------------------------------------ Nắm / kéo

    private NetworkId UpdateSlot(SemiActiveRagdollController current, SemiActiveRagdollController other,
                                 out SemiActiveRagdollController result)
    {
        result = current;
        if (current != null &&
            (!current.IsGrabbing || current.IsStunned || Vector3.Distance(current.HandPosition, _rb.worldCenterOfMass) > releaseDistance))
        {
            current.IsCarrying = false;
            result = null;
        }

        if (result == null)
        {
            float best = grabRange;
            var players = SemiActiveRagdollController.All;
            for (int i = 0; i < players.Count; i++)
            {
                SemiActiveRagdollController p = players[i];
                if (p == null || p == other || !p.IsGrabbing || p.IsStunned || p.IsCarrying) continue;
                float d = Vector3.Distance(p.HandPosition, _rb.worldCenterOfMass);
                if (d < best) { best = d; result = p; }
            }
            if (result != null) result.IsCarrying = true;
        }
        return result != null ? result.Object.Id : default;
    }

    private void Pull(SemiActiveRagdollController carrier)
    {
        Vector3 target = carrier.HandPosition + carrier.transform.forward * holdForward;
        Vector3 error = target - _rb.worldCenterOfMass;
        Vector3 relVel = _rb.linearVelocity - carrier.Velocity;
        Vector3 desiredAccel = error * pullSpring - relVel * pullDamper;
        Vector3 force = Vector3.ClampMagnitude(desiredAccel * _rb.mass, maxPullForcePerCarrier);
        _rb.AddForce(force, ForceMode.Force);
        carrier.ApplyExternalForce(-force * reactionShare);
    }

    private void TryEscape()
    {
        if (_rng.NextDouble() < escapeChancePerSecond * Runner.DeltaTime)
        {
            ReleaseAll();
            Vector3 dir = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f) * Vector3.forward;
            _rb.AddForce(dir * hopImpulse + Vector3.up * hopImpulse * 1.4f, ForceMode.VelocityChange);
        }
    }

    // ------------------------------------------------------------------ AI chạy trốn (gà)

    private void RunAround()
    {
        Vector3 pos = _rb.position;
        SemiActiveRagdollController nearest = null;
        float nearestDist = fleeRadius;
        var players = SemiActiveRagdollController.All;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null) continue;
            float d = Vector3.Distance(players[i].transform.position, pos);
            if (d < nearestDist) { nearestDist = d; nearest = players[i]; }
        }

        Vector3 desired;
        if (nearest != null)
        {
            Vector3 away = pos - nearest.transform.position;
            away.y = 0f;
            // Chạy chéo một chút cho khó đoán.
            away = Quaternion.Euler(0f, Mathf.Sin(Runner.Tick * 0.05f + Object.Id.GetHashCode()) * 35f, 0f) * away.normalized;
            desired = away * fleeSpeed;
        }
        else
        {
            if (DecisionTimer.ExpiredOrNotRunning(Runner))
            {
                float idle = (float)_rng.NextDouble();
                WanderDirection = idle < 0.35f ? Vector3.zero
                    : Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f) * Vector3.forward;
                DecisionTimer = TickTimer.CreateFromSeconds(Runner, 1.5f + (float)_rng.NextDouble() * 2.5f);
            }
            desired = WanderDirection * wanderSpeed;
        }

        Vector3 v = _rb.linearVelocity;
        Vector3 steer = CoopPhysicsMath.SteeringAcceleration(new Vector3(v.x, 0f, v.z), desired, 8f, steerAcceleration);
        _rb.AddForce(steer, ForceMode.Acceleration);

        if (desired.sqrMagnitude > 0.1f)
        {
            Quaternion look = Quaternion.LookRotation(new Vector3(desired.x, 0f, desired.z), Vector3.up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, look, Mathf.Clamp01(Runner.DeltaTime * 10f)));
        }

        // Thỉnh thoảng nhảy cẫng lên khi đang hoảng.
        if (nearest != null && IsGrounded() && _rng.NextDouble() < 1.2 * Runner.DeltaTime)
            _rb.AddForce(Vector3.up * hopImpulse, ForceMode.VelocityChange);
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(_rb.position + Vector3.up * 0.1f, Vector3.down, out _, 0.35f, groundMask, QueryTriggerInteraction.Ignore);
    }

    // ------------------------------------------------------------------ Tính điểm / reset

    /// <summary>Gọi bởi CollectObjective trên host.</summary>
    public void MarkCollected(Vector3 restPosition)
    {
        if (!HasStateAuthority || Collected) return;
        ReleaseAll();
        Collected = true;
        CollectCount++;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        _rb.position = restPosition;
        transform.position = restPosition;
    }

    public void ResetToSpawn()
    {
        if (!HasStateAuthority) return;
        ReleaseAll();
        Collected = false;
        _rb.isKinematic = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.position = _spawnPos;
        _rb.rotation = _spawnRot;
        transform.SetPositionAndRotation(_spawnPos, _spawnRot);
    }

    public void ReceiveSlap(Vector3 impulse, SemiActiveRagdollController slapper)
    {
        if (!HasStateAuthority || Collected) return;
        ReleaseAll();
        _rb.AddForce(impulse * Mathf.Min(1f, 2f / Mathf.Max(0.1f, _rb.mass)), ForceMode.Impulse);
    }

    private void ReleaseAll()
    {
        SemiActiveRagdollController a = Resolve(CarrierA), b = Resolve(CarrierB);
        if (a != null) a.IsCarrying = false;
        if (b != null) b.IsCarrying = false;
        CarrierA = default;
        CarrierB = default;
    }

    private SemiActiveRagdollController Resolve(NetworkId id)
    {
        if (!id.IsValid) return null;
        if (Runner.TryFindObject(id, out NetworkObject obj) && obj != null &&
            obj.TryGetComponent(out SemiActiveRagdollController c)) return c;
        return null;
    }

    public override void Render()
    {
        if (CollectCount != _seenCollectCount)
        {
            _seenCollectCount = CollectCount;
            if (Collected) OnCollected?.Invoke(this);
        }
    }
}
