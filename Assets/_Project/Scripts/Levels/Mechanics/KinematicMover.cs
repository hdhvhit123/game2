using Fusion;
using UnityEngine;

/// <summary>
/// Bệ di động, ghe chạy ngang, quạt trần quay, búa con lắc... Vị trí/góc tính từ tick mô phỏng
/// nên mọi máy giống nhau mà không tốn băng thông. Cần Rigidbody (Is Kinematic) trên cùng object.
/// Người chơi đứng lên sẽ được chở theo (SemiActiveRagdollController đọc GetPointVelocity).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KinematicMover : NetworkBehaviour
{
    public enum MotionType { Path, Spin, Swing }

    [SerializeField] private MotionType motion = MotionType.Path;

    [Header("Path")]
    [SerializeField, Tooltip("Các điểm (toạ độ cục bộ so với vị trí ban đầu)")]
    private Vector3[] localWaypoints = { Vector3.zero, new Vector3(0f, 0f, 6f) };
    [SerializeField] private float speed = 2f;
    [SerializeField] private MoverPath.Mode pathMode = MoverPath.Mode.PingPong;
    [SerializeField] private float pauseAtEnds = 0.8f;

    [Header("Spin / Swing")]
    [SerializeField] private Vector3 axis = Vector3.up;
    [SerializeField] private float degreesPerSecond = 90f;
    [SerializeField] private float swingAmplitude = 60f;
    [SerializeField] private float swingPeriod = 3f;

    [Header("Chung")]
    [SerializeField, Tooltip("Lệch pha (giây) để nhiều vật giống nhau không chạy đồng bộ")] private float timeOffset;

    private Rigidbody _rb;
    private Vector3 _origin;
    private Quaternion _baseRotation;
    private Vector3[] _worldPoints;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.None; // tự đặt vị trí mỗi frame trong Render
        _origin = transform.position;
        _baseRotation = transform.rotation;
        _worldPoints = new Vector3[Mathf.Max(1, localWaypoints.Length)];
        for (int i = 0; i < localWaypoints.Length; i++) _worldPoints[i] = _origin + _baseRotation * localWaypoints[i];
    }

    private float SimTime => Runner != null ? Runner.Tick * Runner.DeltaTime + timeOffset : timeOffset;

    public override void FixedUpdateNetwork()
    {
        Pose(SimTime, out Vector3 p, out Quaternion r);
        _rb.MovePosition(p);
        _rb.MoveRotation(r);
    }

    public override void Render()
    {
        // Nội suy giữa 2 tick cho chuyển động mượt ở mọi FPS.
        float t = SimTime + Runner.LocalAlpha * Runner.DeltaTime;
        Pose(t, out Vector3 p, out Quaternion r);
        transform.SetPositionAndRotation(p, r);
    }

    public void Pose(float time, out Vector3 position, out Quaternion rotation)
    {
        position = _origin;
        rotation = _baseRotation;
        Vector3 ax = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;

        switch (motion)
        {
            case MotionType.Path:
                position = MoverPath.Evaluate(_worldPoints, time, speed, pathMode, pauseAtEnds);
                break;
            case MotionType.Spin:
                rotation = Quaternion.AngleAxis(MoverPath.SpinAngle(time, degreesPerSecond, 0f), ax) * _baseRotation;
                break;
            case MotionType.Swing:
                rotation = Quaternion.AngleAxis(MoverPath.SwingAngle(time, swingAmplitude, swingPeriod, 0f), ax) * _baseRotation;
                break;
        }
    }

    /// <summary>Vận tốc tại một điểm trên bề mặt (để chở người chơi đứng lên).</summary>
    public Vector3 GetPointVelocity(Vector3 worldPoint)
    {
        if (Runner == null) return Vector3.zero;
        float t = SimTime, dt = Runner.DeltaTime;
        Pose(t, out Vector3 p0, out Quaternion r0);
        Pose(t + dt, out Vector3 p1, out Quaternion r1);
        // Biến điểm theo chuyển động của vật trong 1 tick.
        Vector3 local = Quaternion.Inverse(r0) * (worldPoint - p0);
        Vector3 next = p1 + r1 * local;
        return (next - worldPoint) / dt;
    }
}
