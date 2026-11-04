using Fusion;
using UnityEngine;

/// <summary>
/// Vùng gió giật (màn Gom xoài, đoạn cầu khỉ). Đẩy người chơi và vật nhẹ theo hướng gió,
/// cường độ lên xuống theo chu kỳ để người chơi canh lúc gió lặng mà đi.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WindVolume : NetworkBehaviour
{
    [SerializeField] private Vector3 direction = Vector3.right;
    [SerializeField, Tooltip("m/s² lên người chơi lúc gió mạnh nhất")] private float strength = 14f;
    [SerializeField] private float gustPeriod = 5f;
    [SerializeField, Range(0f, 1f), Tooltip("Tỉ lệ thời gian gió lặng trong mỗi chu kỳ")] private float calmShare = 0.4f;
    [SerializeField, Tooltip("Vật nhẹ hơn mức này bị thổi mạnh hơn")] private float lightObjectMass = 2f;
    [SerializeField] private ParticleSystem windParticles;

    private Collider _zone;

    /// <summary>0..1: cường độ gió hiện tại (dùng cho HUD/âm thanh).</summary>
    public float Gust01 { get; private set; }

    public override void Spawned()
    {
        _zone = GetComponent<Collider>();
        _zone.isTrigger = true;
    }

    public static float GustAt(float time, float period, float calmShare)
    {
        if (period <= 0f) return 1f;
        float phase = (time / period) % 1f;
        if (phase < 0f) phase += 1f;
        if (phase < calmShare) return 0f;
        float t = (phase - calmShare) / Mathf.Max(1e-4f, 1f - calmShare);
        return Mathf.Sin(t * Mathf.PI); // lên dần, mạnh nhất ở giữa, tắt dần
    }

    public override void FixedUpdateNetwork()
    {
        Gust01 = GustAt(Runner.Tick * Runner.DeltaTime, gustPeriod, calmShare);
        if (!HasStateAuthority || Gust01 <= 0f) return;

        Bounds b = _zone.bounds;
        Vector3 accel = direction.normalized * (strength * Gust01);

        var players = SemiActiveRagdollController.All;
        for (int i = 0; i < players.Count; i++)
        {
            SemiActiveRagdollController p = players[i];
            if (p == null || !b.Contains(p.transform.position)) continue;
            // Đang khiêng đồ thì vững hơn một chút.
            p.ApplyExternalAcceleration(accel * (p.IsCarrying ? 0.6f : 1f));
        }

        var items = Collectible.All;
        for (int i = 0; i < items.Count; i++)
        {
            Collectible c = items[i];
            if (c == null || c.Collected || !b.Contains(c.transform.position)) continue;
            if (!c.TryGetComponent(out Rigidbody rb) || rb.isKinematic) continue;
            float factor = rb.mass <= lightObjectMass ? 1.3f : lightObjectMass / rb.mass;
            rb.AddForce(accel * factor, ForceMode.Acceleration);
        }
    }

    public override void Render()
    {
        if (windParticles == null) return;
        var emission = windParticles.emission;
        emission.rateOverTime = Mathf.Lerp(0f, 40f, GustAt(Runner.Tick * Runner.DeltaTime, gustPeriod, calmShare));
    }
}
