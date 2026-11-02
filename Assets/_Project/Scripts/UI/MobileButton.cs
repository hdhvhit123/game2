using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Nút cảm ứng (Nhảy / Nắm / Tát). Hỗ trợ đa chạm: mỗi nút độc lập.</summary>
public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private InputButtons button;
    [SerializeField] private float pressedScale = 0.88f;

    private static readonly bool[] Held = new bool[3];
    private static readonly bool[] Tapped = new bool[3];

    public static bool IsHeld(InputButtons b) => Held[(int)b];

    public static bool ConsumeTap(InputButtons b)
    {
        bool t = Tapped[(int)b];
        Tapped[(int)b] = false;
        return t;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Held[(int)button] = true;
        Tapped[(int)button] = true;
        transform.localScale = Vector3.one * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Held[(int)button] = false;
        transform.localScale = Vector3.one;
    }

    private void OnDisable()
    {
        Held[(int)button] = false;
        transform.localScale = Vector3.one;
    }
}
