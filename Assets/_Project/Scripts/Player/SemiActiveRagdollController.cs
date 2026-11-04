using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

/// <summary>
/// Nhân vật Floating Spring Capsule + nghiêng thân trên kiểu bán-ragdoll.
/// Vật lý chỉ chạy trên host (State Authority); client gửi input và nhận trạng thái qua mạng
/// (gắn NetworkRigidbody3D của Fusion Physics addon lên prefab để đồng bộ transform).
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class SemiActiveRagdollController : NetworkBehaviour, ISlapReceiver
{
    public static readonly List<SemiActiveRagdollController> All = new List<SemiActiveRagdollController>(2);

    /// <summary>Vị trí cú tát trúng, bắn trên mọi máy (âm thanh, rung camera).</summary>
    public static event Action<Vector3> OnSlapLanded;
    /// <summary>Nhân vật vừa tiếp đất mạnh (bụi, rung nhẹ).</summary>
    public static event Action<Vector3, float> OnHardLanding;

    [Header("Floating Capsule Settings")]
    [SerializeField] private float rideHeight = 1.1f;
    [SerializeField] private float raycastLength = 1.6f;
    [SerializeField] private float rideSpringStrength = 450f;
    [SerializeField] private float rideSpringDamper = 35f;
    [SerializeField] private float maxSpringAcceleration = 120f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Movement & Action")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float acceleration = 45f;
    [SerializeField] private float maxMoveAcceleration = 80f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.45f;
    [SerializeField, Range(0.1f, 1f)] private float carrySpeedMultiplier = 0.72f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float jumpImpulse = 9.5f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float slapForce = 28f;
    [SerializeField] private float slapRadius = 1.8f;
    [SerializeField] private float slapCooldown = 0.55f;
    [SerializeField] private float slapStunTime = 0.6f;

    [Header("Rope Tether (dây trói)")]
    [SerializeField] private float tetherLength = 4.2f;
    [SerializeField] private float tetherStiffness = 55f;
    [SerializeField] private float tetherDamping = 6f;
    [SerializeField] private float tetherMaxAcceleration = 90f;

    [Header("Respawn")]
    [SerializeField] private float killY = -20f;

    [Header("Procedural Upper-Body Tilt")]
    [SerializeField] private Transform spineBone;
    [SerializeField] private float tiltMultiplier = 2.8f;
    [SerializeField] private float tiltSmoothing = 10f;

    [Header("Squash & Stretch")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float stretchPerSpeed = 0.012f;
    [SerializeField] private float landingSquash = 0.22f;

    [Networked] public NetworkBool IsGrounded { get; set; }
    [Networked] public NetworkBool IsGrabbing { get; set; }
    /// <summary>Do FragileCarryObject đặt khi người chơi đang giữ một quai nồi.</summary>
    [Networked] public NetworkBool IsCarrying { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] private TickTimer CoyoteTimer { get; set; }
    [Networked] private TickTimer JumpBufferTimer { get; set; }
    [Networked] private TickTimer JumpLockTimer { get; set; }
    [Networked] private TickTimer SlapCooldownTimer { get; set; }
    [Networked] private TickTimer StunTimer { get; set; }
    [Networked] private int SlapCount { get; set; }
    [Networked] private int SlapHitCount { get; set; }
    [Networked] private Vector3 LastSlapHitPoint { get; set; }

    public bool IsStunned => Object != null && Object.IsValid && !StunTimer.ExpiredOrNotRunning(Runner);
    public Vector3 HandPosition => transform.position + Vector3.up * 0.5f + transform.forward * 0.35f;
    public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
    public float TetherLength => tetherLength;

    private static readonly Collider[] SlapBuffer = new Collider[24];
    private static readonly HashSet<Rigidbody> SlapVisited = new HashSet<Rigidbody>();

    private Rigidbody _rb;
    private Vector3 _spawnPosition;
    private Vector3 _groundVelocity;

    // Trạng thái chỉ dùng cho hiển thị (mọi máy).
    private Vector3 _lastRenderPos;
    private Vector3 _smoothedVel;
    private Vector3 _smoothedAcc;
    private Quaternion _tiltRotation = Quaternion.identity;
    private Quaternion _spineRest;
    private bool _hasAnimator;
    private bool _wasGrounded;
    private float _squashTimer;
    private float _punchTimer;
    private int _seenSlapCount;
    private int _seenSlapHitCount;
    private Vector3 _visualBaseScale = Vector3.one;

    public override void Spawned()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _spawnPosition = transform.position;
        _lastRenderPos = transform.position;
        _hasAnimator = GetComponentInChildren<Animator>() != null;
        if (spineBone != null) _spineRest = spineBone.localRotation;
        if (visualRoot != null) _visualBaseScale = visualRoot.localScale;
        _seenSlapCount = SlapCount;
        _seenSlapHitCount = SlapHitCount;
        if (!All.Contains(this)) All.Add(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        All.Remove(this);
    }

    public override void FixedUpdateNetwork()
    {
        // Vật lý Rigidbody không được Fusion mô phỏng lại (resimulate) trên client,
        // nên chỉ host áp lực. Nếu client cũng AddForce, lực sẽ bị cộng dồn mỗi lần
        // resimulate gây giật/bay người (lỗi của bản cũ).
        if (!HasStateAuthority) return;

        float dt = Runner.DeltaTime;
        NetworkInputData input = GetInput(out NetworkInputData i) ? i : default;
        // Pha giới thiệu / kết quả của màn chơi: khoá điều khiển.
        if (LevelManager.Current != null && !LevelManager.Current.ControlsEnabled) input = default;
        NetworkButtons pressed = input.buttons.GetPressed(PreviousButtons);
        PreviousButtons = input.buttons;

        bool stunned = IsStunned;
        ApplyFloatingSpring();
        if (IsGrounded) CoyoteTimer = TickTimer.CreateFromSeconds(Runner, coyoteTime);

        // --- Di chuyển ---
        Vector2 move = Vector2.ClampMagnitude(input.moveDirection, 1f); // chặn input gian lận > 1
        float speed = moveSpeed * (IsCarrying ? carrySpeedMultiplier : 1f);
        Vector3 targetVel = new Vector3(move.x, 0f, move.y) * speed;
        Vector3 v = _rb.linearVelocity;
        // Đứng trên bệ/ghe di động thì đi theo bệ (Floating Capsule không có ma sát với mặt đất).
        Vector3 carriedVel = IsGrounded ? new Vector3(_groundVelocity.x, 0f, _groundVelocity.z) : Vector3.zero;
        Vector3 horizontalVel = new Vector3(v.x, 0f, v.z);
        float control = (IsGrounded ? 1f : airControl) * (stunned ? 0.1f : 1f);
        Vector3 steer = CoopPhysicsMath.SteeringAcceleration(horizontalVel, targetVel + carriedVel, acceleration * control, maxMoveAcceleration);
        _rb.AddForce(steer, ForceMode.Acceleration);

        if (!stunned && targetVel.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetVel.normalized, Vector3.up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, Mathf.Clamp01(dt * turnSpeed)));
        }

        // --- Nhảy: jump buffer + coyote time cho cảm giác nhạy hơn ---
        if (pressed.IsSet(InputButtons.Jump)) JumpBufferTimer = TickTimer.CreateFromSeconds(Runner, jumpBufferTime);
        bool canJump = IsGrounded || !CoyoteTimer.ExpiredOrNotRunning(Runner);
        if (!stunned && canJump && !JumpBufferTimer.ExpiredOrNotRunning(Runner))
        {
            v = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(v.x, 0f, v.z);
            _rb.AddForce(Vector3.up * jumpImpulse, ForceMode.Impulse);
            JumpBufferTimer = TickTimer.None;
            CoyoteTimer = TickTimer.None;
            // Tắt lò xo một chút, nếu không lò xo kéo nhân vật dính lại mặt đất (bản cũ nhảy rất thấp).
            JumpLockTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
            IsGrounded = false;
        }

        IsGrabbing = !stunned && input.buttons.IsSet(InputButtons.Grab);

        if (!stunned && pressed.IsSet(InputButtons.Slap) && SlapCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            SlapCooldownTimer = TickTimer.CreateFromSeconds(Runner, slapCooldown);
            ExecuteSlap();
        }

        ApplyTether();

        if (transform.position.y < killY) RespawnAtCheckpoint();
    }

    private void ApplyFloatingSpring()
    {
        if (!JumpLockTimer.ExpiredOrNotRunning(Runner))
        {
            IsGrounded = false;
            return;
        }

        _groundVelocity = Vector3.zero;
        if (Physics.SphereCast(transform.position, 0.25f, Vector3.down, out RaycastHit hit, raycastLength, groundMask, QueryTriggerInteraction.Ignore)
            && hit.rigidbody != _rb)
        {
            if (hit.rigidbody != null && hit.rigidbody.TryGetComponent(out KinematicMover mover))
                _groundVelocity = mover.GetPointVelocity(hit.point);
            else if (hit.rigidbody != null)
                _groundVelocity = hit.rigidbody.GetPointVelocity(hit.point);

            IsGrounded = hit.distance <= rideHeight + 0.15f;

            float groundVelY = _groundVelocity.y;
            float relVelY = _rb.linearVelocity.y - groundVelY;
            float accel = CoopPhysicsMath.RideSpringAcceleration(hit.distance, rideHeight, relVelY,
                                                                  rideSpringStrength, rideSpringDamper, maxSpringAcceleration);
            _rb.AddForce(Vector3.up * accel, ForceMode.Acceleration);

            // Định luật 3 Newton: đứng lên vật động (ví dụ nắp nồi, bè gỗ) sẽ đè nó xuống.
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic && accel > 0f)
                hit.rigidbody.AddForceAtPosition(Vector3.down * accel * _rb.mass, hit.point, ForceMode.Force);
        }
        else
        {
            IsGrounded = false;
        }
    }

    private void ApplyTether()
    {
        for (int i = 0; i < All.Count; i++)
        {
            SemiActiveRagdollController other = All[i];
            if (other == this || other == null || other._rb == null || other.Runner != Runner) continue;

            Vector3 accel = CoopPhysicsMath.TetherAcceleration(_rb.position, other._rb.position,
                _rb.linearVelocity, other._rb.linearVelocity, tetherLength, tetherStiffness, tetherDamping, tetherMaxAcceleration);
            if (accel.sqrMagnitude > 0f) _rb.AddForce(accel, ForceMode.Acceleration);
        }
    }

    private void ExecuteSlap()
    {
        SlapCount++;

        Vector3 center = transform.position + transform.forward * 0.8f;
        int count = Physics.OverlapSphereNonAlloc(center, slapRadius, SlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        SlapVisited.Clear();
        bool hitSomething = false;

        for (int i = 0; i < count; i++)
        {
            Rigidbody body = SlapBuffer[i].attachedRigidbody;
            // Một vật có nhiều collider (ragdoll) chỉ bị tát 1 lần.
            if (body == null || body == _rb || !SlapVisited.Add(body)) continue;

            Vector3 dir = body.worldCenterOfMass - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            Vector3 impulse = (dir.normalized + Vector3.up * 0.45f).normalized * slapForce;

            if (body.TryGetComponent(out ISlapReceiver receiver)) receiver.ReceiveSlap(impulse, this);
            else if (!body.isKinematic) body.AddForce(impulse, ForceMode.Impulse);

            hitSomething = true;
            LastSlapHitPoint = body.worldCenterOfMass;
        }

        if (hitSomething) SlapHitCount++;
    }

    public void ReceiveSlap(Vector3 impulse, SemiActiveRagdollController slapper)
    {
        if (!HasStateAuthority) return;
        _rb.AddForce(impulse, ForceMode.Impulse);
        StunTimer = TickTimer.CreateFromSeconds(Runner, slapStunTime);
        IsGrabbing = false; // bị tát là tuột tay khỏi nồi lẩu
        JumpLockTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);
    }

    /// <summary>Dịch chuyển tức thời (respawn / checkpoint). Chỉ host.</summary>
    public void TeleportTo(Vector3 position)
    {
        if (!HasStateAuthority) return;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.position = position;
        transform.position = position;
        StunTimer = TickTimer.None;
        IsCarrying = false;
    }

    /// <summary>Rơi khỏi map / chạm vùng chết: về checkpoint (chỉ host).</summary>
    public void RespawnAtCheckpoint()
    {
        if (!HasStateAuthority) return;
        int index = All.IndexOf(this);
        Vector3 basePos;
        if (CheckpointZone.Current != null) basePos = CheckpointZone.Current.GetPlayerSpawn(index);
        else if (LevelManager.Current != null) basePos = LevelManager.Current.GetPlayerSpawn(index);
        else basePos = _spawnPosition;
        TeleportTo(basePos + Vector3.up * 0.5f);
        if (LevelManager.Current != null) LevelManager.Current.ReportFall();
    }

    /// <summary>Lực từ bên ngoài (vật nặng ghì lại, gió...). Chỉ host.</summary>
    public void ApplyExternalForce(Vector3 force)
    {
        if (HasStateAuthority && _rb != null) _rb.AddForce(force, ForceMode.Force);
    }

    public void ApplyExternalAcceleration(Vector3 accel)
    {
        if (HasStateAuthority && _rb != null) _rb.AddForce(accel, ForceMode.Acceleration);
    }

    // ------------------------------------------------------------------ Hiển thị (mọi máy)

    public override void Render()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        // Ước lượng vận tốc từ vị trí hiển thị: trên client Rigidbody là kinematic nên
        // linearVelocity luôn bằng 0. Bản cũ còn tính gia tốc theo frame nên bị giật
        // (frame có tick thì gia tốc rất lớn, frame không có tick thì bằng 0).
        Vector3 pos = transform.position;
        Vector3 vel = (pos - _lastRenderPos) / dt;
        _lastRenderPos = pos;
        if (vel.sqrMagnitude > 2500f) vel = _smoothedVel; // bỏ qua frame teleport

        float k = 1f - Mathf.Exp(-tiltSmoothing * dt);
        Vector3 newSmoothedVel = Vector3.Lerp(_smoothedVel, vel, k);
        Vector3 acc = (newSmoothedVel - _smoothedVel) / dt;
        _smoothedVel = newSmoothedVel;
        _smoothedAcc = Vector3.Lerp(_smoothedAcc, acc, k);

        Vector3 localAcc = transform.InverseTransformDirection(_smoothedAcc);
        Quaternion target = Quaternion.Euler(
            Mathf.Clamp(-localAcc.z * tiltMultiplier, -35f, 35f),
            0f,
            Mathf.Clamp(localAcc.x * tiltMultiplier, -35f, 35f));
        _tiltRotation = Quaternion.Slerp(_tiltRotation, target, Mathf.Clamp01(dt * 15f));

        bool grounded = IsGrounded;
        if (grounded && !_wasGrounded)
        {
            float impact = Mathf.Max(0f, -_smoothedVel.y);
            if (impact > 2f)
            {
                _squashTimer = 1f;
                if (impact > 6f) OnHardLanding?.Invoke(pos, impact);
            }
        }
        _wasGrounded = grounded;

        if (SlapCount != _seenSlapCount)
        {
            _seenSlapCount = SlapCount;
            _punchTimer = 1f;
        }
        if (SlapHitCount != _seenSlapHitCount)
        {
            _seenSlapHitCount = SlapHitCount;
            OnSlapLanded?.Invoke(LastSlapHitPoint);
        }

        if (HasInputAuthority && IsStunned && PlayerInputCollector.Instance != null)
            PlayerInputCollector.Instance.ForceReleaseGrab();

        _squashTimer = Mathf.Max(0f, _squashTimer - dt * 6f);
        _punchTimer = Mathf.Max(0f, _punchTimer - dt * 8f);
    }

    private void LateUpdate()
    {
        // Áp sau Animator (Animator chạy giữa Update và LateUpdate). Bản cũ gán trong Render
        // nên bị Animator ghi đè, và còn ghi đè hẳn tư thế animation thay vì cộng thêm.
        if (_rb == null) return;

        if (spineBone != null)
        {
            Quaternion baseRot = _hasAnimator ? spineBone.localRotation : _spineRest;
            spineBone.localRotation = baseRot * _tiltRotation;
        }

        if (visualRoot != null)
        {
            float stretch = Mathf.Clamp(Mathf.Abs(_smoothedVel.y) * stretchPerSpeed, 0f, 0.18f);
            float squash = landingSquash * _squashTimer * _squashTimer;
            float y = 1f + stretch - squash;
            float xz = 1f / Mathf.Sqrt(Mathf.Max(0.3f, y)); // giữ thể tích
            xz += 0.12f * _punchTimer;
            visualRoot.localScale = Vector3.Scale(_visualBaseScale, new Vector3(xz, y, xz));
        }
    }
}
