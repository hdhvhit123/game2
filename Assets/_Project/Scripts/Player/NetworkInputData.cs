using Fusion;
using UnityEngine;

public enum InputButtons
{
    Jump = 0,
    Grab = 1,
    Slap = 2
}

public struct NetworkInputData : INetworkInput
{
    /// <summary>Hướng di chuyển trong không gian thế giới (x = X, y = Z), độ dài tối đa 1.</summary>
    public Vector2 moveDirection;
    public NetworkButtons buttons;
}
