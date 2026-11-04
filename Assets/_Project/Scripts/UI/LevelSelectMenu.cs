using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu chọn màn (scene Menu): liệt kê các màn trong CampaignCatalog với số sao, khoá màn chưa mở,
/// tạo phòng ở màn đã chọn rồi gửi link mời; hoặc nhập mã phòng để vào cùng bạn.
/// </summary>
public class LevelSelectMenu : MonoBehaviour
{
    [SerializeField, Tooltip("Prefab nút: có Button, một Text tên màn, một Text sao")] private Button levelButtonPrefab;
    [SerializeField] private Transform listRoot;
    [SerializeField] private Text totalStarsText;
    [SerializeField] private InputField roomCodeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private Text messageText;
    [SerializeField] private DeepLinkManager deepLinkManager;
    [SerializeField, Tooltip("Bật để thử mọi màn khi đang phát triển")] private bool unlockAll;

    private bool _busy;

    private void Start()
    {
        BuildList();
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void BuildList()
    {
        if (levelButtonPrefab == null || listRoot == null) return;

        var levels = CampaignCatalog.Levels;
        for (int i = 0; i < levels.Count; i++)
        {
            LevelInfo info = levels[i];
            bool unlocked = unlockAll || CampaignProgress.IsUnlocked(i);
            int stars = CampaignProgress.BestStars(info.Id);

            Button b = Instantiate(levelButtonPrefab, listRoot);
            b.interactable = unlocked;
            Text[] texts = b.GetComponentsInChildren<Text>(true);
            if (texts.Length > 0) texts[0].text = $"{i + 1}. {info.Title}";
            if (texts.Length > 1) texts[1].text = unlocked ? Stars(stars) : "🔒";

            int captured = i;
            b.onClick.AddListener(() => OnLevelClicked(captured));
        }

        if (totalStarsText != null)
            totalStarsText.text = $"{CampaignProgress.TotalStars()}/{levels.Count * 3}★";
    }

    private static string Stars(int n) => new string('★', n) + new string('☆', 3 - n);

    private void OnLevelClicked(int index)
    {
        if (_busy) return;
        LevelInfo info = CampaignCatalog.Levels[index];
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(info.ScenePath);
        if (buildIndex < 0)
        {
            Say($"Chưa thêm scene {info.ScenePath} vào Build Profiles.");
            return;
        }
        StartCoroutine(CreateRoomRoutine(buildIndex, info.Title));
    }

    private IEnumerator CreateRoomRoutine(int buildIndex, string title)
    {
        NetworkRunnerHandler net = NetworkRunnerHandler.Instance;
        if (net == null) yield break;
        _busy = true;
        Say($"Đang tạo phòng \"{title}\"...");
        var task = net.CreateNewRoom(buildIndex);
        while (!task.IsCompleted) yield return null;
        _busy = false;

        if (!task.Result) { Say("Không tạo được phòng, kiểm tra mạng."); yield break; }
        Say($"Phòng {net.CurrentRoomCode}. Gửi link cho bạn để vào chơi!");
        if (deepLinkManager != null) deepLinkManager.ShareZaloInviteLink();
    }

    private void OnJoinClicked()
    {
        if (_busy || roomCodeInput == null) return;
        if (!RoomCode.TrySanitize(roomCodeInput.text, out string code))
        {
            Say("Mã phòng gồm 4-12 chữ hoặc số.");
            return;
        }
        StartCoroutine(JoinRoutine(code));
    }

    private IEnumerator JoinRoutine(string code)
    {
        NetworkRunnerHandler net = NetworkRunnerHandler.Instance;
        if (net == null) yield break;
        _busy = true;
        Say($"Đang vào phòng {code}...");
        var task = net.JoinRoom(code);
        while (!task.IsCompleted) yield return null;
        _busy = false;
        if (!task.Result) Say("Không vào được phòng. Mã đúng chưa?");
    }

    private void Say(string msg)
    {
        if (messageText != null) messageText.text = msg;
    }
}
