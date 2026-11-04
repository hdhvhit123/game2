using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD của màn chơi: bảng giới thiệu (tên màn + nhiệm vụ), danh sách nhiệm vụ, đồng hồ,
/// bảng kết quả thắng/thua có sao và nút Chơi lại / Màn kế.
/// Mọi tham chiếu đều tuỳ chọn.
/// </summary>
public class LevelHUD : MonoBehaviour
{
    [Header("Giới thiệu")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private Text introTitle;
    [SerializeField] private Text introBody;

    [Header("Trong màn")]
    [SerializeField] private Text objectivesText;
    [SerializeField] private Text timerText;
    [SerializeField] private Color timerNormal = Color.white;
    [SerializeField] private Color timerUrgent = new Color(1f, 0.3f, 0.2f);
    [SerializeField] private float urgentSeconds = 15f;

    [Header("Kết quả")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultTitle;
    [SerializeField] private Text resultDetail;
    [SerializeField] private GameObject[] resultStars;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button nextButton;

    private readonly StringBuilder _sb = new StringBuilder(256);
    private float _nextTextRefresh;

    private void Awake()
    {
        if (introPanel != null) introPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (retryButton != null) retryButton.onClick.AddListener(() => LevelManager.Current?.RequestAdvance(false));
        if (nextButton != null) nextButton.onClick.AddListener(() => LevelManager.Current?.RequestAdvance(true));
    }

    private void OnEnable() => LevelManager.OnPhaseChanged += HandlePhase;
    private void OnDisable() => LevelManager.OnPhaseChanged -= HandlePhase;

    private void HandlePhase(LevelManager level, LevelPhase phase)
    {
        if (introPanel != null) introPanel.SetActive(phase == LevelPhase.Intro);
        if (resultPanel != null) resultPanel.SetActive(phase == LevelPhase.Won || phase == LevelPhase.Lost);

        if (phase == LevelPhase.Intro)
        {
            if (introTitle != null) introTitle.text = level.Title;
            if (introBody != null) introBody.text = BuildIntro(level);
        }
        else if (phase == LevelPhase.Won || phase == LevelPhase.Lost)
        {
            bool won = phase == LevelPhase.Won;
            if (resultTitle != null) resultTitle.text = won ? "Qua màn!" : "Toang rồi!";
            if (resultDetail != null)
            {
                int best = CampaignProgress.BestStars(level.LevelId);
                resultDetail.text = won
                    ? $"Thời gian {FormatTime(level.FinishTime)} · Rơi {level.Falls} lần · Kỷ lục {best}★"
                    : "Hết giờ hoặc nhiệm vụ thất bại. Chơi lại nào!";
            }
            if (resultStars != null)
                for (int i = 0; i < resultStars.Length; i++)
                    if (resultStars[i] != null) resultStars[i].SetActive(won && i < level.Stars);
            if (nextButton != null) nextButton.gameObject.SetActive(won && CampaignCatalog.Next(level.LevelId) != null);
            if (won) Haptics.Strong();
        }
    }

    private void Update()
    {
        LevelManager level = LevelManager.Current;
        if (level == null || level.Object == null || !level.Object.IsValid)
        {
            if (objectivesText != null) objectivesText.text = string.Empty;
            if (timerText != null) timerText.text = string.Empty;
            return;
        }

        if (timerText != null)
        {
            float left = level.TimeLeft;
            if (level.Phase == LevelPhase.Intro)
                timerText.text = $"Bắt đầu sau {Mathf.CeilToInt(level.PhaseSecondsLeft)}";
            else
                timerText.text = left >= 0f ? FormatTime(left) : FormatTime(level.Elapsed);
            timerText.color = left >= 0f && left <= urgentSeconds && level.Phase == LevelPhase.Playing
                ? Color.Lerp(timerNormal, timerUrgent, Mathf.PingPong(Time.time * 3f, 1f))
                : timerNormal;
        }

        // Ghép chuỗi 5 lần/giây là đủ, tránh tạo rác mỗi frame.
        if (objectivesText != null && Time.unscaledTime >= _nextTextRefresh)
        {
            _nextTextRefresh = Time.unscaledTime + 0.2f;
            _sb.Clear();
            var objectives = level.ActiveObjectives;
            for (int i = 0; i < objectives.Count; i++)
            {
                LevelObjective o = objectives[i];
                if (o == null || o.Object == null || !o.Object.IsValid) continue;
                _sb.Append(o.Required ? "• " : "◦ (phụ) ").Append(o.GetStatusText()).Append('\n');
            }
            objectivesText.text = _sb.ToString();
        }
    }

    private static string BuildIntro(LevelManager level)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(level.IntroText)) sb.Append(level.IntroText).Append("\n\n");
        int i = CampaignCatalog.IndexOf(level.LevelId);
        if (string.IsNullOrEmpty(level.IntroText) && i >= 0) sb.Append(CampaignCatalog.Levels[i].Tagline).Append("\n\n");
        if (level.TimeLimit > 0f) sb.Append("Thời gian: ").Append(FormatTime(level.TimeLimit));
        return sb.ToString();
    }

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }
}
