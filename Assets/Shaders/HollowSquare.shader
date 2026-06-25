using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 标题界面管理器。
/// 负责：
///   1. 根据条件动态生成菜单按钮（不满足条件的按钮不生成，不是灰色）
///   2. 管理阵营切换（希望/熄忘）及翻转动画
///   3. 广播当前阵营给其他组件（按钮FX、背景动画等）
///   4. 管理所有UI文字内容（两套阵营文字均在Inspector里配置）
///
/// ── 按钮条件类型说明 ────────────────────────────────────────
///
///   Always
///     始终生成，无任何前提条件。
///     适用于：开始游戏、设定、退出等核心功能。
///
///   HasSave
///     只有当玩家存在未完成的存档时才生成。
///     由 RunManager.Instance.HasSave 判断（待RunManager扩展后启用）。
///     适用于：继续游戏。
///
/// ── 未来扩展说明 ────────────────────────────────────────────
///
///   如果将来需要加入"百科全书"、"统计"等可选功能按钮，
///   可以在 ButtonCondition 枚举里加入第三种条件类型：
///
///     FeatureEnabled
///       对应功能被开发者标记为"已开启"时才生成。
///       需要配套一个 TitleFeatureConfig ScriptableObject 来管理开关列表。
///       这样加新功能时只需改配置，不需要改脚本或场景结构。
///
/// ────────────────────────────────────────────────────────────
/// </summary>
public class TitleScreenManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // 阵营枚举
    // ─────────────────────────────────────────────────

    public enum Faction { Hope, Void }

    /// <summary>当前选中的阵营</summary>
    public Faction CurrentFaction { get; private set; } = Faction.Hope;

    /// <summary>
    /// 阵营切换时广播。
    /// 其他组件（背景动画、按钮FX等）订阅此事件以响应阵营变化。
    /// </summary>
    public static event System.Action<Faction> OnFactionChanged;

    // ─────────────────────────────────────────────────
    // 按钮条件类型
    // ─────────────────────────────────────────────────

    public enum ButtonCondition
    {
        /// <summary>始终生成</summary>
        Always,

        /// <summary>
        /// 有存档时才生成。
        /// 由 RunManager.Instance.HasSave 判断。
        /// 待 RunManager 扩展存档系统后，在 ShouldShowButton() 里启用对应判断。
        /// </summary>
        HasSave,
    }

    // ─────────────────────────────────────────────────
    // 按钮配置
    // ─────────────────────────────────────────────────

    [System.Serializable]
    public class MenuButtonConfig
    {
        [Tooltip("按钮显示文字")]
        public string Label;

        [Tooltip(
            "按钮的显示条件：\n" +
            "Always  = 始终显示\n" +
            "HasSave = 有存档时显示")]
        public ButtonCondition Condition = ButtonCondition.Always;

        [Tooltip("点击事件，在 Inspector 里绑定对应方法")]
        public UnityEngine.Events.UnityEvent OnClick;
    }

    // ─────────────────────────────────────────────────
    // 阵营文字配置
    // ─────────────────────────────────────────────────

    [System.Serializable]
    public class FactionTextConfig
    {
        [Tooltip("上方阵营标签，如 '— 救世方 · HOPE —'")]
        public string FactionLabel;

        [Tooltip("主标题，如 '希望'")]
        public string TitleMain;

        [Tooltip("副标题英文")]
        public string TitleSub;

        [Tooltip("切换按钮文字")]
        public string SwitchButtonText = "切换阵营";

        [Tooltip("下方阵营标签（倒影区域显示）")]
        public string FactionLabel2;
    }

    // ─────────────────────────────────────────────────
    // Inspector：UI引用
    // ─────────────────────────────────────────────────

    [Header("UI引用")]
    [SerializeField] private TextMeshProUGUI factionLabel;
    [SerializeField] private TextMeshProUGUI titleMain;
    [SerializeField] private TextMeshProUGUI titleSub;
    [SerializeField] private TextMeshProUGUI factionLabel2;
    [SerializeField] private Button          switchFactionButton;

    [Tooltip("翻转动画作用的容器（Content对象）")]
    [SerializeField] private RectTransform contentRoot;

    [Tooltip("按钮生成的容器（ButtonContainer对象）")]
    [SerializeField] private RectTransform buttonContainer;

    [Header("按钮Prefab")]
    [Tooltip("拖入 MenuButton Prefab")]
    [SerializeField] private GameObject menuButtonPrefab;

    [Header("希望阵营文字")]
    [SerializeField] private FactionTextConfig hopeText = new FactionTextConfig
    {
        FactionLabel     = "— 救世方 · HOPE —",
        TitleMain        = "希望",
        TitleSub         = "MAKE A WISH ON THE ENUMERABLE WORLD",
        SwitchButtonText = "切换阵营",
        FactionLabel2    = "— 救世方 · HOPE —",
    };

    [Header("熄忘阵营文字")]
    [SerializeField] private FactionTextConfig voidText = new FactionTextConfig
    {
        FactionLabel     = "— 柩世方 · VOID —",
        TitleMain        = "熄忘",
        TitleSub         = "CARVE SILENCE AND THE UNIVERSE OBEYS",
        SwitchButtonText = "切换阵营",
        FactionLabel2    = "— 柩世方 · VOID —",
    };

    [Header("按钮配置")]
    [Tooltip(
        "在此列表里配置所有可能出现的按钮。\n" +
        "运行时根据每个按钮的 Condition 决定是否生成。\n" +
        "不满足条件的按钮不会生成，不是灰色。")]
    [SerializeField] private List<MenuButtonConfig> buttonConfigs = new List<MenuButtonConfig>();

    [Header("阵营配色")]
    [SerializeField] private Color hopeColor = new Color(0.29f, 0.62f, 1f);
    [SerializeField] private Color voidColor = new Color(0.86f, 0.20f, 0.20f);

    [Header("场景名称")]
    [Tooltip("战斗场景的名称，需与 Build Settings 里的名称完全一致")]
    [SerializeField] private string battleSceneName = "Battle Scene";

    [Header("切换动画时长（秒）")]
    [SerializeField] private float switchDuration = 0.8f;

    // ─────────────────────────────────────────────────
    // 运行时
    // ─────────────────────────────────────────────────

    private List<Button>          _buttons     = new List<Button>();
    private List<TextMeshProUGUI> _buttonTexts = new List<TextMeshProUGUI>();
    private bool _switching = false;

    // ─────────────────────────────────────────────────
    // 初始化
    // ─────────────────────────────────────────────────

    void Start()
    {
        SetupLayout();
        GenerateButtons();
        SetupSwitchButton();
        ApplyFaction(CurrentFaction, instant: true);
    }

    // ─────────────────────────────────────────────────
    // 布局
    // ─────────────────────────────────────────────────

    private void SetupLayout()
    {
        if (buttonContainer == null) return;

        // 移除旧的Layout Group，改用手动等间距定位
        var oldH = buttonContainer.GetComponent<HorizontalLayoutGroup>();
        var oldV = buttonContainer.GetComponent<VerticalLayoutGroup>();
        if (oldH != null) Destroy(oldH);
        if (oldV != null) Destroy(oldV);
    }

    // ─────────────────────────────────────────────────
    // 按钮条件判断
    // ─────────────────────────────────────────────────

    private bool ShouldShowButton(MenuButtonConfig config)
    {
        switch (config.Condition)
        {
            case ButtonCondition.Always:
                return true;

            case ButtonCondition.HasSave:
                // 待 RunManager 扩展存档系统后，替换为：
                // return RunManager.Instance != null && RunManager.Instance.HasSave;
                return false;

            default:
                return false;
        }
    }

    // ─────────────────────────────────────────────────
    // 生成按钮
    // ─────────────────────────────────────────────────

    private void GenerateButtons()
    {
        if (menuButtonPrefab == null || buttonContainer == null)
        {
            Debug.LogError("[TitleScreenManager] menuButtonPrefab 或 buttonContainer 未赋值");
            return;
        }

        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);
        _buttons.Clear();
        _buttonTexts.Clear();

        // 先收集所有需要显示的按钮配置
        var visibleConfigs = new List<MenuButtonConfig>();
        foreach (var config in buttonConfigs)
        {
            if (ShouldShowButton(config))
                visibleConfigs.Add(config);
        }

        int count = visibleConfigs.Count;
        if (count == 0) return;

        float containerHeight = buttonContainer.rect.height;
        // 等间距：把容器高度平均分成 count 份，每个按钮放在各份的中心
        float slotHeight = containerHeight / count;

        // 容器锚点在底部居中，子对象锚点也设为底部居中
        // 从上到下排列：第0个在最上，最后一个在最下
        for (int i = 0; i < count; i++)
        {
            var config = visibleConfigs[i];
            GameObject go = Instantiate(menuButtonPrefab, buttonContainer);
            go.name = $"Btn_{config.Label}";

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                // 锚点设为中心
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);

                // 从容器顶部往下均匀分布
                float posY = containerHeight * 0.5f - slotHeight * (i + 0.5f);
                rt.anchoredPosition = new Vector2(0f, posY);
            }

            Button          btn = go.GetComponent<Button>();
            TextMeshProUGUI lbl = go.GetComponentInChildren<TextMeshProUGUI>();

            if (lbl != null)
            {
                lbl.text = config.Label;
            }

            // 将点击回调注入 MenuButtonFX，由它在动画结束后触发
            // Button.onClick 留空，不直接绑定
            var fx = go.GetComponent<MenuButtonFX>();
            if (fx != null)
            {
                fx.SetFaction(CurrentFaction);
                var captured = config;
                fx.SetClickCallback(() => captured.OnClick?.Invoke());
            }
            else if (btn != null)
            {
                // 没有 FX 脚本时退化为直接触发（保险用）
                var captured = config;
                btn.onClick.AddListener(() => captured.OnClick?.Invoke());
            }

            _buttons.Add(btn);
            _buttonTexts.Add(lbl);
        }
    }

    // ─────────────────────────────────────────────────
    // 阵营切换
    // ─────────────────────────────────────────────────

    private void SetupSwitchButton()
    {
        if (switchFactionButton == null) return;
        switchFactionButton.onClick.AddListener(SwitchFaction);
    }

    // 累计旋转角度，单向递增
    private float _currentRotation = 0f;

    public void SwitchFaction()
    {
        if (_switching) return;
        _switching = true;

        bool isVoid = CurrentFaction == Faction.Hope;
        float halfDuration = switchDuration * 0.5f;

        if (contentRoot != null)
        {
            float midRot    = _currentRotation + 90f;
            float targetRot = _currentRotation + 180f;

            // 第一阶段：顺时针转到+90度
            DOTween.To(() => _currentRotation, x => {
                _currentRotation = x;
                contentRoot.localEulerAngles = new Vector3(0, 0, x);
            }, midRot, halfDuration)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                CurrentFaction = isVoid ? Faction.Void : Faction.Hope;
                ApplyFaction(CurrentFaction, instant: true);
                OnFactionChanged?.Invoke(CurrentFaction);

                if (factionLabel2 != null)
                {
                    var glitch = factionLabel2.GetComponent<GlitchText>();
                    if (glitch != null)
                    {
                        FactionTextConfig txt2 = CurrentFaction == Faction.Hope ? hopeText : voidText;
                        glitch.Glitch(txt2.FactionLabel2);
                    }
                }

                for (int i = 0; i < _buttonTexts.Count; i++)
                {
                    if (_buttonTexts[i] == null) continue;
                    var glitch = _buttonTexts[i].GetComponent<GlitchText>();
                    if (glitch != null) glitch.GlitchInPlace();
                }

                // 第二阶段：继续顺时针转到+180度
                bool flipped = Mathf.RoundToInt(targetRot / 180f) % 2 == 1;
                Vector3 targetScale = flipped ? new Vector3(-1, 1, 1) : Vector3.one;

                DOTween.To(() => _currentRotation, x => {
                    _currentRotation = x;
                    contentRoot.localEulerAngles = new Vector3(0, 0, x);
                }, targetRot, halfDuration)
                .SetEase(Ease.OutCubic);

                contentRoot.DOScale(targetScale, halfDuration)
                    .SetEase(Ease.OutCubic)
                    .OnComplete(() => _switching = false);
            });
        }
        else
        {
            CurrentFaction = isVoid ? Faction.Void : Faction.Hope;
            ApplyFaction(CurrentFaction, instant: true);
            OnFactionChanged?.Invoke(CurrentFaction);
            _switching = false;
        }
    }

    // ─────────────────────────────────────────────────
    // 阵营应用（文字 + 配色）
    // ─────────────────────────────────────────────────

    private void ApplyFaction(Faction faction, bool instant)
    {
        FactionTextConfig txt = faction == Faction.Hope ? hopeText : voidText;
        Color c   = faction == Faction.Hope ? hopeColor : voidColor;
        float dur = instant ? 0f : switchDuration;

        // 文字内容
        if (factionLabel  != null) factionLabel.text  = txt.FactionLabel;
        if (titleMain     != null) titleMain.text     = txt.TitleMain;
        if (titleSub      != null) titleSub.text      = txt.TitleSub;

        // FactionLabel2：雪花效果切换（非即时时）
        if (factionLabel2 != null)
        {
            if (instant)
                factionLabel2.text = txt.FactionLabel2;
            else
            {
                var glitch = factionLabel2.GetComponent<GlitchText>();
                if (glitch != null) glitch.Glitch(txt.FactionLabel2);
                else factionLabel2.text = txt.FactionLabel2;
            }
        }

        var switchTmp = switchFactionButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (switchTmp != null) switchTmp.text = txt.SwitchButtonText;

        // 文字颜色
        ApplyColor(factionLabel,  c, 0.5f,  dur);
        ApplyColor(titleMain,     c, 1f,    dur);
        ApplyColor(titleSub,      c, 0.4f,  dur);
        ApplyColor(factionLabel2, c, 0.12f, dur);
        ApplyColor(switchTmp,     c, 1f,    dur);

        var switchImg = switchFactionButton?.GetComponent<Image>();
        if (switchImg != null)
        {
            Color border = new Color(c.r, c.g, c.b, 0.4f);
            if (instant) switchImg.color = border;
            else switchImg.DOColor(border, dur);
        }

        // 按钮：雪花效果切换文字颜色
        for (int i = 0; i < _buttons.Count; i++)
        {
            if (_buttonTexts[i] == null) continue;

            if (!instant)
            {
                var glitch = _buttonTexts[i].GetComponent<GlitchText>();
                if (glitch != null) glitch.GlitchInPlace();
            }

            if (instant) _buttonTexts[i].color = c;
            else _buttonTexts[i].DOColor(c, dur);

            var img = _buttons[i]?.GetComponent<Image>();
            if (img != null)
            {
                Color border = new Color(c.r, c.g, c.b, 0.3f);
                if (instant) img.color = border;
                else img.DOColor(border, dur);
            }
        }
    }

    private void ApplyColor(TextMeshProUGUI tmp, Color baseColor, float alpha, float duration)
    {
        if (tmp == null) return;
        Color target = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        if (duration <= 0f) tmp.color = target;
        else tmp.DOColor(target, duration);
    }

    // ─────────────────────────────────────────────────
    // 按钮事件（在 Inspector 的 OnClick 里绑定）
    // ─────────────────────────────────────────────────

    /// <summary>开始游戏：将阵营存入 RunManager，跳转战斗场景</summary>
    public void StartGame()
    {
        // TODO: RunManager.Instance?.SetStartFaction(CurrentFaction);
        Debug.Log($"[TitleScreen] 开始游戏，阵营：{CurrentFaction}");
        SceneManager.LoadScene(battleSceneName);
    }

    /// <summary>继续游戏：从存档恢复状态，跳转战斗场景</summary>
    public void ContinueGame()
    {
        // TODO: RunManager.Instance?.LoadSave();
        Debug.Log("[TitleScreen] 继续游戏（待实现）");
        SceneManager.LoadScene(battleSceneName);
    }

    /// <summary>打开设置面板</summary>
    public void OpenSettings()
    {
        // TODO: 打开设置UI
        Debug.Log("[TitleScreen] 设置（待实现）");
    }

    /// <summary>退出游戏</summary>
    public void QuitGame()
    {
        Debug.Log("[TitleScreen] 退出");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}