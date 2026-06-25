using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// 老式雪花电视机文字切换效果。
/// 挂在 TextMeshProUGUI 对象上。
/// 切换时文字先变成随机乱码，然后逐个字符还原成新内容。
///
/// 使用方式：
///   GetComponent<GlitchText>().Glitch("新文字", onComplete);
/// 或者只触发效果不改变文字：
///   GetComponent<GlitchText>().GlitchInPlace();
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class GlitchText : MonoBehaviour
{
    [Header("效果参数")]
    [Tooltip("每个字符随机变化的持续时间")]
    [SerializeField] private float glitchDuration = 0.3f;

    [Tooltip("字符变化间隔（秒）。越大变化越慢，0表示每帧变化")]
    [SerializeField] private float glitchInterval = 0.05f;

    [Tooltip("字符还原速度（每秒还原几个字符）")]
    [SerializeField] private float resolveSpeed = 12f;

    [Tooltip("乱码字符集")]
    [SerializeField] private string glitchChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";

    private TextMeshProUGUI _tmp;
    private Coroutine _currentCoroutine;
    private string _targetText;
    private string _originalText; // GlitchHold 前保存的原始文字

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// 触发雪花效果并切换到新文字。
    /// </summary>
    public void Glitch(string newText, System.Action onComplete = null)
    {
        if (_currentCoroutine != null)
            StopCoroutine(_currentCoroutine);
        _targetText = newText;
        _currentCoroutine = StartCoroutine(GlitchRoutine(newText, onComplete));
    }

    /// <summary>
    /// 在不改变文字内容的情况下触发雪花效果（颜色闪烁）。
    /// </summary>
    public void GlitchInPlace()
    {
        if (_currentCoroutine != null)
            StopCoroutine(_currentCoroutine);
        _currentCoroutine = StartCoroutine(GlitchRoutine(_tmp.text, null));
    }

    /// <summary>
    /// 持续乱码：文字变成随机乱码后一直保持，不还原。
    /// 调用 Release() 或 Glitch(原文字) 来恢复。
    /// </summary>
    public void GlitchHold()
    {
        if (_currentCoroutine != null)
            StopCoroutine(_currentCoroutine);
        _currentCoroutine = StartCoroutine(GlitchHoldRoutine());
    }

    /// <summary>
    /// 停止持续乱码，还原成指定文字（不填则还原成 GlitchHold 前的原始文字）。
    /// </summary>
    public void Release(string restoreText = null)
    {
        Glitch(restoreText ?? _originalText ?? _tmp.text);
    }

    private IEnumerator GlitchHoldRoutine()
    {
        string original = _tmp.text;
        _originalText = original; // 保存原始文字供 Release 使用
        int maxLen = original.Length;
        float intervalTimer = 0f;

        // 一直保持乱码，不还原
        while (true)
        {
            intervalTimer += Time.deltaTime;
            if (intervalTimer >= glitchInterval)
            {
                intervalTimer = 0f;
                string glitched = "";
                for (int i = 0; i < maxLen; i++)
                {
                    if (Random.value > 0.3f)
                        glitched += glitchChars[Random.Range(0, glitchChars.Length)];
                    else if (i < original.Length)
                        glitched += original[i];
                    else
                        glitched += " ";
                }
                _tmp.text = glitched;
            }
            yield return null;
        }
    }

    private IEnumerator GlitchRoutine(string newText, System.Action onComplete)
    {
        string original = _tmp.text;
        int maxLen = Mathf.Max(original.Length, newText.Length);
        float elapsed = 0f;
        float intervalTimer = 0f;
        string currentDisplay = original;

        // 阶段1：随机乱码
        while (elapsed < glitchDuration)
        {
            elapsed      += Time.deltaTime;
            intervalTimer += Time.deltaTime;

            // 只在间隔到达时才更新字符
            if (intervalTimer >= glitchInterval)
            {
                intervalTimer = 0f;
                string glitched = "";
                for (int i = 0; i < maxLen; i++)
                {
                    if (Random.value > 0.3f)
                        glitched += glitchChars[Random.Range(0, glitchChars.Length)];
                    else if (i < original.Length)
                        glitched += original[i];
                    else
                        glitched += " ";
                }
                currentDisplay = glitched;
            }
            _tmp.text = currentDisplay;
            yield return null;
        }

        // 阶段2：逐个字符还原成新文字
        char[] current = new char[maxLen];
        for (int i = 0; i < maxLen; i++)
            current[i] = i < newText.Length ? glitchChars[Random.Range(0, glitchChars.Length)] : ' ';

        int resolved = 0;
        intervalTimer = 0f;

        while (resolved < newText.Length)
        {
            intervalTimer += Time.deltaTime;

            if (intervalTimer >= glitchInterval)
            {
                intervalTimer = 0f;
                int toResolve = Mathf.Max(1, Mathf.FloorToInt(resolveSpeed * glitchInterval));
                for (int i = 0; i < toResolve && resolved < newText.Length; i++)
                {
                    current[resolved] = newText[resolved];
                    resolved++;
                }

                string display = "";
                for (int i = 0; i < newText.Length; i++)
                {
                    if (i < resolved)
                        display += current[i];
                    else
                        display += glitchChars[Random.Range(0, glitchChars.Length)];
                }
                _tmp.text = display;
            }
            yield return null;
        }

        _tmp.text = newText;
        _currentCoroutine = null;
        onComplete?.Invoke();
    }
}