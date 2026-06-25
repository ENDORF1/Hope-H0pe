using UnityEngine;
using System.Collections;
using DG.Tweening;

/// <summary>
/// 角色死亡时的镜头演出：
///   1. 镜头移向死亡角色的肖像并放大
///   2. 肖像翻面（ShowBack）
///   3. 演出结束后镜头复原
/// 挂在场景任意常驻对象上，由 GameManager 调用 PlayDeathEffect()。
/// </summary>
public class DeathCameraEffect : MonoBehaviour
{
    [Header("镜头引用")]
    [SerializeField] private Camera mainCamera;

    [Header("玩家肖像")]
    [SerializeField] private Transform playerPortrait;   // 玩家肖像 Transform
    [SerializeField] private Transform aiPortrait;       // AI 肖像 Transform

    [Header("镜头参数")]
    [SerializeField] private float zoomInSize     = 3f;    // 放大后的 orthographic size
    [SerializeField] private float zoomInDuration = 0.6f;
    [SerializeField] private float holdDuration   = 1.2f;  // 放大后停留时间
    [SerializeField] private float zoomOutDuration= 0.5f;

    [Header("翻面延迟（镜头移动完成后）")]
    [SerializeField] private float flipDelay = 0.2f;

    // 记录初始镜头状态用于复原
    private Vector3 _originCamPos;
    private float   _originSize;

    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    // ─────────────────────────────────────────────────
    /// <summary>
    /// 播放死亡演出协程。
    /// isPlayer = true → 玩家死亡；false → AI 死亡
    /// </summary>
    public IEnumerator PlayDeathEffect(bool playerDead, bool aiDead)
    {
        bool bothDead = playerDead && aiDead;

        if (!bothDead)
        {
            // ── 单方死亡：镜头移动 + 放大 ──────────────
            Transform target = playerDead ? playerPortrait : aiPortrait;
            if (target == null || mainCamera == null) yield break;

            _originCamPos = mainCamera.transform.position;
            _originSize   = mainCamera.orthographicSize;

            Vector3 targetPos = new Vector3(
                target.position.x,
                target.position.y,
                mainCamera.transform.position.z
            );

            Sequence moveIn = DOTween.Sequence();
            moveIn.Join(mainCamera.transform.DOMove(targetPos, zoomInDuration).SetEase(Ease.InOutQuart));
            moveIn.Join(DOTween.To(
                () => mainCamera.orthographicSize,
                v  => mainCamera.orthographicSize = v,
                zoomInSize, zoomInDuration
            ).SetEase(Ease.InOutQuart));

            yield return moveIn.WaitForCompletion();
            yield return new WaitForSeconds(flipDelay);
        }

        // ── 翻面（双方死亡则同时翻，单方只翻死者）──────
        if (playerDead) FlipPortrait(playerPortrait);
        if (aiDead)     FlipPortrait(aiPortrait);

        // 等翻面动画完成
        yield return new WaitForSeconds(0.5f);

        if (!bothDead)
        {
            // ── 停留后镜头复原 ────────────────────────
            yield return new WaitForSeconds(holdDuration);

            Sequence moveOut = DOTween.Sequence();
            moveOut.Join(mainCamera.transform.DOMove(_originCamPos, zoomOutDuration).SetEase(Ease.InOutQuart));
            moveOut.Join(DOTween.To(
                () => mainCamera.orthographicSize,
                v  => mainCamera.orthographicSize = v,
                _originSize, zoomOutDuration
            ).SetEase(Ease.InOutQuart));

            yield return moveOut.WaitForCompletion();
        }
        else
        {
            yield return new WaitForSeconds(holdDuration);
        }
    }

    private void FlipPortrait(Transform portrait)
    {
        if (portrait == null) return;
        BetterCardRotation rot = portrait.GetComponentInChildren<BetterCardRotation>(true);
        if (rot != null) rot.FlipWithAnimation(false); // false = 翻到卡背
    }
}