using Fusion;
using UnityEngine;

/// <summary>
/// Cổng/cầu rút mở khi các bàn đạp được nhấn. Cần Rigidbody (Is Kinematic) trên door.
/// </summary>
public class LevelGate : NetworkBehaviour
{
    public enum Rule { AllPlates, AnyPlate }

    [SerializeField] private PressurePlate[] plates;
    [SerializeField] private Rule rule = Rule.AllPlates;
    [SerializeField, Tooltip("Mở rồi thì mở luôn, không cần giữ bàn đạp")] private bool latchOpen;
    [SerializeField] private Rigidbody door;
    [SerializeField, Tooltip("Dịch chuyển cục bộ khi mở hẳn")] private Vector3 openOffset = new Vector3(0f, -3f, 0f);
    [SerializeField] private float openSpeed = 1.5f;
    [SerializeField] private float closeSpeed = 0.8f;

    [Networked] public float OpenAmount { get; set; }
    [Networked] private NetworkBool Latched { get; set; }

    public static event System.Action<LevelGate, bool> OnGateStateChanged;

    private Vector3 _closedLocal;
    private bool _seenOpen;

    public override void Spawned()
    {
        if (door != null)
        {
            door.isKinematic = true;
            _closedLocal = door.transform.localPosition;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            bool want = Latched || PlatesSatisfied();
            if (want && latchOpen) Latched = true;
            float speed = want ? openSpeed : -closeSpeed;
            OpenAmount = Mathf.Clamp01(OpenAmount + speed * Runner.DeltaTime);
        }

        if (door != null) door.MovePosition(DoorWorldPosition(OpenAmount));
    }

    public override void Render()
    {
        if (door != null) door.transform.position = DoorWorldPosition(OpenAmount);

        bool open = OpenAmount > 0.5f;
        if (open != _seenOpen)
        {
            _seenOpen = open;
            OnGateStateChanged?.Invoke(this, open);
        }
    }

    private bool PlatesSatisfied()
    {
        if (plates == null || plates.Length == 0) return false;
        bool any = false, all = true;
        foreach (PressurePlate p in plates)
        {
            bool pressed = p != null && p.Object != null && p.Object.IsValid && p.IsPressed;
            any |= pressed;
            all &= pressed;
        }
        return rule == Rule.AllPlates ? all : any;
    }

    private Vector3 DoorWorldPosition(float amount)
    {
        Transform parent = door.transform.parent;
        Vector3 local = _closedLocal + openOffset * amount;
        return parent != null ? parent.TransformPoint(local) : local;
    }
}
