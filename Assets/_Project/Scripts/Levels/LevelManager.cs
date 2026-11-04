using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Điều khiển một màn chơi: giới thiệu → chơi → thắng/thua → sang màn kế hoặc chơi lại.
/// Mỗi scene màn chơi có đúng một LevelManager (scene object có NetworkObject).
/// </summary>
public class LevelManager : NetworkBehaviour
{
    public static LevelManager Current { get; private set; }
    public static event Action<LevelManager, LevelPhase> OnPhaseChanged;

    private static readonly List<LevelObjective> Objectives = new List<LevelObjective>();

    [Header("Thông tin màn")]
    [SerializeField, Tooltip("Trùng Id trong CampaignCatalog")] private string levelId = "L01_KhiengLau";
    [SerializeField] private string titleOverride;
    [SerializeField, TextArea] private string introText;

    [Header("Luật")]
    [SerializeField, Tooltip("0 = không giới hạn")] private float timeLimit = 180f;
    [SerializeField] private float threeStarTime = 90f;
    [SerializeField] private float twoStarTime = 140f;
    [SerializeField, Tooltip("Rơi khỏi map bao nhiêu lần thì mất 1 sao (0 = không trừ)")] private int fallsPerStarLost = 3;
    [SerializeField] private float introSeconds = 3.5f;
    [SerializeField] private float resultSeconds = 6f;
    [SerializeField, Tooltip("Thua thì tự chơi lại; thắng thì tự sang màn kế")] private bool autoAdvance = true;
    [SerializeField, Tooltip("Màn tập luyện có thể bắt đầu khi mới có 1 người")] private int minPlayersToStart = 2;

    [Header("Điểm xuất phát")]
    [SerializeField] private Transform[] playerSpawnPoints;

    [Networked] public LevelPhase Phase { get; set; }
    [Networked] private TickTimer PhaseTimer { get; set; }
    [Networked] private int StartTick { get; set; }
    [Networked] public float FinishTime { get; set; }
    [Networked] public int Stars { get; set; }
    [Networked] public int Falls { get; set; }

    public string LevelId => levelId;
    public float TimeLimit => timeLimit;
    public string IntroText => introText;
    public IReadOnlyList<LevelObjective> ActiveObjectives => Objectives;
    public bool ControlsEnabled => Object != null && Object.IsValid && Phase == LevelPhase.Playing;

    public string Title
    {
        get
        {
            if (!string.IsNullOrEmpty(titleOverride)) return titleOverride;
            int i = CampaignCatalog.IndexOf(levelId);
            return i >= 0 ? CampaignCatalog.Levels[i].Title : levelId;
        }
    }

    public float Elapsed
    {
        get
        {
            if (Object == null || !Object.IsValid) return 0f;
            if (Phase == LevelPhase.Won || Phase == LevelPhase.Lost) return FinishTime;
            if (Phase != LevelPhase.Playing) return 0f;
            return Mathf.Max(0f, (Runner.Tick - StartTick) * Runner.DeltaTime);
        }
    }

    public float TimeLeft => timeLimit > 0f ? Mathf.Max(0f, timeLimit - Elapsed) : -1f;
    public float PhaseSecondsLeft => Object != null && Object.IsValid ? (PhaseTimer.RemainingTime(Runner) ?? 0f) : 0f;

    private ObjectiveState[] _stateBuffer = new ObjectiveState[8];
    private LevelPhase _seenPhase = (LevelPhase)(-1);
    private bool _loadingScene;

    public static void Register(LevelObjective o)
    {
        if (!Objectives.Contains(o)) Objectives.Add(o);
        Objectives.Sort((a, b) => a.DisplayOrder.CompareTo(b.DisplayOrder));
    }

    public static void Unregister(LevelObjective o) => Objectives.Remove(o);

    public override void Spawned()
    {
        Current = this;
        if (HasStateAuthority)
        {
            Phase = LevelPhase.Intro;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, introSeconds);
            Falls = 0;
            PlacePlayersAtStart();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Current == this) Current = null;
    }

    public Vector3 GetPlayerSpawn(int index)
    {
        if (CheckpointZone.Current != null) return CheckpointZone.Current.GetPlayerSpawn(index);
        if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
        {
            Transform t = playerSpawnPoints[Mathf.Abs(index) % playerSpawnPoints.Length];
            if (t != null) return t.position;
        }
        return transform.position + new Vector3(index % 2 == 0 ? -1.5f : 1.5f, 1f, 0f);
    }

    /// <summary>Thua ngay (ví dụ bị nước lũ cuốn khi không còn chỗ hồi sinh). Chỉ host.</summary>
    public void FailLevel()
    {
        if (!HasStateAuthority || Phase != LevelPhase.Playing) return;
        Finish(LevelPhase.Lost, Elapsed, FillStates());
    }

    /// <summary>Người chơi gọi khi rơi khỏi map (chỉ trên host).</summary>
    public void ReportFall()
    {
        if (HasStateAuthority && Phase == LevelPhase.Playing) Falls++;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        switch (Phase)
        {
            case LevelPhase.Intro:
                if (PhaseTimer.ExpiredOrNotRunning(Runner) && SemiActiveRagdollController.All.Count >= minPlayersToStart)
                {
                    Phase = LevelPhase.Playing;
                    StartTick = Runner.Tick;
                }
                break;

            case LevelPhase.Playing:
                float elapsed = Elapsed;
                for (int i = 0; i < Objectives.Count; i++)
                    if (Objectives[i] != null && Objectives[i].Object != null && Objectives[i].Object.IsValid)
                        Objectives[i].EvaluateObjective(elapsed);

                int count = FillStates();
                LevelPhase next = LevelRules.Evaluate(Phase, elapsed, timeLimit, _stateBuffer, count);
                if (next != LevelPhase.Playing) Finish(next, elapsed, count);
                break;

            case LevelPhase.Won:
            case LevelPhase.Lost:
                if (autoAdvance && PhaseTimer.Expired(Runner)) Advance(Phase == LevelPhase.Won);
                break;
        }
    }

    private int FillStates()
    {
        if (_stateBuffer.Length < Objectives.Count) _stateBuffer = new ObjectiveState[Objectives.Count * 2];
        int n = 0;
        for (int i = 0; i < Objectives.Count; i++)
            if (Objectives[i] != null && Objectives[i].Object != null && Objectives[i].Object.IsValid)
                _stateBuffer[n++] = Objectives[i].ToState();
        return n;
    }

    private void Finish(LevelPhase result, float elapsed, int count)
    {
        Phase = result;
        FinishTime = elapsed;
        Stars = result == LevelPhase.Won
            ? LevelRules.FinalStars(elapsed, threeStarTime, twoStarTime, _stateBuffer, count, Falls, fallsPerStarLost)
            : 0;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, resultSeconds);
    }

    /// <summary>Nút "Chơi lại" / "Màn kế" trên HUD (máy nào bấm cũng được).</summary>
    public void RequestAdvance(bool goNext)
    {
        if (Object == null || !Object.IsValid) return;
        if (HasStateAuthority) Advance(goNext);
        else RPC_RequestAdvance(goNext);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestAdvance(NetworkBool goNext)
    {
        if (Phase == LevelPhase.Won || Phase == LevelPhase.Lost) Advance(goNext);
    }

    private void Advance(bool goNext)
    {
        if (_loadingScene || !Runner.IsSceneAuthority) return;

        string scenePath = SceneManager.GetActiveScene().path;
        if (goNext)
        {
            LevelInfo next = CampaignCatalog.Next(levelId);
            if (next != null) scenePath = next.ScenePath;
        }

        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
        if (buildIndex < 0)
        {
            Debug.LogError($"Scene '{scenePath}' chưa có trong Build Profiles > Scene List.");
            return;
        }

        _loadingScene = true;
        Runner.LoadScene(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);
    }

    private void PlacePlayersAtStart()
    {
        var all = SemiActiveRagdollController.All;
        for (int i = 0; i < all.Count; i++) all[i].TeleportTo(GetPlayerSpawn(i));
    }

    public override void Render()
    {
        if (Phase == _seenPhase) return;
        _seenPhase = Phase;
        if (Phase == LevelPhase.Won) CampaignProgress.Record(levelId, Stars);
        OnPhaseChanged?.Invoke(this, Phase);
    }
}
