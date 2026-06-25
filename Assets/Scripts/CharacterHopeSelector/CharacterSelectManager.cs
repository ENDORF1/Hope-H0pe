using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 选角界面管理器。
/// 职责：
///   1. 读取 CharacterAsset 列表，动态生成角色卡
///   2. 驱动飞入、翻面、扇形展开动画序列
///   3. 处理选卡逻辑：消散未选中卡、坠落选中卡
///   4. 将选中角色写入 GameData，切换到战斗场景
///   5. 驱动背景冲击波涟漪动画
/// </summary>
public class CharacterSelectManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // Inspector 引用
    // ─────────────────────────────────────────────────

    [Header("角色数据")]
    [Tooltip("Hope 阵营所有角色的 CharacterAsset，按顺序填入")]
    public List<CharacterAsset> characters = new List<CharacterAsset>();

    [Header("卡牌 Prefab")]
    [Tooltip("角色卡 Prefab（挂有 CharacterCardUI 的预制体）")]
    public GameObject cardPrefab;

    [Header("场景引用")]
    [Tooltip("卡牌生成的父容器（Stage）")]
    public RectTransform cardStage;

    [Tooltip("标题下划线 Image（用于展开动画）")]
    public RectTransform titleUnderline;

    [Tooltip("副标题 TMP（淡入）")]
    public TextMeshProUGUI subTitle;

    [Tooltip("底部提示 TMP（延迟淡入）")]
    public TextMeshProUGUI promptText;

    [Tooltip("过场遮罩（全黑 Image，alpha 0→1）")]
    public CanvasGroup transitionOverlay;

    [Tooltip("遮罩上的文字（ENTERING BATTLE）")]
    public TextMeshProUGUI transitionText;

    [Tooltip("白色冲击闪光 Image（全屏，alpha 瞬间闪）")]
    public Image flashImage;

    [Header("背景涟漪")]
    [Tooltip("涟漪绘制的 RawImage（材质驱动）或直接用 RippleController 脚本）")]
    public RippleController rippleController;

    [Header("扇形展开参数")]
    [Tooltip("每张卡相对舞台中心的 X 偏移（从左到右）")]
    public float[] fanOffsetX = { -600f, -300f, 0f, 300f, 600f };

    [Tooltip("每张卡的旋转角（度）")]
    public float[] fanRotation = { -11f, -5f, 1f, 6f, 12f };

    [Tooltip("每张卡飞入的间隔（秒）")]
    public float cardFlyInInterval = 0.16f;

    [Tooltip("第一张卡飞入前的延迟（秒）")]
    public float flyInStartDelay = 0.3f;

    [Header("场景切换")]
    [Tooltip("战斗场景名称")]
    public string battleSceneName = "Battle Scene";

    [Tooltip("选中后到切换场景的延迟（秒）")]
    public float sceneLoadDelay = 1.8f;

    // ─────────────────────────────────────────────────
    // 运行时
    // ─────────────────────────────────────────────────

    private readonly List<CharacterCardUI> _cards = new List<CharacterCardUI>();
    private bool _locked = false;
    private bool _introPlayed = false;

    private float      _subTitleTargetAlpha = 0.27f;
    private float      _underlineTargetWidth = 400f;
    private GlitchText _promptGlitch;
    private string    _defaultPrompt;

    // ─────────────────────────────────────────────────
    void Start()
    {
        // 初始化遮罩
        if (transitionOverlay != null)
        {
            transitionOverlay.alpha          = 0f;
            transitionOverlay.blocksRaycasts = false;
        }
        if (flashImage != null)
        {
            Color c = flashImage.color; c.a = 0f;
            flashImage.color = c;
            flashImage.raycastTarget = false;
        }
        if (transitionText != null)
        {
            Color c = transitionText.color; c.a = 0f;
            transitionText.color = c;
        }
        if (subTitle != null)
        {
            if (subTitle.color.a > 0f) _subTitleTargetAlpha = subTitle.color.a;
            Color c = subTitle.color; c.a = 0f;
            subTitle.color = c;
        }
        if (titleUnderline != null)
        {
            if (titleUnderline.sizeDelta.x > 0f) _underlineTargetWidth = titleUnderline.sizeDelta.x;
            titleUnderline.sizeDelta = new Vector2(0f, titleUnderline.sizeDelta.y);
        }
        if (promptText != null)
        {
            Color c = promptText.color; c.a = 0f;
            promptText.color = c;
            _promptGlitch  = promptText.GetComponent<GlitchText>();
            _defaultPrompt = promptText.text;
        }

        SpawnCards();
        // 不立即播动画——等 CharacterSelectEntry.Reveal() 淡入后再触发
        // 独立运行模式（无 TitleScene）则自动播放
        if (CharacterSelectEntry.Instance != null && CharacterSelectEntry.Standalone)
            BeginIntro();
    }

    /// <summary>由 CharacterSelectEntry 在场景淡入后调用</summary>
    public void BeginIntro()
    {
        if (_introPlayed) return;
        _introPlayed = true;
        StartCoroutine(PlayIntroSequence());
    }

    // ─────────────────────────────────────────────────
    // 生成卡牌
    // ─────────────────────────────────────────────────

    private void SpawnCards()
    {
        if (cardPrefab == null)
        {
            Debug.LogError("[CharacterSelectManager] cardPrefab 未赋值！");
            return;
        }

        // 按实际角色数量决定扇形参数
        int count = Mathf.Min(characters.Count, fanOffsetX.Length);

        for (int i = 0; i < count; i++)
        {
            if (characters[i] == null) continue;

            GameObject go = Instantiate(cardPrefab, cardStage);
            go.name = $"Card_{characters[i].CharacterName}";

            var card = go.GetComponent<CharacterCardUI>();
            if (card == null)
            {
                Debug.LogError($"[CharacterSelectManager] Prefab 上没有 CharacterCardUI！");
                continue;
            }

            card.FanPosition = new Vector2(fanOffsetX[i], 0f);
            card.FanRotation = fanRotation[i];
            card.Init(characters[i], this);

            _cards.Add(card);
        }
    }

    // ─────────────────────────────────────────────────
    // 开场动画序列
    // ─────────────────────────────────────────────────

    private IEnumerator PlayIntroSequence()
    {
        yield return new WaitForSeconds(0.2f);

        // 标题下划线展开 → 完成后副标题 + 提示文字淡入
        if (titleUnderline != null)
        {
            titleUnderline.DOSizeDelta(
                new Vector2(_underlineTargetWidth, titleUnderline.sizeDelta.y),
                1.2f
            ).SetEase(Ease.OutQuart).OnComplete(() =>
            {
                // SubTitle：直接显示 + 启动 Writer 逐字打字
                if (subTitle != null)
                {
                    Color c = subTitle.color; c.a = _subTitleTargetAlpha;
                    subTitle.color = c;
                    var wave = subTitle.GetComponent<SubTitleWave>();
                    if (wave != null) wave.StartStandby();
                }
                if (promptText != null)
                    promptText.DOFade(1f, 0.8f);
            });
        }

        // 卡牌依次飞入
        for (int i = 0; i < _cards.Count; i++)
        {
            bool isLast = (i == _cards.Count - 1);
            int idx = i;

            _cards[i].PlayFlyIn(
                delay: flyInStartDelay + i * cardFlyInInterval,
                onComplete: isLast ? () => OnAllCardsReady() : null
            );
        }

        // 无卡牌时直接触发就位回调
        if (_cards.Count == 0)
            OnAllCardsReady();
    }

    // ─────────────────────────────────────────────────
    // Hover 提示切换
    // ─────────────────────────────────────────────────

    public void OnCardHovered(CharacterCardUI card)
    {
        if (_locked) return;
        string line = card.Data?.PromptLine;
        if (string.IsNullOrWhiteSpace(line)) return;
        _promptGlitch?.Glitch(line);
    }

    public void OnCardUnhovered()
    {
        if (_locked) return;
        if (string.IsNullOrEmpty(_defaultPrompt)) return;
        _promptGlitch?.Glitch(_defaultPrompt);
    }

    // ─────────────────────────────────────────────────
    // 所有卡就位后
    // ─────────────────────────────────────────────────

    private void OnAllCardsReady()
    {
        // 涟漪开始跳动
        rippleController?.StartBeating();
    }

    // ─────────────────────────────────────────────────
    // 选卡回调（由 CharacterCardUI 调用）
    // ─────────────────────────────────────────────────

    public void OnCardClicked(CharacterCardUI clicked)
    {
        if (_locked) return;
        _locked = true;

        // 写入全局数据
        GameData.SelectedCharacter = clicked.Data;
        GameData.SelectedFaction   = TitleScreenManager.Faction.Hope;

        Debug.Log($"[CharacterSelect] 选择角色：{clicked.Data.CharacterName}");

        // 白色冲击闪光
        PlayFlash();

        // 涟漪爆发
        rippleController?.SpawnBurst();

        // 隐藏提示
        if (promptText != null) promptText.DOFade(0f, 0.2f);

        // 其他卡消散
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i] == clicked) continue;
            _cards[i].PlayDismiss(delay: i * 0.025f);
        }

        // 选中卡：翻回卡背 → 飞升
        clicked.FlipToBack(() =>
        {
            clicked.PlayExit();
        });

        // 过渡遮罩
        DOVirtual.DelayedCall(sceneLoadDelay - 0.4f, () =>
        {
            if (transitionOverlay != null)
            {
                transitionOverlay.blocksRaycasts = true;
                transitionOverlay.DOFade(1f, 0.4f).OnComplete(() =>
                {
                    if (transitionText != null)
                        transitionText.DOFade(1f, 0.3f);
                });
            }
        });

        // 切换场景
        DOVirtual.DelayedCall(sceneLoadDelay, () =>
        {
            SceneManager.LoadScene(battleSceneName);
        });
    }

    // ─────────────────────────────────────────────────
    // 白色闪光
    // ─────────────────────────────────────────────────

    private void PlayFlash()
    {
        if (flashImage == null) return;
        var seq = DOTween.Sequence();
        seq.Append(flashImage.DOFade(0.22f, 0.05f));
        seq.Append(flashImage.DOFade(0f, 0.18f));
    }

    // ─────────────────────────────────────────────────
    void OnDestroy()
    {
        DOTween.KillAll();
    }
}
