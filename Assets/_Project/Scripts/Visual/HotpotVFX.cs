using UnityEngine;

/// <summary>
/// Hiệu ứng cho nồi lẩu, tự dựng bằng code nên không cần asset:
///  - Hơi nóng bốc lên: càng đầy càng nhiều hơi; nồi nguy hiểm thì hơi chuyển cam đỏ và bốc gấp.
///  - Giọt nước lẩu bắn ra mỗi lần sánh, bùng mạnh khi nồi vỡ.
///  - Nồi rung lắc nhẹ khi sắp đổ (cảnh báo bằng hình ảnh).
/// Gắn lên cùng GameObject với FragileCarryObject. Kéo một material hạt (URP Particles/Unlit)
/// vào particleMaterial để chắc chắn shader không bị strip khi build.
/// </summary>
[RequireComponent(typeof(FragileCarryObject))]
public class HotpotVFX : MonoBehaviour
{
    [SerializeField] private Material particleMaterial;
    [SerializeField] private Transform potRim;
    [SerializeField] private Transform wobbleVisual;
    [SerializeField] private float rimRadius = 0.45f;

    [Header("Colors")]
    [SerializeField] private Color steamCalm = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color steamDanger = new Color(1f, 0.45f, 0.2f, 0.55f);
    [SerializeField] private Color soupColor = new Color(0.86f, 0.24f, 0.06f, 1f);

    private FragileCarryObject _pot;
    private ParticleSystem _steam;
    private ParticleSystem _splash;
    private Quaternion _wobbleBase;
    private float _danger;

    private void Awake()
    {
        _pot = GetComponent<FragileCarryObject>();
        Transform rim = potRim != null ? potRim : transform;
        if (particleMaterial == null) particleMaterial = CreateFallbackMaterial();

        _steam = CreateSteam(rim);
        _splash = CreateSplash(rim);
        if (wobbleVisual != null) _wobbleBase = wobbleVisual.localRotation;
    }

    private void OnEnable()
    {
        FragileCarryObject.OnSoupSpilled += HandleSpill;
        FragileCarryObject.OnObjectShatteredEvent += HandleShatter;
        FragileCarryObject.OnPotReset += HandleReset;
    }

    private void OnDisable()
    {
        FragileCarryObject.OnSoupSpilled -= HandleSpill;
        FragileCarryObject.OnObjectShatteredEvent -= HandleShatter;
        FragileCarryObject.OnPotReset -= HandleReset;
    }

    private void Update()
    {
        if (_pot.Object == null || !_pot.Object.IsValid) return;

        bool alive = !_pot.IsShattered;
        _danger = Mathf.Lerp(_danger, alive ? _pot.Danger : 0f, 1f - Mathf.Exp(-8f * Time.deltaTime));

        var emission = _steam.emission;
        emission.rateOverTime = alive ? Mathf.Lerp(2f, 16f, _pot.Soup) * (1f + _danger * 1.5f) : 0f;
        var main = _steam.main;
        main.startColor = Color.Lerp(steamCalm, steamDanger, _danger);
        main.startSpeed = Mathf.Lerp(0.6f, 1.6f, _danger);

        if (wobbleVisual != null)
        {
            float t = Time.time * 28f;
            float amp = 6f * _danger * _danger;
            wobbleVisual.localRotation = _wobbleBase * Quaternion.Euler(Mathf.Sin(t) * amp, 0f, Mathf.Cos(t * 1.3f) * amp);
        }
    }

    private void HandleSpill(FragileCarryObject pot, float soupLeft)
    {
        if (pot != _pot) return;
        _splash.Emit(10);
    }

    private void HandleShatter()
    {
        _splash.Emit(70);
        _steam.Emit(25);
        Haptics.Strong();
    }

    private void HandleReset(FragileCarryObject pot)
    {
        if (pot != _pot) return;
        _splash.Clear();
        _danger = 0f;
    }

    // ------------------------------------------------------------------ Dựng particle

    private ParticleSystem CreateSteam(Transform parent)
    {
        ParticleSystem ps = NewSystem("Steam", parent, new Vector3(0f, 0.05f, 0f));
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startSpeed = 0.8f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startColor = steamCalm;
        main.gravityModifier = -0.05f;
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = rimRadius * 0.8f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.6f;

        ps.Play();
        return ps;
    }

    private ParticleSystem CreateSplash(Transform parent)
    {
        ParticleSystem ps = NewSystem("SoupSplash", parent, Vector3.zero);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(soupColor, Color.Lerp(soupColor, new Color(1f, 0.8f, 0.3f), 0.5f));
        main.gravityModifier = 1.6f;
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = rimRadius;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.bounce = 0.1f;
        collision.lifetimeLoss = 0.6f;
        collision.quality = ParticleSystemCollisionQuality.Low; // rẻ trên điện thoại

        return ps;
    }

    private ParticleSystem NewSystem(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        // AddComponent làm hệ hạt tự chạy ngay; phải dừng trước khi đổi duration, nếu không Unity báo lỗi.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    private static Material CreateFallbackMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) : null;
    }
}
