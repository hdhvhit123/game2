using UnityEngine;

/// <summary>
/// Camera tự đóng khung cả 2 người chơi và nồi lẩu: hai người càng xa nhau camera càng lùi ra.
/// Có rung màn hình (trauma) khi tát, tiếp đất mạnh, nước lẩu sánh và khi nồi vỡ.
/// Gắn lên Main Camera.
/// </summary>
public class CoopCameraRig : MonoBehaviour
{
    private static float _trauma;

    [Header("Framing")]
    [SerializeField] private Vector3 viewDirection = new Vector3(0f, 1.15f, -1f);
    [SerializeField] private float minDistance = 9f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private float distancePerMeter = 1.1f;
    [SerializeField] private float potWeight = 1.5f;
    [SerializeField] private float positionSmoothTime = 0.25f;
    [SerializeField] private float lookHeight = 0.6f;

    [Header("Shake")]
    [SerializeField] private float maxShakeAngle = 3.5f;
    [SerializeField] private float maxShakeOffset = 0.35f;
    [SerializeField] private float shakeFrequency = 22f;
    [SerializeField] private float traumaDecay = 1.6f;

    private Vector3 _velocity;
    private Vector3 _basePos;
    private Vector3 _lookTarget;
    private bool _hasTarget;

    public static void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

    private void OnEnable()
    {
        FragileCarryObject.OnObjectShatteredEvent += OnShatter;
        FragileCarryObject.OnSoupSpilled += OnSpill;
        SemiActiveRagdollController.OnSlapLanded += OnSlap;
        SemiActiveRagdollController.OnHardLanding += OnLanding;
    }

    private void OnDisable()
    {
        FragileCarryObject.OnObjectShatteredEvent -= OnShatter;
        FragileCarryObject.OnSoupSpilled -= OnSpill;
        SemiActiveRagdollController.OnSlapLanded -= OnSlap;
        SemiActiveRagdollController.OnHardLanding -= OnLanding;
    }

    private static void OnShatter() => AddTrauma(0.9f);
    private static void OnSpill(FragileCarryObject pot, float soup) => AddTrauma(0.08f);
    private static void OnSlap(Vector3 pos) => AddTrauma(0.3f);
    private static void OnLanding(Vector3 pos, float speed) => AddTrauma(Mathf.Clamp01(speed / 40f));

    private void LateUpdate()
    {
        if (!ComputeFocus(out Vector3 center, out float spread)) return;

        Vector3 dir = viewDirection.sqrMagnitude > 1e-4f ? viewDirection.normalized : new Vector3(0f, 0.75f, -0.66f);
        float distance = Mathf.Clamp(minDistance + spread * distancePerMeter, minDistance, maxDistance);
        Vector3 desired = center + dir * distance;

        if (!_hasTarget)
        {
            _basePos = desired;
            _lookTarget = center;
            _hasTarget = true;
        }

        float dt = Time.deltaTime;
        _basePos = Vector3.SmoothDamp(_basePos, desired, ref _velocity, positionSmoothTime);
        _lookTarget = Vector3.Lerp(_lookTarget, center, 1f - Mathf.Exp(-8f * dt));
        Quaternion baseRot = Quaternion.LookRotation(_lookTarget + Vector3.up * lookHeight - _basePos, Vector3.up);

        // Rung theo Perlin noise, cường độ = trauma^2 để rung nhẹ thì nhẹ hẳn, mạnh thì mạnh hẳn.
        float shake = _trauma * _trauma;
        float t = Time.time * shakeFrequency;
        Quaternion shakeRot = Quaternion.Euler(
            maxShakeAngle * shake * (Mathf.PerlinNoise(t, 0.1f) * 2f - 1f),
            maxShakeAngle * shake * (Mathf.PerlinNoise(0.2f, t) * 2f - 1f),
            maxShakeAngle * shake * (Mathf.PerlinNoise(t, t * 0.5f) * 2f - 1f));
        Vector3 shakeOffset = new Vector3(Mathf.PerlinNoise(t + 3f, 7f) * 2f - 1f, Mathf.PerlinNoise(11f, t + 5f) * 2f - 1f, 0f)
                              * (maxShakeOffset * shake);

        transform.rotation = baseRot * shakeRot;
        transform.position = _basePos + transform.rotation * shakeOffset;
        _trauma = Mathf.Max(0f, _trauma - traumaDecay * dt);
    }

    private bool ComputeFocus(out Vector3 center, out float spread)
    {
        center = Vector3.zero;
        spread = 0f;
        float weight = 0f;
        bool hasBounds = false;
        Bounds bounds = default;

        var players = SemiActiveRagdollController.All;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null) continue;
            Vector3 p = players[i].transform.position;
            center += p; weight += 1f;
            if (!hasBounds) { bounds = new Bounds(p, Vector3.zero); hasBounds = true; } else bounds.Encapsulate(p);
        }

        FragileCarryObject pot = FragileCarryObject.Current;
        if (pot != null)
        {
            Vector3 p = pot.transform.position;
            center += p * potWeight; weight += potWeight;
            if (!hasBounds) { bounds = new Bounds(p, Vector3.zero); hasBounds = true; } else bounds.Encapsulate(p);
        }

        if (weight <= 0f) return false;
        center /= weight;
        Vector3 size = bounds.size;
        spread = Mathf.Max(size.x, size.z);
        return true;
    }
}
