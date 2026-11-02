using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Android 6+ bắt buộc xin quyền micro lúc chạy. Bản cũ chỉ khai báo RECORD_AUDIO trong
/// AndroidManifest nên Photon Voice không thu được tiếng trên máy thật.
/// Bật Recorder của Photon Voice sau khi OnResult(true).
/// </summary>
public class MicrophonePermission : MonoBehaviour
{
    public static bool Granted { get; private set; }
    public static event Action<bool> OnResult;

    [SerializeField] private bool requestOnStart = true;

    private void Start()
    {
        if (requestOnStart) Request();
    }

    public void Request()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Report(true);
            return;
        }
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => Report(true);
        callbacks.PermissionDenied += _ => Report(false);
        Permission.RequestUserPermission(Permission.Microphone, callbacks);
#else
        Report(true);
#endif
    }

    private static void Report(bool granted)
    {
        Granted = granted;
        OnResult?.Invoke(granted);
    }
}
