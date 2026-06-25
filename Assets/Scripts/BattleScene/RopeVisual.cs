using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 绳子视觉效果。
/// 挂在 Slider 对象上，由 GameManager 调用 StartRope / StopRope。
/// Slider value 从 1→0 表示时间流逝。
/// 快烧完时（低于 warningThreshold）变红并闪烁。
/// </summary>
public class RopeVisual : MonoBehaviour
{
    [Header("Slider 引用（留空则自动获取）")]
    [SerializeField] private Slider slider;

    [Header("填充颜色")]
    [SerializeField] private Color normalColor  = new Color(0.9f, 0.75f, 0.2f); // 金黄
    [SerializeField] private Color warningColor = new Color(0.95f, 0.2f, 0.1f); // 红
    [SerializeField] private float warningThreshold = 0.3f; // 低于此比例触发警告

    [Header("警告闪烁")]
    [SerializeField] private float flashSpeed    = 4f;   // 闪烁频率
    [SerializeField] private float flashMinAlpha = 0.4f;

    [Header("计时器文字（留空则自动查找名为 '00:00' 的 TMP）")]
    [SerializeField] private TextMeshProUGUI timerText;

    private Image   _fillImage;
    private bool    _running    = false;
    private float   _duration   = 30f;
    private float   _elapsed    = 0f;
    private bool    _inWarning  = false;
    private Tweener _flashTween;

    // ─────────────────────────────────────────────────
    void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        // 找到 Fill 的 Image 组件
        if (slider != null && slider.fillRect != null)
            _fillImage = slider.fillRect.GetComponent<Image>();

        if (_fillImage != null)
            _fillImage.color = normalColor;

        // 自动查找父级里的 TMP（找名字含冒号的，即 00:00）
        if (timerText == null)
        {
            TextMeshProUGUI[] tmps = GetComponentsInParent<TextMeshProUGUI>(true);
            foreach (var t in tmps)
                if (t.text.Contains(":")) { timerText = t; break; }
        }

        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_running) return;

        _elapsed += Time.deltaTime;
        float ratio = 1f - Mathf.Clamp01(_elapsed / _duration);

        if (slider != null)
            slider.value = ratio;

        // 进入警告区
        if (!_inWarning && ratio <= warningThreshold)
            EnterWarning();

        if (_inWarning && ratio > warningThreshold)
            ExitWarning();

        UpdateTimerText(_duration - _elapsed);
    }

    // ─────────────────────────────────────────────────
    // 公开接口（由 GameManager 调用）
    // ─────────────────────────────────────────────────

    /// <summary>开始倒计时，duration 为窗口总秒数</summary>
    public void StartRope(float duration)
    {
        _duration = duration;
        _elapsed  = 0f;
        _running  = true;
        _inWarning = false;

        if (slider != null) slider.value = 1f;
        if (_fillImage != null) _fillImage.color = normalColor;

        UpdateTimerText(_duration);
        gameObject.SetActive(true);
    }

    /// <summary>停止并隐藏绳子</summary>
    public void StopRope()
    {
        _running   = false;
        _inWarning = false;

        _flashTween?.Kill();
        if (_fillImage != null)
        {
            Color c = _fillImage.color;
            c.a = 1f;
            _fillImage.color = c;
        }

        gameObject.SetActive(false);
    }

    /// <summary>外部同步进度（由 GameManager 直接驱动时用，不用内部 Update）</summary>
    public void SetProgress(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        if (slider != null) slider.value = ratio;

        if (!_inWarning && ratio <= warningThreshold)
            EnterWarning();
        if (_inWarning && ratio > warningThreshold)
            ExitWarning();
    }

    // ─────────────────────────────────────────────────
    private void EnterWarning()
    {
        _inWarning = true;
        if (_fillImage == null) return;

        _fillImage.color = warningColor;

        _flashTween?.Kill();
        _flashTween = DOTween.To(
            () => _fillImage.color.a,
            a =>
            {
                Color c = _fillImage.color;
                c.a = a;
                _fillImage.color = c;
            },
            flashMinAlpha,
            1f / flashSpeed
        ).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    private void ExitWarning()
    {
        _inWarning = false;
        _flashTween?.Kill();

        if (_fillImage != null)
        {
            _fillImage.color = normalColor;
            Color c = _fillImage.color;
            c.a = 1f;
            _fillImage.color = c;
        }
    }

    private void UpdateTimerText(float secondsLeft)
    {
        if (timerText == null) return;
        secondsLeft = Mathf.Max(0f, secondsLeft);
        int mins = Mathf.FloorToInt(secondsLeft / 60f);
        int secs = Mathf.FloorToInt(secondsLeft % 60f);
        timerText.text = $"{mins:00}:{secs:00}";
    }
}