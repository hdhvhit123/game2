using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD: thanh nước lẩu (nhấp nháy khi nguy hiểm), thông báo trạng thái, đếm ngược hồi nồi,
/// nút mời bạn / xem quảng cáo bảo hiểm / khoe ảnh highlight, và màn hình thắng có sao.
/// Mọi tham chiếu đều tuỳ chọn: thiếu cái nào thì bỏ qua cái đó.
/// </summary>
public class CoopHUD : MonoBehaviour
{
    [Header("Soup meter")]
    [SerializeField] private Image soupFill;
    [SerializeField] private Gradient soupColor;
    [SerializeField] private RectTransform soupMeterRoot;

    [Header("Messages")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text roomCodeText;
    [SerializeField] private Text toastText;

    [Header("Panels & buttons")]
    [SerializeField] private GameObject shatterPanel;
    [SerializeField] private Text shatterCountdownText;
    [SerializeField] private Button insuranceAdButton;
    [SerializeField] private Button shareHighlightButton;
    [SerializeField] private Button inviteButton;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject[] winStars;

    [Header("Refs")]
    [SerializeField] private DeepLinkManager deepLinkManager;
    [SerializeField] private RingBufferRecorder recorder;

    private float _displayedSoup = 1f;
    private Coroutine _toastRoutine;

    private void Awake()
    {
        if (soupColor == null || IsPlainWhite(soupColor))
        {
            soupColor = new Gradient();
            soupColor.SetKeys(
                new[] { new GradientColorKey(new Color(0.9f, 0.15f, 0.1f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.1f), 0.5f), new GradientColorKey(new Color(1f, 0.85f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }

        if (insuranceAdButton != null) insuranceAdButton.onClick.AddListener(OnInsuranceClicked);
        if (shareHighlightButton != null)
        {
            shareHighlightButton.onClick.AddListener(() => { if (recorder != null) recorder.ShareLastHighlight(); });
            shareHighlightButton.gameObject.SetActive(false);
        }
        if (inviteButton != null) inviteButton.onClick.AddListener(OnInviteClicked);
        if (shatterPanel != null) shatterPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (toastText != null) toastText.gameObject.SetActive(false);
    }

    private static bool IsPlainWhite(Gradient g)
    {
        foreach (GradientColorKey k in g.colorKeys)
            if (k.color != Color.white) return false;
        return true;
    }

    private void OnEnable()
    {
        FragileCarryObject.OnObjectShatteredEvent += HandleShatter;
        FragileCarryObject.OnPotReset += HandleReset;
        FragileCarryObject.OnPotDelivered += HandleDelivered;
        CheckpointZone.OnCheckpointReached += HandleCheckpoint;
        RingBufferRecorder.OnHighlightReady += HandleHighlight;
        NetworkRunnerHandler.OnConnectionStateChanged += HandleConnection;
    }

    private void OnDisable()
    {
        FragileCarryObject.OnObjectShatteredEvent -= HandleShatter;
        FragileCarryObject.OnPotReset -= HandleReset;
        FragileCarryObject.OnPotDelivered -= HandleDelivered;
        CheckpointZone.OnCheckpointReached -= HandleCheckpoint;
        RingBufferRecorder.OnHighlightReady -= HandleHighlight;
        NetworkRunnerHandler.OnConnectionStateChanged -= HandleConnection;
    }

    private void Update()
    {
        FragileCarryObject pot = FragileCarryObject.Current;
        bool potValid = pot != null && pot.Object != null && pot.Object.IsValid;

        float soup = potValid ? pot.Soup : 1f;
        float danger = potValid ? pot.Danger : 0f;
        _displayedSoup = Mathf.Lerp(_displayedSoup, soup, 1f - Mathf.Exp(-10f * Time.deltaTime));

        if (soupFill != null)
        {
            soupFill.fillAmount = _displayedSoup;
            Color c = soupColor.Evaluate(_displayedSoup);
            if (danger > 0.3f) c = Color.Lerp(c, Color.white, (Mathf.Sin(Time.time * 18f) * 0.5f + 0.5f) * danger * 0.6f);
            soupFill.color = c;
        }
        if (soupMeterRoot != null && soupMeterRoot.gameObject.activeSelf != potValid)
            soupMeterRoot.gameObject.SetActive(potValid); // màn không có nồi lẩu thì ẩn thanh nước
        if (soupMeterRoot != null && potValid)
        {
            float pulse = 1f + danger * 0.08f * Mathf.Sin(Time.time * 18f);
            soupMeterRoot.localScale = new Vector3(pulse, pulse, 1f);
        }

        if (shatterPanel != null && shatterPanel.activeSelf && shatterCountdownText != null && potValid)
            shatterCountdownText.text = $"Hồi nồi sau {Mathf.CeilToInt(pot.ResetSecondsLeft)}s";

        if (insuranceAdButton != null)
            insuranceAdButton.interactable = AdMobRewardCheckpoint.Instance != null && AdMobRewardCheckpoint.Instance.IsReady;

        if (roomCodeText != null)
        {
            NetworkRunnerHandler net = NetworkRunnerHandler.Instance;
            roomCodeText.text = net != null && !string.IsNullOrEmpty(net.CurrentRoomCode) ? $"Phòng: {net.CurrentRoomCode}" : string.Empty;
        }

        if (statusText != null)
        {
            int players = SemiActiveRagdollController.All.Count;
            statusText.text = potValid && players < 2 ? "Đang chờ đồng đội... Bấm Mời để gửi link Zalo" : string.Empty;
        }
    }

    private void HandleShatter()
    {
        if (shatterPanel != null) shatterPanel.SetActive(true);
        ShowToast("Nồi lẩu toang rồi!!!");
    }

    private void HandleReset(FragileCarryObject pot)
    {
        if (shatterPanel != null) shatterPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        _displayedSoup = pot.Soup;
    }

    private void HandleDelivered(FragileCarryObject pot, int stars)
    {
        // Màn chơi có LevelManager thì LevelHUD hiện bảng kết quả chung cho mọi loại nhiệm vụ.
        if (LevelManager.Current != null) { ShowToast("Giao hàng thành công!"); return; }
        if (winPanel != null) winPanel.SetActive(true);
        if (winStars != null)
            for (int i = 0; i < winStars.Length; i++)
                if (winStars[i] != null) winStars[i].SetActive(i < stars);
        ShowToast(stars >= 3 ? "Hoàn hảo! Không rơi giọt nào!" : "Giao lẩu thành công!");
    }

    private void HandleCheckpoint(CheckpointZone zone) => ShowToast("Đã lưu checkpoint");

    private void HandleHighlight(string path)
    {
        if (shareHighlightButton != null) shareHighlightButton.gameObject.SetActive(true);
    }

    private void HandleConnection(NetworkRunnerHandler.ConnectionState state)
    {
        switch (state)
        {
            case NetworkRunnerHandler.ConnectionState.Reconnecting: ShowToast("Mất mạng, đang kết nối lại..."); break;
            case NetworkRunnerHandler.ConnectionState.Failed: ShowToast("Không kết nối được. Kiểm tra mạng rồi thử lại."); break;
            case NetworkRunnerHandler.ConnectionState.Connected: ShowToast("Đã vào phòng!"); break;
        }
    }

    private void OnInsuranceClicked()
    {
        if (AdMobRewardCheckpoint.Instance == null) return;
        AdMobRewardCheckpoint.Instance.ShowPotInsuranceAd(() => ShowToast("Chưa có quảng cáo, thử lại sau nhé"));
    }

    private void OnInviteClicked()
    {
        NetworkRunnerHandler net = NetworkRunnerHandler.Instance;
        if (net == null) return;
        if (string.IsNullOrEmpty(net.CurrentRoomCode)) StartCoroutine(CreateRoomThenInvite(net));
        else if (deepLinkManager != null) deepLinkManager.ShareZaloInviteLink();
    }

    private IEnumerator CreateRoomThenInvite(NetworkRunnerHandler net)
    {
        var task = net.CreateNewRoom();
        while (!task.IsCompleted) yield return null;
        if (task.Result && deepLinkManager != null) deepLinkManager.ShareZaloInviteLink();
    }

    public void ShowToast(string message, float seconds = 2f)
    {
        if (toastText == null) return;
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(ToastRoutine(message, seconds));
    }

    private IEnumerator ToastRoutine(string message, float seconds)
    {
        toastText.text = message;
        toastText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(seconds);
        toastText.gameObject.SetActive(false);
        _toastRoutine = null;
    }
}
