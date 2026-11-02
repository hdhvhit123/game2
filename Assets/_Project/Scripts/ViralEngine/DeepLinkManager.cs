using System;
using UnityEngine;

/// <summary>
/// Mời bạn qua Zalo/TikTok bằng link 1 chạm, và tự vào phòng khi mở app từ link
/// hoặc lần đầu cài app từ link (Google Play Install Referrer).
/// Yêu cầu plugin NativeShare (github.com/yasirkula/UnityNativeShare) và
/// lớp Java com.studio.game.InstallReferrerBridge (không nằm trong bộ mã này).
/// </summary>
public class DeepLinkManager : MonoBehaviour
{
    private const string PrefReferrerChecked = "CheckedInstallReferrer";

    private string _pendingRoom;

    private void Start()
    {
        Application.deepLinkActivated += OnDeepLinkActivated;

        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            OnDeepLinkActivated(Application.absoluteURL);
            return;
        }

        if (PlayerPrefs.GetInt(PrefReferrerChecked, 0) == 0)
        {
            CheckGooglePlayInstallReferrer();
        }
    }

    private void OnDestroy()
    {
        // Bản cũ không gỡ đăng ký: đổi scene xong mở link sẽ gọi vào object đã huỷ.
        Application.deepLinkActivated -= OnDeepLinkActivated;
    }

    private void Update()
    {
        // NetworkRunnerHandler có thể chưa sẵn sàng ở frame đầu: giữ mã phòng lại và vào sau.
        if (_pendingRoom != null && NetworkRunnerHandler.Instance != null)
        {
            string room = _pendingRoom;
            _pendingRoom = null;
            _ = NetworkRunnerHandler.Instance.StartCoopSession(room);
        }
    }

    private void OnDeepLinkActivated(string url)
    {
        // Link từ bên ngoài là dữ liệu không tin cậy: bản cũ dùng new Uri() và Split()
        // nên link sai định dạng làm crash, và mã phòng tuỳ ý được đưa thẳng vào SessionName.
        if (RoomCode.TryParseFromUrl(url, out string room)) _pendingRoom = room;
        else Debug.LogWarning("Deep link không hợp lệ, bỏ qua.");
    }

    public void ShareZaloInviteLink()
    {
        NetworkRunnerHandler net = NetworkRunnerHandler.Instance;
        if (net == null || string.IsNullOrEmpty(net.CurrentRoomCode))
        {
            Debug.LogWarning("Chưa có phòng để mời. Gọi NetworkRunnerHandler.CreateNewRoom() trước.");
            return;
        }

        string message = $"Vào khiêng nồi lẩu phụ tao lẹ lên! Mã phòng {net.CurrentRoomCode}. Bấm vào đây vào thẳng phòng: {net.BuildInviteUrl()}";
        new NativeShare().SetText(message).Share();
    }

    private void CheckGooglePlayInstallReferrer()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass referrerBridge = new AndroidJavaClass("com.studio.game.InstallReferrerBridge"))
            {
                referrerBridge.CallStatic("FetchReferrer", activity, gameObject.name, "OnReferrerReceived");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Install Referrer bỏ qua: " + e.Message);
            MarkReferrerChecked();
        }
#else
        MarkReferrerChecked();
#endif
    }

    /// <summary>Được Java gọi qua UnitySendMessage.</summary>
    public void OnReferrerReceived(string referrerString)
    {
        // Chỉ đánh dấu đã kiểm tra khi thật sự nhận được kết quả (bản cũ đánh dấu trước khi gọi,
        // nếu app bị tắt giữa chừng thì mất luôn lời mời).
        MarkReferrerChecked();
        if (RoomCode.TryParseFromReferrer(referrerString, out string room)) _pendingRoom = room;
    }

    private static void MarkReferrerChecked()
    {
        PlayerPrefs.SetInt(PrefReferrerChecked, 1);
        PlayerPrefs.Save();
    }
}
