using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;

/// <summary>
/// Quản lý phiên chơi Photon Fusion 2: tạo/vào phòng, spawn người chơi, gửi input và tự kết nối lại.
/// </summary>
public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    public enum ConnectionState { Idle, Connecting, Connected, Reconnecting, Failed }

    public static NetworkRunnerHandler Instance { get; private set; }
    public static event Action<ConnectionState> OnConnectionStateChanged;

    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField, Tooltip("-1 = dùng scene đang mở")] private int gameplaySceneBuildIndex = -1;
    [SerializeField] private int maxReconnectAttempts = 4;
    [SerializeField] private string inviteHost = "dayoannghiet.vn";

    public string CurrentRoomCode { get; private set; }
    public ConnectionState State { get; private set; } = ConnectionState.Idle;
    public NetworkRunner Runner => _currentRunner;
    public string InviteHost => inviteHost;

    private NetworkRunner _currentRunner;
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private bool _isStarting;
    private bool _intentionalShutdown;
    private bool _quitting;
    private int _reconnectAttempt;
    private bool _reconnectLoopRunning;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
        }
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnApplicationQuit() => _quitting = true;

    /// <summary>Tạo phòng mới với mã ngẫu nhiên rồi vào luôn (người mời bấm nút này).</summary>
    public Task<bool> CreateNewRoom() => StartCoopSession(RoomCode.Generate());

    public string BuildInviteUrl() => RoomCode.BuildInviteUrl(inviteHost, CurrentRoomCode);

    public async Task<bool> StartCoopSession(string roomCode)
    {
        if (!RoomCode.TrySanitize(roomCode, out string code))
        {
            Debug.LogWarning($"Mã phòng không hợp lệ: '{roomCode}'");
            return false;
        }
        // Deep link và Install Referrer có thể gọi cùng lúc lúc mở app: chỉ cho 1 lần khởi động.
        if (_isStarting) return false;
        if (_currentRunner != null && _currentRunner.IsRunning && CurrentRoomCode == code) return true;

        _reconnectAttempt = 0;
        return await StartInternal(code, ConnectionState.Connecting);
    }

    private async Task<bool> StartInternal(string code, ConnectionState stateWhileStarting)
    {
        _isStarting = true;
        SetState(stateWhileStarting);
        try
        {
            CurrentRoomCode = code;
            await ShutdownCurrentRunner();
            if (this == null) return false;

            _currentRunner = Instantiate(runnerPrefab);
            _currentRunner.name = $"Runner_{code}";
            _currentRunner.ProvideInput = true;
            _currentRunner.AddCallbacks(this);

            var sceneManager = _currentRunner.GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null) sceneManager = _currentRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();

            int sceneIndex = gameplaySceneBuildIndex >= 0 ? gameplaySceneBuildIndex : SceneManager.GetActiveScene().buildIndex;
            var sceneInfo = new NetworkSceneInfo();
            SceneRef sceneRef = SceneRef.FromIndex(sceneIndex);
            if (sceneRef.IsValid) sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

            var startArgs = new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = code,
                PlayerCount = 2,
                Scene = sceneInfo, // bản cũ không truyền scene nên nồi lẩu (scene object) không được đăng ký mạng
                SceneManager = sceneManager
            };

            StartGameResult result = await _currentRunner.StartGame(startArgs);
            if (this == null) return false;

            if (!result.Ok)
            {
                Debug.LogError($"Không thể vào phòng {code}: {result.ShutdownReason}");
                SetState(ConnectionState.Failed);
                return false;
            }

            _reconnectAttempt = 0;
            SetState(ConnectionState.Connected);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetState(ConnectionState.Failed);
            return false;
        }
        finally
        {
            _isStarting = false;
        }
    }

    public async Task LeaveSession()
    {
        await ShutdownCurrentRunner();
        CurrentRoomCode = null;
        SetState(ConnectionState.Idle);
    }

    private async Task ShutdownCurrentRunner()
    {
        if (_currentRunner == null) return;
        NetworkRunner old = _currentRunner;
        _currentRunner = null;
        _spawnedPlayers.Clear();
        _intentionalShutdown = true;
        try
        {
            old.RemoveCallbacks(this);
            if (old.IsRunning) await old.Shutdown();
        }
        finally
        {
            _intentionalShutdown = false;
        }
    }

    private async void TryReconnect(ShutdownReason reason)
    {
        if (_quitting || _reconnectLoopRunning || string.IsNullOrEmpty(CurrentRoomCode)) return;

        // Các lỗi mà thử lại cũng vô ích.
        if (reason == ShutdownReason.Ok || reason == ShutdownReason.GameIsFull ||
            reason == ShutdownReason.IncompatibleConfiguration || reason == ShutdownReason.MaxCcuReached)
        {
            SetState(ConnectionState.Failed);
            return;
        }

        _reconnectLoopRunning = true;
        try
        {
        while (_reconnectAttempt < maxReconnectAttempts && !_quitting)
        {
            _reconnectAttempt++;
            int delayMs = 1000 * (1 << (_reconnectAttempt - 1)); // 1s, 2s, 4s, 8s
            Debug.Log($"Mất kết nối ({reason}). Thử lại lần {_reconnectAttempt}/{maxReconnectAttempts} sau {delayMs / 1000}s...");
            SetState(ConnectionState.Reconnecting);
            await Task.Delay(delayMs);
            if (this == null || _quitting) return;
            if (_currentRunner != null && _currentRunner.IsRunning) return; // người chơi đã tự vào lại

            // AutoHostOrClient: nếu host cũ đã thoát, người còn lại sẽ thành host mới của cùng phòng.
            if (_isStarting) continue;
            if (await StartInternal(CurrentRoomCode, ConnectionState.Reconnecting)) return;
        }
        SetState(ConnectionState.Failed);
        }
        finally
        {
            _reconnectLoopRunning = false;
        }
    }

    private void SetState(ConnectionState s)
    {
        if (State == s) return;
        State = s;
        OnConnectionStateChanged?.Invoke(s);
    }

    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        int index = _spawnedPlayers.Count;
        if (CheckpointZone.Current != null) return CheckpointZone.Current.GetPlayerSpawn(index);
        if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[index % spawnPoints.Length] != null)
            return spawnPoints[index % spawnPoints.Length].position;
        return new Vector3(index == 0 ? -1.5f : 1.5f, 1.5f, 0f);
    }

    // ------------------------------------------------------------------ INetworkRunnerCallbacks

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer || playerPrefab == null || _spawnedPlayers.ContainsKey(player)) return;
        NetworkObject obj = runner.Spawn(playerPrefab, GetSpawnPosition(player), Quaternion.identity, player);
        runner.SetPlayerObject(player, obj);
        _spawnedPlayers[player] = obj;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject obj))
        {
            if (obj != null) runner.Despawn(obj);
            _spawnedPlayers.Remove(player);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerInputCollector collector = PlayerInputCollector.Instance;
        input.Set(collector != null ? collector.ConsumeInput() : default(NetworkInputData));
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (runner == _currentRunner) _currentRunner = null;
        _spawnedPlayers.Clear();
        if (_intentionalShutdown || _quitting) return;
        TryReconnect(shutdownReason);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"Ngắt kết nối khỏi host: {reason}");
        // Runner sẽ tự Shutdown ngay sau đó -> OnShutdown xử lý kết nối lại.
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"Kết nối thất bại: {reason}");
    }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
