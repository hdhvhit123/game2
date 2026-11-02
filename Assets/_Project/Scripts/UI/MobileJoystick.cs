using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Joystick ảo: gắn lên một Image nền, kéo handle con bên trong.</summary>
public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static MobileJoystick Active { get; private set; }

    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.12f;

    public Vector2 Value { get; private set; }

    private void OnEnable() { Active = this; }

    private void OnDisable()
    {
        if (Active == this) Active = null;
        ResetStick();
    }

    public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position,
                eventData.pressEventCamera, out Vector2 local)) return;

        Vector2 radius = background.rect.size * 0.5f;
        if (radius.x <= 0f || radius.y <= 0f) return;

        Vector2 v = new Vector2(local.x / radius.x, local.y / radius.y);
        v = Vector2.ClampMagnitude(v, 1f);
        Value = v.magnitude < deadZone ? Vector2.zero : v;
        if (handle != null) handle.anchoredPosition = new Vector2(v.x * radius.x, v.y * radius.y);
    }

    public void OnPointerUp(PointerEventData eventData) => ResetStick();

    private void ResetStick()
    {
        Value = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
    }
}
