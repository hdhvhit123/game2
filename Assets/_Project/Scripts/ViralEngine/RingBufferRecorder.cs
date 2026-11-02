using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Liên tục giữ vài giây hình ảnh gần nhất. Khi nồi lẩu vỡ, chờ thêm một nhịp để bắt được
/// "hậu quả", rồi ghép 6 khung thành ảnh truyện tranh 3x2 để chia sẻ.
///
/// Sửa so với bản cũ:
///  - Bộ nhớ: bản cũ giữ 160 khung 540x960 thô (~250MB RAM) và cấp phát ~31MB rác/giây
///    -> dễ bị Android kill. Bản mới cấp phát trước ~28MB, không tạo rác mỗi frame.
///  - Không còn chiếm camera chính: tạo camera ghi hình riêng, tắt sẵn và chỉ render khi cần
///    (bản cũ gán targetTexture vào camera được truyền vào; nếu là camera chính thì màn hình đen,
///    nếu là camera phụ đang bật thì cảnh bị render 2 lần mỗi frame).
///  - Giới hạn số yêu cầu đọc GPU đang chờ, kiểm tra thiết bị có hỗ trợ AsyncGPUReadback.
///  - Không bật bảng chia sẻ giữa lúc đang chơi: lưu ảnh rồi báo sự kiện, UI hiện nút "Khoe pha này".
///  - Giải phóng RenderTexture khi huỷ.
/// Muốn xuất video MP4 thật cần plugin mã hoá native (ví dụ NatCorder); FrameRing đã sẵn dữ liệu.
/// </summary>
public class RingBufferRecorder : MonoBehaviour
{
    public static event Action<string> OnHighlightReady;

    [SerializeField, Tooltip("Để trống = tự sao chép thiết lập từ Camera.main")] private Camera sourceCamera;
    [SerializeField] private int width = 270;
    [SerializeField] private int height = 480;
    [SerializeField] private int captureFps = 12;
    [SerializeField] private float bufferSeconds = 6f;
    [SerializeField] private float aftermathDelay = 0.6f;
    [SerializeField] private int collageColumns = 3;
    [SerializeField] private int collageRows = 2;
    [SerializeField] private int collageBorder = 8;
    [SerializeField, Range(30, 100)] private int jpgQuality = 85;
    [SerializeField] private bool autoShare = false;
    [SerializeField] private int maxPendingReadbacks = 3;

    public string LastHighlightPath { get; private set; }

    private Camera _recordCam;
    private RenderTexture _rt;
    private FrameRing _ring;
    private float _timer;
    private int _pendingReadbacks;
    private bool _isSaving;
    private bool _destroyed;
    private bool _supported;

    private void OnEnable()
    {
        FragileCarryObject.OnObjectShatteredEvent += TriggerHighlightSave;
    }

    private void OnDisable()
    {
        FragileCarryObject.OnObjectShatteredEvent -= TriggerHighlightSave;
    }

    private void Start()
    {
        _supported = SystemInfo.supportsAsyncGPUReadback;
        if (!_supported)
        {
            Debug.LogWarning("Thiết bị không hỗ trợ AsyncGPUReadback: tắt tính năng quay highlight.");
            enabled = false;
            return;
        }

        _rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { name = "HighlightRT" };
        _rt.Create();

        int capacity = Mathf.Max(collageColumns * collageRows, Mathf.RoundToInt(captureFps * bufferSeconds));
        _ring = new FrameRing(capacity, width * height * 3);
    }

    private void OnDestroy()
    {
        _destroyed = true;
        if (_recordCam != null) Destroy(_recordCam.gameObject);
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }
    }

    private void LateUpdate()
    {
        if (_isSaving || _ring == null) return;

        _timer += Time.unscaledDeltaTime;
        float interval = 1f / Mathf.Max(1, captureFps);
        if (_timer < interval) return;
        _timer = Mathf.Min(_timer - interval, interval); // giữ phần dư, không dồn khung

        if (_pendingReadbacks >= maxPendingReadbacks) return; // GPU chậm: bỏ khung thay vì xếp hàng
        if (!EnsureRecordCamera()) return;

        RenderRecordCamera();
        _pendingReadbacks++;
        AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGB24, OnCompleteReadback);
    }

    private bool EnsureRecordCamera()
    {
        Camera src = sourceCamera != null ? sourceCamera : Camera.main;
        if (src == null) return false;

        if (_recordCam == null)
        {
            var go = new GameObject("HighlightRecordCamera");
            _recordCam = go.AddComponent<Camera>();
            _recordCam.enabled = false; // chỉ render thủ công
        }

        // Theo camera chính (vị trí, FOV, layer...) nhưng xuất ra RenderTexture riêng.
        _recordCam.CopyFrom(src);
        _recordCam.enabled = false;
        _recordCam.targetTexture = _rt;
        return true;
    }

    private void RenderRecordCamera()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            var request = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(_recordCam, request))
            {
                request.destination = _rt;
                RenderPipeline.SubmitRenderRequest(_recordCam, request);
                return;
            }
        }
        _recordCam.Render();
    }

    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        _pendingReadbacks = Mathf.Max(0, _pendingReadbacks - 1);
        if (_destroyed || request.hasError || _isSaving || _ring == null) return;

        var data = request.GetData<byte>();
        if (data.Length != _ring.FrameBytes) return;

        data.CopyTo(_ring.AcquireWriteSlot());
        _ring.CommitWrite();
    }

    public void TriggerHighlightSave()
    {
        if (_isSaving || _ring == null || _ring.Count == 0 || !isActiveAndEnabled) return;
        StartCoroutine(SaveHighlightRoutine());
    }

    private IEnumerator SaveHighlightRoutine()
    {
        // Tiếp tục ghi thêm một chút để có khung "nồi lẩu bay tung toé".
        yield return new WaitForSecondsRealtime(aftermathDelay);
        _isSaving = true;

        int panels = collageColumns * collageRows;
        int[] ages = _ring.SampleAgesOldestFirst(panels);
        var frames = new byte[ages.Length][];
        for (int i = 0; i < ages.Length; i++) frames[i] = _ring.GetByAge(ages[i]);

        int outW = CollageBuilder.OutputWidth(width, collageColumns, collageBorder);
        int outH = CollageBuilder.OutputHeight(height, collageRows, collageBorder);
        var collage = new byte[outW * outH * 3];

        // Ghép ảnh trên luồng phụ để không giật khung hình.
        Task composeTask = Task.Run(() =>
            CollageBuilder.Compose(frames, width, height, collageColumns, collageRows, collageBorder, collage));
        while (!composeTask.IsCompleted) yield return null;

        if (composeTask.IsFaulted || _destroyed)
        {
            if (composeTask.Exception != null) Debug.LogException(composeTask.Exception);
            _isSaving = false;
            yield break;
        }

        var tex = new Texture2D(outW, outH, TextureFormat.RGB24, false);
        tex.LoadRawTextureData(collage);
        tex.Apply(false, false);
        byte[] jpg = tex.EncodeToJPG(jpgQuality);
        Destroy(tex);

        // Mở khoá ghi hình ngay để không mất khung trong lúc đợi ghi file.
        _isSaving = false;

        string path = Path.Combine(Application.temporaryCachePath, $"KhoanhKhacCayCu_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
        Task writeTask = File.WriteAllBytesAsync(path, jpg);
        while (!writeTask.IsCompleted) yield return null;

        if (writeTask.IsFaulted)
        {
            Debug.LogException(writeTask.Exception);
            yield break;
        }

        DeleteOldHighlight(LastHighlightPath);
        LastHighlightPath = path;
        OnHighlightReady?.Invoke(path);
        if (autoShare) ShareLastHighlight();
    }

    /// <summary>Gắn vào nút "Khoe pha này" trên HUD.</summary>
    public void ShareLastHighlight()
    {
        if (string.IsNullOrEmpty(LastHighlightPath) || !File.Exists(LastHighlightPath)) return;

        NetworkRunnerHandler net = NetworkRunnerHandler.Instance;
        string link = net != null && !string.IsNullOrEmpty(net.CurrentRoomCode) ? $" Vào gánh lại pha này coi: {net.BuildInviteUrl()}" : string.Empty;

        new NativeShare()
            .AddFile(LastHighlightPath)
            .SetSubject("Nồi lẩu toang rồi!")
            .SetText("Tại mày hết đó!" + link)
            .Share();
    }

    private static void DeleteOldHighlight(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception) { /* file đang được app khác dùng: bỏ qua */ }
    }
}
