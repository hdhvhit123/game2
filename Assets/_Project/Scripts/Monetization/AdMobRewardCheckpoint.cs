using System;
using System.Collections;
using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// Quảng cáo có thưởng "Bảo hiểm nồi lẩu": xem xong thì nồi hồi lại đầy nước tại checkpoint.
/// </summary>
public class AdMobRewardCheckpoint : MonoBehaviour
{
    public static AdMobRewardCheckpoint Instance { get; private set; }

    // ID THỬ NGHIỆM của Google. Thay bằng ID thật trước khi phát hành, nếu không sẽ không có doanh thu.
    [SerializeField] private string androidRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
    [SerializeField] private float maxRetryDelay = 64f;

    public bool IsReady => _rewardedAd != null && _rewardedAd.CanShowAd();

    private RewardedAd _rewardedAd;
    private bool _isLoading;
    private int _retryCount;
    private Coroutine _retryRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Callback của SDK mặc định có thể chạy trên luồng Java -> gọi API Unity sẽ crash.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(_ => LoadRewardedAd());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _rewardedAd?.Destroy();
        _rewardedAd = null;
    }

    public void LoadRewardedAd()
    {
        if (_isLoading) return;
        _isLoading = true;

        _rewardedAd?.Destroy();
        _rewardedAd = null;

        RewardedAd.Load(androidRewardedUnitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            _isLoading = false;
            if (this == null) { ad?.Destroy(); return; }

            if (error != null || ad == null)
            {
                // Bản cũ không thử lại: lỗi mạng một lần là cả phiên chơi không còn quảng cáo.
                Debug.LogWarning($"Tải quảng cáo thất bại: {error?.GetMessage()}");
                ScheduleRetry();
                return;
            }

            _retryCount = 0;
            _rewardedAd = ad;
            // Chỉ tải quảng cáo mới sau khi quảng cáo cũ đã đóng (bản cũ Destroy ngay trong
            // callback thưởng, lúc quảng cáo còn đang hiển thị).
            ad.OnAdFullScreenContentClosed += LoadRewardedAd;
            ad.OnAdFullScreenContentFailed += _ => LoadRewardedAd();
        });
    }

    private void ScheduleRetry()
    {
        if (_retryRoutine != null) StopCoroutine(_retryRoutine);
        float delay = Mathf.Min(maxRetryDelay, Mathf.Pow(2f, _retryCount) * 2f);
        _retryCount++;
        _retryRoutine = StartCoroutine(RetryAfter(delay));
    }

    private IEnumerator RetryAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        _retryRoutine = null;
        LoadRewardedAd();
    }

    /// <summary>
    /// Hiện quảng cáo. onRewarded chạy khi người chơi xem đủ; onUnavailable chạy khi chưa có quảng cáo
    /// (bản cũ im lặng không làm gì, nút bấm như bị hỏng).
    /// </summary>
    public void ShowRewardedAd(Action onRewarded, Action onUnavailable = null)
    {
        if (!IsReady)
        {
            onUnavailable?.Invoke();
            if (!_isLoading && _retryRoutine == null) LoadRewardedAd();
            return;
        }

        _rewardedAd.Show(_ => onRewarded?.Invoke());
    }

    /// <summary>Tiện ích: xem quảng cáo để nồi lẩu hồi lại đầy nước.</summary>
    public void ShowPotInsuranceAd(Action onUnavailable = null)
    {
        ShowRewardedAd(() =>
        {
            FragileCarryObject pot = FragileCarryObject.Current;
            if (pot == null) return;
            if (pot.IsShattered) pot.GrantInsurance();   // hồi nồi sắp tới sẽ đầy nước
            else pot.RequestReset(true);                  // hồi ngay về checkpoint với nồi đầy
        }, onUnavailable);
    }
}
