using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 统一对话管理器。
/// 支持三种触发时机：
///   1. 进场对话      → 调用 PlayEntryDialogues()
///   2. 角色死亡对话  → 调用 PlayDeathDialogue(playerDead, aiDead)
///   3. 打牌对话      → 调用 PlayCardDialogue(CardAsset)
///
/// 进场对话和死亡对话在 Inspector 里自由配置（List<CardDialogue>）。
/// 打牌对话从 CardAsset.OnPlayDialogues 读取。
/// </summary>
public class DialogueManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // 气泡 UI 引用
    // ─────────────────────────────────────────────────

    [Header("玩家气泡")]
    [SerializeField] private RectTransform playerBubbleRoot;
    [SerializeField] private TextMeshProUGUI playerBubbleText;
    [SerializeField] private Image playerBubbleImage;

    [Header("AI 气泡")]
    [SerializeField] private RectTransform aiBubbleRoot;
    [SerializeField] private TextMeshProUGUI aiBubbleText;
    [SerializeField] private Image aiBubbleImage;

    // ─────────────────────────────────────────────────
    // 对话内容配置
    // ─────────────────────────────────────────────────

    [Header("进场对话（按顺序逐条播放）")]
    [SerializeField] private List<CardDialogue> entryDialogues = new List<CardDialogue>();

    [Header("玩家死亡时对话（随机取一条）")]
    [SerializeField] private List<CardDialogue> playerDeathDialogues = new List<CardDialogue>();

    [Header("AI 死亡时对话（随机取一条）")]
    [SerializeField] private List<CardDialogue> aiDeathDialogues = new List<CardDialogue>();

    // ─────────────────────────────────────────────────
    // 动画参数
    // ─────────────────────────────────────────────────

    [Header("动画参数")]
    [SerializeField] private float displayDuration       = 2.5f;
    [SerializeField] private float fadeDuration          = 0.3f;
    [SerializeField] private float floatAmplitude        = 6f;
    [SerializeField] private float floatSpeed            = 1.2f;
    [SerializeField] private float entryDialogueInterval = 0.8f;

    // ─────────────────────────────────────────────────
    // 内部状态
    // ─────────────────────────────────────────────────

    private Coroutine _playerFloatCoroutine;
    private Coroutine _aiFloatCoroutine;
    private Coroutine _playerShowCoroutine;
    private Coroutine _aiShowCoroutine;

    private Vector2 _playerImageOrigin, _playerTextOrigin;
    private Vector2 _aiImageOrigin,     _aiTextOrigin;
    private bool    _originsRecorded;

    // ─────────────────────────────────────────────────
    // 公开接口
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 进场对话：按顺序逐条播放。
    /// yield return 可等待所有条目播完。
    /// </summary>
    public IEnumerator PlayEntryDialogues()
    {
        foreach (var dialogue in entryDialogues)
        {
            PlayDialogueLine(dialogue);
            yield return new WaitForSeconds(displayDuration + fadeDuration * 2f + entryDialogueInterval);
        }
    }

    /// <summary>
    /// 角色死亡对话。双方同时死亡时不触发。
    /// </summary>
    public void PlayDeathDialogue(bool playerDead, bool aiDead)
    {
        if (playerDead && aiDead) return;

        List<CardDialogue> pool = playerDead ? playerDeathDialogues : aiDeathDialogues;
        if (pool == null || pool.Count == 0) return;

        PlayDialogueLine(pool[Random.Range(0, pool.Count)]);
    }

    /// <summary>
    /// 立即平滑淡出所有正在显示的气泡，停止所有协程。
    /// 在结局 UI 出现前调用。
    /// </summary>
    public IEnumerator FadeOutAll()
    {
        // 停止显示协程（防止协程内部再次操作 alpha/位置）
        if (_playerShowCoroutine != null) { StopCoroutine(_playerShowCoroutine); _playerShowCoroutine = null; }
        if (_aiShowCoroutine     != null) { StopCoroutine(_aiShowCoroutine);     _aiShowCoroutine     = null; }

        // 淡出时保持浮动继续，透明后再停止归位
        var seq = DOTween.Sequence();
        if (playerBubbleText  != null) seq.Join(playerBubbleText.DOFade(0f,  fadeDuration));
        if (playerBubbleImage != null) seq.Join(playerBubbleImage.DOFade(0f, fadeDuration));
        if (aiBubbleText      != null) seq.Join(aiBubbleText.DOFade(0f,      fadeDuration));
        if (aiBubbleImage     != null) seq.Join(aiBubbleImage.DOFade(0f,     fadeDuration));
        yield return seq.WaitForCompletion();

        // 完全透明后才停止浮动并归位，视觉上无跳变
        StopFloat(true);
        StopFloat(false);
        RestorePositions(true);
        RestorePositions(false);
    }

    /// <summary>
    /// 打牌对话：从 CardAsset.OnPlayDialogues 随机取一条。
    /// </summary>
    public void PlayCardDialogue(CardAsset card)
    {
        if (card == null || card.OnPlayDialogues == null || card.OnPlayDialogues.Count == 0)
            return;

        var dialogue = card.OnPlayDialogues[Random.Range(0, card.OnPlayDialogues.Count)];
        Debug.Log($"[DialogueManager] PlayCardDialogue: speaker={dialogue.Speaker}, line={dialogue.Line}, playerBubbleText={playerBubbleText}, aiBubbleText={aiBubbleText}");
        PlayDialogueLine(dialogue);
    }

    // ─────────────────────────────────────────────────
    // 核心：播放单条对话
    // ─────────────────────────────────────────────────

    private void PlayDialogueLine(CardDialogue dialogue)
    {
        if (dialogue == null) return;

        switch (dialogue.Speaker)
        {
            case DialogueSpeaker.Player:
                if (_playerShowCoroutine != null) StopCoroutine(_playerShowCoroutine);
                _playerShowCoroutine = StartCoroutine(
                    ShowBubble(playerBubbleText, playerBubbleImage, dialogue.Line, isPlayer: true));
                break;

            case DialogueSpeaker.AI:
                if (_aiShowCoroutine != null) StopCoroutine(_aiShowCoroutine);
                _aiShowCoroutine = StartCoroutine(
                    ShowBubble(aiBubbleText, aiBubbleImage, dialogue.Line, isPlayer: false));
                break;

            case DialogueSpeaker.Narrator:
                // 旁白预留
                break;
        }
    }

    // ─────────────────────────────────────────────────
    // 气泡显示协程
    // ─────────────────────────────────────────────────

    private IEnumerator ShowBubble(TextMeshProUGUI text, Image image,
        string line, bool isPlayer)
    {
        if (text == null) yield break;

        StopFloat(isPlayer);
        RecordOriginsIfNeeded();

        text.text = line;
        SetAlpha(text,  0f);
        SetAlpha(image, 0f);
        text.DOKill();
        image?.DOKill();

        // 淡入
        var fadeIn = DOTween.Sequence();
        fadeIn.Join(text.DOFade(1f, fadeDuration));
        if (image != null) fadeIn.Join(image.DOFade(1f, fadeDuration));
        yield return fadeIn.WaitForCompletion();

        // 浮动
        StartFloat(isPlayer);

        yield return new WaitForSeconds(displayDuration);

        // 淡出时保持浮动，淡出完成后再停止
        var fadeOut = DOTween.Sequence();
        fadeOut.Join(text.DOFade(0f, fadeDuration));
        if (image != null) fadeOut.Join(image.DOFade(0f, fadeDuration));
        yield return fadeOut.WaitForCompletion();

        StopFloat(isPlayer);
        RestorePositions(isPlayer);
    }

    // ─────────────────────────────────────────────────
    // 浮动
    // ─────────────────────────────────────────────────

    private void StartFloat(bool isPlayer)
    {
        if (isPlayer)
            _playerFloatCoroutine = StartCoroutine(FloatBubble(isPlayer));
        else
            _aiFloatCoroutine = StartCoroutine(FloatBubble(isPlayer));
    }

    private void StopFloat(bool isPlayer)
    {
        if (isPlayer)
        {
            if (_playerFloatCoroutine != null) { StopCoroutine(_playerFloatCoroutine); _playerFloatCoroutine = null; }
        }
        else
        {
            if (_aiFloatCoroutine != null) { StopCoroutine(_aiFloatCoroutine); _aiFloatCoroutine = null; }
        }
    }

    private IEnumerator FloatBubble(bool isPlayer)
    {
        RectTransform imageRT = isPlayer ? playerBubbleImage?.rectTransform : aiBubbleImage?.rectTransform;
        RectTransform textRT  = isPlayer ? playerBubbleText?.rectTransform  : aiBubbleText?.rectTransform;
        Vector2 imageOrigin   = isPlayer ? _playerImageOrigin : _aiImageOrigin;
        Vector2 textOrigin    = isPlayer ? _playerTextOrigin  : _aiTextOrigin;

        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime * floatSpeed;
            float offsetY = Mathf.Sin(elapsed * Mathf.PI) * floatAmplitude;
            if (imageRT != null) imageRT.anchoredPosition = imageOrigin + new Vector2(0f, offsetY);
            if (textRT  != null) textRT.anchoredPosition  = textOrigin  + new Vector2(0f, offsetY);
            yield return null;
        }
    }

    private void RestorePositions(bool isPlayer)
    {
        if (isPlayer)
        {
            if (playerBubbleImage != null) playerBubbleImage.rectTransform.anchoredPosition = _playerImageOrigin;
            if (playerBubbleText  != null) playerBubbleText.rectTransform.anchoredPosition  = _playerTextOrigin;
        }
        else
        {
            if (aiBubbleImage != null) aiBubbleImage.rectTransform.anchoredPosition = _aiImageOrigin;
            if (aiBubbleText  != null) aiBubbleText.rectTransform.anchoredPosition  = _aiTextOrigin;
        }
    }

    // ─────────────────────────────────────────────────
    // 辅助
    // ─────────────────────────────────────────────────

    private void RecordOriginsIfNeeded()
    {
        if (_originsRecorded) return;
        _playerImageOrigin = playerBubbleImage != null ? playerBubbleImage.rectTransform.anchoredPosition : Vector2.zero;
        _playerTextOrigin  = playerBubbleText  != null ? playerBubbleText.rectTransform.anchoredPosition  : Vector2.zero;
        _aiImageOrigin     = aiBubbleImage     != null ? aiBubbleImage.rectTransform.anchoredPosition     : Vector2.zero;
        _aiTextOrigin      = aiBubbleText      != null ? aiBubbleText.rectTransform.anchoredPosition      : Vector2.zero;
        _originsRecorded   = true;
    }

    private static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        var c = g.color; c.a = a; g.color = c;
    }
}