// Vỏ API Photon Fusion 2, Google Mobile Ads, NativeShare - chữ ký theo tài liệu chính thức.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
namespace Fusion
{
    [AttributeUsage(AttributeTargets.Property)] public class NetworkedAttribute : Attribute { }
    public enum RpcSources { StateAuthority, InputAuthority, Proxies, All }
    public enum RpcTargets { StateAuthority, InputAuthority, Proxies, All }
    [AttributeUsage(AttributeTargets.Method)] public class RpcAttribute : Attribute { public RpcAttribute(RpcSources s, RpcTargets t) { } }
    public struct NetworkBool { bool v; public static implicit operator bool(NetworkBool b) => b.v; public static implicit operator NetworkBool(bool b) => new NetworkBool { v = b }; }
    public struct NetworkId { public bool IsValid => false; }
    public struct PlayerRef { public int PlayerId => 0; }
    public struct NetworkButtons
    {
        public NetworkButtons GetPressed(NetworkButtons prev) => default;
        public bool IsSet<T>(T button) where T : struct, Enum => false;
        public void Set<T>(T button, bool state) where T : struct, Enum { }
    }
    public struct TickTimer
    {
        public static TickTimer None => default;
        public static TickTimer CreateFromSeconds(NetworkRunner r, float s) => default;
        public bool Expired(NetworkRunner r) => false;
        public bool ExpiredOrNotRunning(NetworkRunner r) => true;
        public float? RemainingTime(NetworkRunner r) => null;
    }
    public interface INetworkInput { }
    public struct NetworkInput { public void Set<T>(T input) where T : unmanaged, INetworkInput { } }
    public class NetworkObject : MonoBehaviour { public NetworkId Id => default; public bool IsValid => true; }
    public class SimulationBehaviour : MonoBehaviour { public NetworkRunner Runner => null; }
    public class NetworkBehaviour : SimulationBehaviour
    {
        public NetworkObject Object => null;
        public bool HasStateAuthority => true;
        public bool HasInputAuthority => true;
        public virtual void Spawned() { }
        public virtual void Despawned(NetworkRunner runner, bool hasState) { }
        public virtual void FixedUpdateNetwork() { }
        public virtual void Render() { }
        public bool GetInput<T>(out T input) where T : unmanaged, INetworkInput { input = default; return false; }
    }
    public enum GameMode { Single, Shared, Server, Host, Client, AutoHostOrClient }
    public enum ShutdownReason { Ok, Error, IncompatibleConfiguration, GameIsFull, MaxCcuReached, GameClosed }
    public class StartGameResult { public bool Ok => true; public ShutdownReason ShutdownReason => default; }
    public struct SceneRef { public static SceneRef FromIndex(int i) => default; public bool IsValid => true; }
    public struct NetworkSceneInfo { public int AddSceneRef(SceneRef s, UnityEngine.SceneManagement.LoadSceneMode m = 0) => 0; }
    public interface INetworkSceneManager { }
    public class NetworkSceneManagerDefault : MonoBehaviour, INetworkSceneManager { }
    public class StartGameArgs
    {
        public GameMode GameMode;
        public string SessionName;
        public int? PlayerCount;
        public NetworkSceneInfo? Scene;
        public INetworkSceneManager SceneManager;
    }
    public class NetworkRunner : MonoBehaviour
    {
        public bool ProvideInput { get; set; }
        public bool IsRunning => true;
        public bool IsServer => true;
        public float DeltaTime => 1f / 60f;
        public void AddCallbacks(params INetworkRunnerCallbacks[] c) { }
        public void RemoveCallbacks(params INetworkRunnerCallbacks[] c) { }
        public Task<StartGameResult> StartGame(StartGameArgs a) => Task.FromResult(new StartGameResult());
        public Task Shutdown(bool destroyGameObject = true, ShutdownReason reason = ShutdownReason.Ok, bool force = false) => Task.CompletedTask;
        public NetworkObject Spawn(NetworkObject prefab, Vector3? pos = null, Quaternion? rot = null, PlayerRef? input = null) => prefab;
        public void Despawn(NetworkObject o) { }
        public void SetPlayerObject(PlayerRef p, NetworkObject o) { }
        public bool TryFindObject(NetworkId id, out NetworkObject o) { o = null; return false; }
    }
    public class SessionInfo { }
    public class HostMigrationToken { }
    public struct ReliableKey { }
    public struct SimulationMessagePtr { }
    public class NetworkRunnerCallbackArgs { public class ConnectRequest { } }
    public interface INetworkRunnerCallbacks
    {
        void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player);
        void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player);
        void OnPlayerJoined(NetworkRunner runner, PlayerRef player);
        void OnPlayerLeft(NetworkRunner runner, PlayerRef player);
        void OnInput(NetworkRunner runner, NetworkInput input);
        void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input);
        void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason);
        void OnConnectedToServer(NetworkRunner runner);
        void OnDisconnectedFromServer(NetworkRunner runner, Fusion.Sockets.NetDisconnectReason reason);
        void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token);
        void OnConnectFailed(NetworkRunner runner, Fusion.Sockets.NetAddress remoteAddress, Fusion.Sockets.NetConnectFailedReason reason);
        void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message);
        void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList);
        void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data);
        void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken);
        void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data);
        void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress);
        void OnSceneLoadDone(NetworkRunner runner);
        void OnSceneLoadStart(NetworkRunner runner);
    }
}
namespace Fusion.Sockets
{
    public enum NetDisconnectReason { Unknown }
    public struct NetAddress { }
    public enum NetConnectFailedReason { Timeout }
}
namespace GoogleMobileAds.Api
{
    public class InitializationStatus { }
    public static class MobileAds
    {
        public static bool RaiseAdEventsOnUnityMainThread { get; set; }
        public static void Initialize(Action<InitializationStatus> cb) { }
    }
    public class AdRequest { }
    public class AdError { public string GetMessage() => ""; }
    public class LoadAdError : AdError { }
    public class Reward { }
    public class RewardedAd
    {
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        public static void Load(string id, AdRequest r, Action<RewardedAd, LoadAdError> cb) { }
        public bool CanShowAd() => false;
        public void Show(Action<Reward> cb) { }
        public void Destroy() { OnAdFullScreenContentClosed?.Invoke(); OnAdFullScreenContentFailed?.Invoke(null); }
    }
}
public class NativeShare
{
    public NativeShare SetText(string t) => this;
    public NativeShare SetSubject(string s) => this;
    public NativeShare AddFile(string path, string mime = null) => this;
    public void Share() { }
}
