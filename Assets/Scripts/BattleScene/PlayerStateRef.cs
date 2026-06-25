using UnityEngine;

/// <summary>
/// 挂在肖像 GameObject（碰撞体对象）上，让 QuickPlayExecutor 能从 target 直接拿到对应的 PlayerState。
/// 只需在 Inspector 里把对应的 PlayerState 拖入即可。
/// </summary>
public class PlayerStateRef : MonoBehaviour
{
    [Tooltip("该肖像对应的 PlayerState（玩家或 AI）")]
    public PlayerState State;
}
