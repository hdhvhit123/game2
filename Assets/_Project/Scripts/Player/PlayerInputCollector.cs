using Fusion;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Gom input bàn phím + điều khiển cảm ứng mỗi frame, rồi NetworkRunnerHandler.OnInput lấy ra mỗi tick.
/// Bản cũ bật ProvideInput nhưng không có ai gửi input, nên nhân vật không bao giờ di chuyển.
/// Nút được "chốt" giữa các tick để cú chạm nhanh hơn 1 tick không bị mất.
/// </summary>
public class PlayerInputCollector : MonoBehaviour
{
    public static PlayerInputCollector Instance { get; private set; }

    [Tooltip("Di chuyển theo hướng camera (nên bật khi camera xoay)")]
    [SerializeField] private bool cameraRelative = true;
    [Tooltip("Trên điện thoại: chạm 1 lần để nắm/thả quai nồi thay vì phải giữ ngón tay")]
    [SerializeField] private bool mobileGrabToggle = true;

    private Vector2 _rawMove;
    private bool _jumpHeld, _grabHeld, _slapHeld;
    private bool _jumpLatched, _slapLatched;
    private bool _grabToggled;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        Vector2 move = Vector2.zero;
        bool jump = false, grab = false, slap = false;

        ReadKeyboard(ref move, ref jump, ref grab, ref slap);

        if (MobileJoystick.Active != null) move += MobileJoystick.Active.Value;
        jump |= MobileButton.IsHeld(InputButtons.Jump);
        slap |= MobileButton.IsHeld(InputButtons.Slap);

        if (MobileButton.ConsumeTap(InputButtons.Grab) && mobileGrabToggle) _grabToggled = !_grabToggled;
        grab |= mobileGrabToggle ? _grabToggled : MobileButton.IsHeld(InputButtons.Grab);

        _rawMove = Vector2.ClampMagnitude(move, 1f);

        if (jump && !_jumpHeld) _jumpLatched = true;
        if (slap && !_slapHeld) _slapLatched = true;
        _jumpHeld = jump;
        _grabHeld = grab;
        _slapHeld = slap;
    }

    /// <summary>Gọi từ OnInput mỗi tick mạng.</summary>
    public NetworkInputData ConsumeInput()
    {
        var data = new NetworkInputData { moveDirection = ToWorld(_rawMove) };
        data.buttons.Set(InputButtons.Jump, _jumpHeld || _jumpLatched);
        data.buttons.Set(InputButtons.Grab, _grabHeld);
        data.buttons.Set(InputButtons.Slap, _slapHeld || _slapLatched);
        _jumpLatched = false;
        _slapLatched = false;
        return data;
    }

    /// <summary>Bị tát/rơi thì tự buông quai để UI toggle không bị lệch trạng thái.</summary>
    public void ForceReleaseGrab() => _grabToggled = false;

    private Vector2 ToWorld(Vector2 input)
    {
        if (!cameraRelative || input.sqrMagnitude < 1e-6f) return input;
        Camera cam = Camera.main;
        if (cam == null) return input;

        Vector3 fwd = cam.transform.forward; fwd.y = 0f;
        Vector3 right = cam.transform.right; right.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return input;
        fwd.Normalize(); right.Normalize();

        Vector3 world = fwd * input.y + right * input.x;
        return Vector2.ClampMagnitude(new Vector2(world.x, world.z), 1f);
    }

    private static void ReadKeyboard(ref Vector2 move, ref bool jump, ref bool grab, ref bool slap)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
        jump |= kb.spaceKey.isPressed;
        grab |= kb.eKey.isPressed || kb.leftShiftKey.isPressed;
        slap |= kb.fKey.isPressed || kb.jKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
        move.x += Input.GetAxisRaw("Horizontal");
        move.y += Input.GetAxisRaw("Vertical");
        jump |= Input.GetKey(KeyCode.Space);
        grab |= Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.LeftShift);
        slap |= Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.J);
#endif
    }
}
