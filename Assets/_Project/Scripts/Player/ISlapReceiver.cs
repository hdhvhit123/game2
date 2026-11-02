using UnityEngine;

/// <summary>Vật thể phản ứng riêng khi bị tát (người chơi bị choáng, nồi lẩu sánh nước...).</summary>
public interface ISlapReceiver
{
    /// <summary>Chỉ được gọi trên máy có State Authority (host).</summary>
    void ReceiveSlap(Vector3 impulse, SemiActiveRagdollController slapper);
}
