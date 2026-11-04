using Fusion;
using UnityEngine;

/// <summary>
/// Bàn đạp: được nhấn khi có người chơi (hoặc vật nặng) đứng lên. Kết hợp với LevelGate.
/// Vì hai người bị trói, đặt hai bàn đạp xa nhau hơn chiều dài dây là thành câu đố.
/// </summary>
public class PressurePlate : NetworkBehaviour
{
    [SerializeField] private Vector3 halfExtents = new Vector3(0.8f, 0.6f, 0.8f);
    [SerializeField] private Vector3 centerOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField, Tooltip("Vật Collectible (thùng, xe...) cũng nhấn được")] private bool heavyObjectsCanPress = true;
    [SerializeField] private float minObjectMass = 5f;

    [Header("Hiển thị")]
    [SerializeField] private Transform plateVisual;
    [SerializeField] private float pressDepth = 0.08f;
    [SerializeField] private Renderer indicator;
    [SerializeField] private Color idleColor = new Color(0.9f, 0.3f, 0.2f);
    [SerializeField] private Color pressedColor = new Color(0.3f, 0.9f, 0.35f);

    [Networked] public NetworkBool IsPressed { get; set; }

    private static readonly Collider[] Buffer = new Collider[16];
    private Vector3 _plateRest;
    private float _pressVisual;
    private MaterialPropertyBlock _mpb;

    public override void Spawned()
    {
        if (plateVisual != null) _plateRest = plateVisual.localPosition;
        _mpb = new MaterialPropertyBlock();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        Vector3 center = transform.TransformPoint(centerOffset);
        int n = Physics.OverlapBoxNonAlloc(center, halfExtents, Buffer, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        bool pressed = false;
        for (int i = 0; i < n && !pressed; i++)
        {
            Rigidbody body = Buffer[i].attachedRigidbody;
            if (body == null) continue;
            if (body.TryGetComponent(out SemiActiveRagdollController _)) pressed = true;
            else if (heavyObjectsCanPress && body.mass >= minObjectMass && body.TryGetComponent(out Collectible _)) pressed = true;
        }
        IsPressed = pressed;
    }

    public override void Render()
    {
        _pressVisual = Mathf.Lerp(_pressVisual, IsPressed ? 1f : 0f, 1f - Mathf.Exp(-15f * Time.deltaTime));
        if (plateVisual != null) plateVisual.localPosition = _plateRest + Vector3.down * (pressDepth * _pressVisual);
        if (indicator != null)
        {
            indicator.GetPropertyBlock(_mpb);
            Color c = Color.Lerp(idleColor, pressedColor, _pressVisual);
            _mpb.SetColor("_BaseColor", c); // URP Lit
            _mpb.SetColor("_Color", c);     // Built-in
            indicator.SetPropertyBlock(_mpb);
        }
    }
}
