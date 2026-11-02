// Vỏ API UnityEngine (chữ ký giống Unity 6) chỉ để kiểm tra biên dịch.
using System;
using System.Collections;
namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static T Instantiate<T>(T original) where T : Object => original;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => 0;
    }
    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public T GetComponentInParent<T>() => default;
        public bool TryGetComponent<T>(out T c) { c = default; return false; }
    }
    public class Behaviour : Component { public bool enabled { get; set; } public bool isActiveAndEnabled => true; }
    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator e) => null;
        public void StopCoroutine(Coroutine c) { }
    }
    public class Coroutine { }
    public class YieldInstruction { }
    public class CustomYieldInstruction : IEnumerator { public object Current => null; public bool MoveNext() => false; public void Reset() { } }
    public class WaitForSecondsRealtime : CustomYieldInstruction { public WaitForSecondsRealtime(float t) { } }
    public class GameObject : Object
    {
        public GameObject(string name) { }
        public Transform transform => null;
        public T AddComponent<T>() where T : Component => default;
        public T GetComponent<T>() => default;
        public void SetActive(bool v) { }
        public bool activeSelf => true;
    }
    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 forward => Vector3.forward;
        public Vector3 right => Vector3.right;
        public Vector3 up => Vector3.up;
        public void SetParent(Transform p, bool worldStays) { }
        public Transform Find(string n) => null;
        public Vector3 InverseTransformDirection(Vector3 d) => d;
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { }
        public IEnumerator GetEnumerator() { yield break; }
    }
    public class RectTransform : Transform { public Rect rect => default; public Vector2 anchoredPosition { get; set; } }
    public struct Rect { public Vector2 size => default; }
    public struct Bounds
    {
        public Bounds(Vector3 c, Vector3 s) { }
        public void Encapsulate(Vector3 p) { }
        public Vector3 size => default;
    }
    public struct LayerMask
    {
        public int value { get; set; }
        public static implicit operator int(LayerMask m) => m.value;
        public static implicit operator LayerMask(int i) => new LayerMask { value = i };
    }
    public enum ForceMode { Force, Acceleration, Impulse, VelocityChange }
    public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
    public class Rigidbody : Component
    {
        public Vector3 linearVelocity { get; set; }
        public Vector3 angularVelocity { get; set; }
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 worldCenterOfMass => default;
        public bool freezeRotation { get; set; }
        public bool isKinematic { get; set; }
        public float mass { get; set; }
        public RigidbodyInterpolation interpolation { get; set; }
        public void AddForce(Vector3 f, ForceMode m = ForceMode.Force) { }
        public void AddTorque(Vector3 f, ForceMode m = ForceMode.Force) { }
        public void AddForceAtPosition(Vector3 f, Vector3 p, ForceMode m = ForceMode.Force) { }
        public Vector3 GetPointVelocity(Vector3 p) => default;
        public void MoveRotation(Quaternion q) { }
    }
    public class Collider : Component { public Rigidbody attachedRigidbody => null; public bool isTrigger { get; set; } }
    public class CapsuleCollider : Collider { }
    public class Collision { public Rigidbody rigidbody => null; public Vector3 relativeVelocity => default; }
    public struct RaycastHit { public Vector3 point; public float distance; public Rigidbody rigidbody => null; }
    public static class Physics
    {
        public static bool SphereCast(Vector3 o, float r, Vector3 d, out RaycastHit h, float max, int mask, QueryTriggerInteraction q) { h = default; return false; }
        public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit h, float max, int mask, QueryTriggerInteraction q) { h = default; return false; }
        public static int OverlapSphereNonAlloc(Vector3 p, float r, Collider[] res, int mask, QueryTriggerInteraction q) => 0;
    }
    public class Animator : Behaviour { }
    public class Camera : Behaviour
    {
        public static Camera main => null;
        public RenderTexture targetTexture { get; set; }
        public void CopyFrom(Camera c) { }
        public void Render() { }
    }
    public enum RenderTextureFormat { ARGB32 }
    public enum TextureFormat { RGB24, RGBA32 }
    public class Texture : Object { }
    public class RenderTexture : Texture
    {
        public RenderTexture(int w, int h, int d, RenderTextureFormat f) { }
        public bool Create() => true;
        public void Release() { }
    }
    public class Texture2D : Texture
    {
        public Texture2D(int w, int h, TextureFormat f, bool mip) { }
        public void LoadRawTextureData(byte[] d) { }
        public void Apply(bool a, bool b) { }
    }
    public static class ImageConversion { public static byte[] EncodeToJPG(this Texture2D t, int q) => new byte[0]; }
    public class LineRenderer : Component
    {
        public int positionCount { get; set; }
        public bool useWorldSpace { get; set; }
        public int numCapVertices { get; set; }
        public int numCornerVertices { get; set; }
        public bool enabled { get; set; }
        public Color startColor { get; set; }
        public Color endColor { get; set; }
        public float widthMultiplier { get; set; }
        public void SetPositions(Vector3[] p) { }
    }
    public static class Time
    {
        public static float deltaTime => 0.016f;
        public static float unscaledDeltaTime => 0.016f;
        public static float time => 0f;
        public static float unscaledTime => 0f;
    }
    public static class Debug
    {
        public static void Log(object o) { }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { }
        public static void LogException(Exception e) { }
    }
    public static class Application
    {
        public static bool runInBackground { get; set; }
        public static string absoluteURL => "";
        public static string temporaryCachePath => "";
        public static event Action<string> deepLinkActivated;
        static void Touch() { deepLinkActivated?.Invoke(""); }
    }
    public static class PlayerPrefs
    {
        public static int GetInt(string k, int d) => d;
        public static void SetInt(string k, int v) { }
        public static void Save() { }
    }
    public static class SystemInfo { public static bool supportsAsyncGPUReadback => true; }
    public static class Handheld { public static void Vibrate() { } }
    public class AnimationCurve { public static AnimationCurve Linear(float a, float b, float c, float d) => new AnimationCurve(); }
    public struct GradientColorKey { public Color color; public GradientColorKey(Color c, float t) { color = c; } }
    public struct GradientAlphaKey { public GradientAlphaKey(float a, float t) { } }
    public class Gradient
    {
        public GradientColorKey[] colorKeys => new GradientColorKey[0];
        public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a) { }
        public Color Evaluate(float t) => default;
    }
    public class Shader : Object { public static Shader Find(string n) => null; }
    public class Material : Object { public Material(Shader s) { } }
    public enum ParticleSystemSimulationSpace { Local, World }
    public enum ParticleSystemShapeType { Cone, Circle }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear, StopEmitting }
    public enum ParticleSystemRenderMode { Billboard }
    public enum ParticleSystemCollisionType { Planes, World }
    public enum ParticleSystemCollisionQuality { High, Medium, Low }
    public class ParticleSystemRenderer : Component { public Material sharedMaterial { get; set; } public ParticleSystemRenderMode renderMode { get; set; } }
    public class ParticleSystem : Component
    {
        public struct MinMaxCurve
        {
            public MinMaxCurve(float c) { }
            public MinMaxCurve(float a, float b) { }
            public MinMaxCurve(float m, AnimationCurve c) { }
            public static implicit operator MinMaxCurve(float f) => new MinMaxCurve(f);
        }
        public struct MinMaxGradient
        {
            public MinMaxGradient(Color c) { }
            public MinMaxGradient(Color a, Color b) { }
            public MinMaxGradient(Gradient g) { }
            public static implicit operator MinMaxGradient(Color c) => new MinMaxGradient(c);
            public static implicit operator MinMaxGradient(Gradient g) => new MinMaxGradient(g);
        }
        public struct MainModule
        {
            public bool loop { get; set; }
            public bool playOnAwake { get; set; }
            public float duration { get; set; }
            public MinMaxCurve startLifetime { get; set; }
            public MinMaxCurve startSpeed { get; set; }
            public MinMaxCurve startSize { get; set; }
            public MinMaxGradient startColor { get; set; }
            public MinMaxCurve gravityModifier { get; set; }
            public int maxParticles { get; set; }
            public ParticleSystemSimulationSpace simulationSpace { get; set; }
        }
        public struct EmissionModule { public MinMaxCurve rateOverTime { get; set; } }
        public struct ShapeModule { public ParticleSystemShapeType shapeType { get; set; } public float radius { get; set; } public float angle { get; set; } public Vector3 rotation { get; set; } }
        public struct SizeOverLifetimeModule { public bool enabled { get; set; } public MinMaxCurve size { get; set; } }
        public struct ColorOverLifetimeModule { public bool enabled { get; set; } public MinMaxGradient color { get; set; } }
        public struct NoiseModule { public bool enabled { get; set; } public MinMaxCurve strength { get; set; } public float frequency { get; set; } }
        public struct CollisionModule { public bool enabled { get; set; } public ParticleSystemCollisionType type { get; set; } public MinMaxCurve bounce { get; set; } public MinMaxCurve lifetimeLoss { get; set; } public ParticleSystemCollisionQuality quality { get; set; } }
        public MainModule main => default;
        public EmissionModule emission => default;
        public ShapeModule shape => default;
        public SizeOverLifetimeModule sizeOverLifetime => default;
        public ColorOverLifetimeModule colorOverLifetime => default;
        public NoiseModule noise => default;
        public CollisionModule collision => default;
        public void Play() { }
        public void Stop(bool children, ParticleSystemStopBehavior b) { }
        public void Emit(int n) { }
        public void Clear() { }
    }
    public class AudioSource : Behaviour { public float pitch { get; set; } }
    public enum AudioReverbPreset { Off, Cave }
    public class AudioReverbFilter : Behaviour { public AudioReverbPreset reverbPreset { get; set; } }
    public class AudioHighPassFilter : Behaviour { public float cutoffFrequency { get; set; } }
    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform r, Vector2 p, Camera c, out Vector2 local) { local = default; return false; }
    }
    public enum KeyCode { Space, E, LeftShift, F, J }
    public static class Input
    {
        public static float GetAxisRaw(string a) => 0f;
        public static bool GetKey(KeyCode k) => false;
    }
    public class AndroidJavaObject : IDisposable
    {
        public T GetStatic<T>(string n) => default;
        public void CallStatic(string m, params object[] a) { }
        public void Dispose() { }
    }
    public class AndroidJavaClass : AndroidJavaObject { public AndroidJavaClass(string n) { } }

    [AttributeUsage(AttributeTargets.All)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.All)] public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.All)] public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.All)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class RequireComponent : Attribute
    {
        public RequireComponent(Type a) { }
        public RequireComponent(Type a, Type b) { }
        public RequireComponent(Type a, Type b, Type c) { }
    }
}
namespace UnityEngine.Android
{
    public class PermissionCallbacks { public event Action<string> PermissionGranted; public event Action<string> PermissionDenied; void T() { PermissionGranted?.Invoke(""); PermissionDenied?.Invoke(""); } }
    public struct Permission
    {
        public const string Microphone = "android.permission.RECORD_AUDIO";
        public static bool HasUserAuthorizedPermission(string p) => false;
        public static void RequestUserPermission(string p, PermissionCallbacks c) { }
    }
}
namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single, Additive }
    public struct Scene { public int buildIndex => 0; }
    public static class SceneManager { public static Scene GetActiveScene() => default; }
}
namespace UnityEngine.Rendering
{
    public class RenderPipelineAsset : Object { }
    public static class GraphicsSettings { public static RenderPipelineAsset currentRenderPipeline => null; }
    public abstract class RenderPipeline
    {
        public class StandardRequest { public RenderTexture destination; }
        public static bool SupportsRenderRequest<T>(Camera c, T data) => false;
        public static void SubmitRenderRequest<T>(Camera c, T data) { }
    }
    public struct AsyncGPUReadbackRequest
    {
        public bool hasError => false;
        public Unity.Collections.NativeArray<T> GetData<T>(int layer = 0) where T : struct => default;
    }
    public static class AsyncGPUReadback
    {
        public static AsyncGPUReadbackRequest Request(Texture src, int mip, TextureFormat f, Action<AsyncGPUReadbackRequest> cb) => default;
    }
}
namespace Unity.Collections
{
    public struct NativeArray<T> where T : struct
    {
        public int Length => 0;
        public void CopyTo(T[] array) { }
    }
}
namespace UnityEngine.EventSystems
{
    public class PointerEventData { public Vector2 position; public Camera pressEventCamera => null; }
    public interface IPointerDownHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData e); }
    public interface IDragHandler { void OnDrag(PointerEventData e); }
}
namespace UnityEngine.UI
{
    public class Graphic : Behaviour { public Color color { get; set; } }
    public class Image : Graphic { public float fillAmount { get; set; } }
    public class Text : Graphic { public string text { get; set; } }
    public class Button : Behaviour
    {
        public class ButtonClickedEvent { public void AddListener(Action a) { } }
        public ButtonClickedEvent onClick => new ButtonClickedEvent();
        public bool interactable { get; set; }
    }
}
namespace UnityEngine.InputSystem
{
    public class KeyControl { public bool isPressed => false; }
    public class Keyboard
    {
        public static Keyboard current => null;
        public KeyControl aKey, dKey, sKey, wKey, leftArrowKey, rightArrowKey, upArrowKey, downArrowKey, spaceKey, eKey, leftShiftKey, fKey, jKey;
    }
}
