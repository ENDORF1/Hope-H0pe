using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 激光特效：从激光模块射向目标，纯UI实现。
/// 挂在场景Canvas下的一个空GameObject上，Inspector赋值。
/// CombatEngine.ResolveLaserFlip 调用 LaserEffect.Instance.Play(...)
/// </summary>
public class LaserEffect : MonoBehaviour
{
    public static LaserEffect Instance { get; private set; }

    [Header("激光图像")]
    [Tooltip("一张横向的激光光束 Sprite，Pivot 设为左中（0, 0.5）")]
    [SerializeField] private Image laserBeam;

    [Tooltip("激光撞击点光晕 Image，Pivot 居中")]
    [SerializeField] private Image impactGlow;

    [Header("参数")]
    [SerializeField] private float beamDuration   = 0.08f;  // 光束持续时间
    [SerializeField] private float fadeDuration   = 0.12f;  // 淡出时间
    [SerializeField] private float impactDuration = 0.18f;  // 撞击光晕持续时间
    [SerializeField] private Color beamColor      = new Color(0.2f, 1f, 0.4f, 1f);  // 激光颜色
    [SerializeField] private Color impactColor    = new Color(0.2f, 1f, 0.4f, 1f);

    [Header("屏幕震动")]
    [SerializeField] private float shakeStrength  = 12f;
    [SerializeField] private float shakeDuration  = 0.18f;
    [SerializeField] private int   shakeVibrato   = 10;

    [Header("目标闪烁")]
    [SerializeField] private float flashDuration  = 0.08f;
    [SerializeField] private int   flashCount     = 2;
    [SerializeField] private Color flashColor     = Color.white;

    // 震动用的相机目标（若为空则找 Camera.main）
    private Transform _cameraTransform;

    void Awake()
    {
        Instance = this;
        if (laserBeam  != null) laserBeam.gameObject.SetActive(false);
        if (impactGlow != null) impactGlow.gameObject.SetActive(false);
        _cameraTransform = Camera.main?.transform;
    }

    [Header("随机光束（无目标时）")]
    [SerializeField] private float randomBeamLength = 8f;   // 随机光束长度（世界单位）
    [SerializeField] private float randomBeamWidth  = 0.8f; // 光束粗细
    [SerializeField] private float randomAngleMin   = 20f;  // 最小偏转角度
    [SerializeField] private float randomAngleMax   = 60f;  // 最大偏转角度

    /// <summary>
    /// 播放激光特效。由 CombatEngine 以 yield return StartCoroutine 调用。
    /// to 不为 null 时射向目标；to 为 null 时改为随机方向巨大光束（无冲击波）。
    /// </summary>
    public IEnumerator Play(Transform from, Transform to, Vector3 toWorld = default)
    {
        if (laserBeam == null) yield break;

        Vector3 fromPos = from.position;

        laserBeam.DOKill();
        laserBeam.transform.DOKill();

        if (to != null)
        {
            // ── 有目标：定向光束 + 冲击波 ────────────
            Vector3 toPos = to.position;
            PositionBeam(fromPos, toPos);

            if (impactGlow != null)
            {
                impactGlow.DOKill();
                impactGlow.transform.DOKill();
                impactGlow.transform.position   = toPos;
                impactGlow.transform.localScale = Vector3.one * 0.3f;
                impactGlow.gameObject.SetActive(true);
                impactGlow.color = new Color(impactColor.r, impactColor.g, impactColor.b, 0f);
                impactGlow.DOFade(1f, beamDuration * 0.3f);
                impactGlow.transform.DOScale(1.5f, impactDuration).SetEase(Ease.OutBack);
            }

            laserBeam.gameObject.SetActive(true);
            laserBeam.color = new Color(beamColor.r, beamColor.g, beamColor.b, 0f);
            laserBeam.DOFade(1f, beamDuration * 0.3f);

            yield return new WaitForSeconds(beamDuration);

            StartCoroutine(FlashTarget(to));

            if (impactGlow != null)
            {
                impactGlow.DOFade(0f, fadeDuration * 1.5f).OnComplete(() =>
                {
                    impactGlow.gameObject.SetActive(false);
                    impactGlow.transform.DOKill();
                });
                impactGlow.transform.DOScale(2.5f, fadeDuration * 1.5f).SetEase(Ease.OutQuad);
            }
        }
        else
        {
            // ── 无目标：随机方向巨大扫射光束，无冲击波 ──
            // 以"敌方方向"为基准左右随机偏转，Y 坐标判断敌方在上还是下
            float baseAngle = fromPos.y > 0f ? 270f : 90f;
            float offset    = Random.Range(randomAngleMin, randomAngleMax)
                              * (Random.value > 0.5f ? 1f : -1f);
            float angle     = baseAngle + offset;

            RectTransform rt = laserBeam.rectTransform;
            rt.position      = fromPos;
            rt.sizeDelta     = new Vector2(randomBeamLength, randomBeamWidth);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);

            laserBeam.gameObject.SetActive(true);
            laserBeam.color = new Color(beamColor.r, beamColor.g, beamColor.b, 0f);
            laserBeam.DOFade(1f, beamDuration * 0.2f);

            yield return new WaitForSeconds(beamDuration * 1.5f);
        }

        // ── 淡出光束 ──────────────────────────────────
        laserBeam.DOFade(0f, fadeDuration).OnComplete(() =>
            laserBeam.gameObject.SetActive(false));

        yield return new WaitForSeconds(fadeDuration);
    }

    // ── 计算光束位置、旋转、宽度（World Space Canvas）──
    private void PositionBeam(Vector3 from, Vector3 to)
    {
        RectTransform rt = laserBeam.rectTransform;

        Vector3 dir      = to - from;
        float   distance = dir.magnitude;
        float   angle    = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // World Space Canvas 下直接用世界坐标
        rt.position      = from;
        rt.sizeDelta     = new Vector2(distance, rt.sizeDelta.y);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // ── 目标闪烁协程 ─────────────────────────────────
    private IEnumerator FlashTarget(Transform target)
    {
        // 找目标身上所有 Graphic
        var graphics = target.GetComponentsInChildren<Graphic>();
        Color[] origColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
            origColors[i] = graphics[i].color;

        for (int f = 0; f < flashCount; f++)
        {
            foreach (var g in graphics) g.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].color = origColors[i];
            yield return new WaitForSeconds(flashDuration);
        }
    }
}