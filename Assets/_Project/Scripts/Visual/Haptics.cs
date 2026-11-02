using UnityEngine;

/// <summary>Rung điện thoại cho các khoảnh khắc lớn. Có thể tắt trong cài đặt.</summary>
public static class Haptics
{
    private const string PrefKey = "HapticsEnabled";
    private static float _lastBuzzTime = -10f;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
        set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void Strong()
    {
        if (!Enabled || Time.unscaledTime - _lastBuzzTime < 0.5f) return;
        _lastBuzzTime = Time.unscaledTime;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
