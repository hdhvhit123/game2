using UnityEngine;

/// <summary>
/// Giọng nói của người chơi biến dạng khi rơi tự do (vang như trong hang, trầm xuống).
/// Gắn lên GameObject có AudioSource của Photon Voice Speaker trong prefab người chơi.
/// </summary>
[RequireComponent(typeof(AudioSource), typeof(AudioReverbFilter), typeof(AudioHighPassFilter))]
public class VoiceDopplerEffect : MonoBehaviour
{
    [SerializeField] private Rigidbody targetPlayerRb;
    [SerializeField] private float fallSpeedStart = 3f;
    [SerializeField] private float fallSpeedFull = 25f;
    [Tooltip("Giới hạn hạ cao độ: giọng stream qua mạng bị phát chậm quá nhiều sẽ tích trễ")]
    [SerializeField, Range(0.6f, 1f)] private float minPitch = 0.82f;
    [SerializeField] private float responsiveness = 6f;

    private AudioSource _voiceSource;
    private AudioReverbFilter _reverbFilter;
    private AudioHighPassFilter _highPassFilter;
    private Vector3 _lastPos;
    private float _fallFactor;
    private bool _reverbOn;

    private void Awake()
    {
        _voiceSource = GetComponent<AudioSource>();
        _reverbFilter = GetComponent<AudioReverbFilter>();
        _highPassFilter = GetComponent<AudioHighPassFilter>();
        // Người chơi được spawn lúc chạy nên không gán sẵn được trong Inspector.
        if (targetPlayerRb == null) targetPlayerRb = GetComponentInParent<Rigidbody>();
        _lastPos = transform.position;
        SetReverb(false);
    }

    private void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        // Trên client Rigidbody là kinematic (vận tốc = 0), nên ước lượng từ vị trí.
        Vector3 pos = targetPlayerRb != null ? targetPlayerRb.position : transform.position;
        float vy = (pos.y - _lastPos.y) / dt;
        _lastPos = pos;
        if (targetPlayerRb != null && !targetPlayerRb.isKinematic) vy = targetPlayerRb.linearVelocity.y;

        float target = Mathf.InverseLerp(fallSpeedStart, fallSpeedFull, -vy);
        _fallFactor = Mathf.Lerp(_fallFactor, target, 1f - Mathf.Exp(-responsiveness * dt));

        // Bản cũ gán reverbPreset mỗi frame: việc này nạp lại toàn bộ thông số reverb, gây rè tiếng.
        bool wantReverb = _fallFactor > 0.1f;
        if (wantReverb != _reverbOn) SetReverb(wantReverb);

        _voiceSource.pitch = Mathf.Lerp(1f, minPitch, _fallFactor);
        _highPassFilter.cutoffFrequency = Mathf.Lerp(80f, 950f, _fallFactor);
    }

    private void SetReverb(bool on)
    {
        _reverbOn = on;
        _reverbFilter.reverbPreset = on ? AudioReverbPreset.Cave : AudioReverbPreset.Off;
    }
}
