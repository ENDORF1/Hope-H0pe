using UnityEngine;
using DG.Tweening;

/// <summary>
/// 退场动画资产。
/// 在 Project 窗口右键 → Create → Animation → Exit Animation 创建。
/// 每个角色可引用不同的动画资产，实现不同的退场效果。
/// </summary>
[CreateAssetMenu(fileName = "ExitAnim_Standard", menuName = "Animation/Exit Animation")]
public class ExitAnimationAsset : ScriptableObject
{
    /// <summary>退场方向：飞升(↑) 或 坠落(↓)</summary>
    public enum Direction { Ascend, Sink }

    [Header("方向")]
    [Tooltip("Ascend = 向上飞出（Hope），Sink = 向下坠落（Void）")]
    public Direction direction = Direction.Ascend;

    [Header("蓄力阶段")]
    [Tooltip("蓄力时长（秒）")]
    public float dipDuration = 0.22f;
    [Tooltip("蓄力幅度（px）")]
    public float dipAmount = 28f;
    [Tooltip("蓄力时的缓动曲线")]
    public Ease dipEase = Ease.InBack;
    [Tooltip("蓄力时的缩放目标")]
    public float dipScale = 0.94f;

    [Header("飞升/坠落阶段")]
    [Tooltip("飞行时长（秒）")]
    public float flyDuration = 0.75f;
    [Tooltip("飞行距离（px），0 = 自动计算飞出画面")]
    public float flyDistance;
    [Tooltip("飞行缓动曲线")]
    public Ease flyEase = Ease.OutQuart;

    [Header("淡出")]
    [Tooltip("淡出延迟比例（相对于飞行时长）")]
    [Range(0f, 1f)]
    public float fadeDelayRatio = 0.35f;
    [Tooltip("淡出时长比例（相对于飞行时长）")]
    [Range(0f, 1f)]
    public float fadeDurationRatio = 0.6f;
}
