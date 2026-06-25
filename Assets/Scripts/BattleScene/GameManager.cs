using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using DG.Tweening;

/// <summary>
/// 回合流程状态机 + 时点系统。
/// 
/// 时点（QuickPlayTiming）标记当前速攻窗口的位置，
/// 速攻牌可以读取 CurrentTiming 判断自己是否能发动。
/// 
/// 完整回合流程：
///   TurnStart
///   → QP: TurnStart
///   → DrawAndDeploy
///   → QP: AfterDraw
///   → QP: BeforeBattle
///   → Combat 循环:
///       → QP: BeforeSlot(i)
///       → 翻牌/结算
///       → QP: AfterSlot(i)
///   → QP: AfterBattle
///   → BeforeMissile 播报
///   → QP: BeforeMissile
///   → QP: MissileReady（橙色高亮所有导弹）
///   → MissilePhase 结算
///   → QP: AfterMissile
///   → TurnEnd 过渡动画
///   → TurnEnd 播报
///   → QP: TurnEnd
/// </summary>
public enum TurnTiming
{
    None,
    TurnStart,       // 回合开始时
    AfterDraw,       // 抽牌部署后
    BeforeBattle,    // 战斗开始前
    BeforeSlot,      // 每格翻牌前
    BeforeEffect,    // 每格翻牌完成、伤害结算前
    AfterEffect,     // 每格伤害结算后
    AfterSlot,       // 每格完全结算后
    AfterBattle,     // 战斗阶段全部结算完毕后
    BeforeMissile,   // 导弹发射前
    MissileReady,    // 导弹即将发射（播报后，橙色高亮所有导弹）
    AfterMissile,    // 导弹结算后
    TurnEnd,         // 回合结束时
}

public class GameManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // 阶段 & 时点枚举
    // ─────────────────────────────────────────────────
    public enum TurnPhase
    {
        GameStart,
        TurnStart,
        DrawAndDeploy,
        Combat,
        MissilePhase,
        TurnEnd,
    }



    // ─────────────────────────────────────────────────
    // Inspector 引用
    // ─────────────────────────────────────────────────
    [Header("玩家引用")]
    [SerializeField] private PlayerState playerState;   // 拖入玩家的 PlayerState 对象
    [SerializeField] private PlayerState aiState;       // 拖入 AI 的 PlayerState 对象

    [Header("抽牌管理器")]
    [SerializeField] private DeckManager playerDeckManager; // 拖入玩家的 DeckManager
    [SerializeField] private DeckManager aiDeckManager;     // 拖入 AI 的 DeckManager

    [Header("部署区")]
    [SerializeField] private DeployZone playerDeployZone;   // 拖入玩家的 DeployZone
    [SerializeField] private DeployZone aiDeployZone;       // 拖入 AI 的 DeployZone

    [Header("战斗引擎")]
    [SerializeField] private CombatEngine combatEngine;     // 拖入场景中的 CombatEngine

    [Header("手牌管理")]
    [SerializeField] private HandManager playerHandManager; // 拖入玩家的 HandManager
    [SerializeField] private HandManager aiHandManager;     // 拖入 AI 的 HandManager
    [SerializeField] private GameObject  quickPlayTargetRoot; // 拖入场景中的 Target 对象

    [Header("UI")]
    [SerializeField] private MessageManager  messageManager;    // 拖入场景中的 MessageManager
    [SerializeField] private Button          endWindowButton;   // 拖入"结束窗口"按钮
    [SerializeField] private RopeVisual      ropeVisual;        // 拖入场景中的 RopeVisual（倒计时绳子）
    [SerializeField] private DeathCameraEffect deathCameraEffect; // 拖入场景中的 DeathCameraEffect
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private float deathDialogueWait = 1.5f;
    // 拖入玩家肖像根对象上的 OneCardManager（用于读取 CardFaceGlowImage 做血量光芒）
    [SerializeField] private OneCardManager  playerPortraitOCM;
    // 拖入 AI 肖像根对象上的 OneCardManager
    [SerializeField] private OneCardManager  aiPortraitOCM;
    // 拖入场景中覆盖全屏的黑色 Image（CanvasGroup alpha=0，用于回合过渡暗化效果）
    [SerializeField] private UnityEngine.UI.Image screenOverlay;

    [Header("开场肖像翻转")]
    [SerializeField] private BetterCardRotation playerPortraitRotation;
    [SerializeField] private BetterCardRotation aiPortraitRotation;
    [SerializeField] private float portraitFlipDelay = 0.3f; // 翻开前等待时长

    [Header("速攻窗口")]
    // 每个速攻窗口的时限（秒）。填 0 表示不限时，玩家必须手动点击结束按钮
    [SerializeField] private float quickPlayTimeLimit = 30f;
    // 勾选后，第一回合的【回合开始】速攻窗口会被跳过（因为第一回合开始时双方都没有手牌）
    [SerializeField] private bool  skipFirstTurnWindows = true;

    [Header("战斗镜头")]
    // 拖入场景中的主摄像机（Main Camera）。不填则跳过镜头动画
    [SerializeField] private Camera mainCamera;
    // 每格结算时镜头推进的幅度（orthographicSize 缩小值）。数值越大推进越明显，建议 1~2
    [SerializeField] private float slotZoomAmount   = 1.5f;
    // 镜头推进/复原的动画时长（秒）
    [SerializeField] private float slotZoomDuration = 0.3f;

    [Header("调试")]
    // 勾选后，每次阶段切换都会在 Console 打印日志
    [SerializeField] private bool logPhaseChanges = true;
    // 勾选后跳过所有开场动画、对话、MessageManager 等待，直接发牌进入速攻窗口
    [SerializeField] private bool skipIntroForDebug = false;
    /// <summary>供外部调试脚本读写</summary>
    public bool SkipIntroForDebug { get => skipIntroForDebug; set => skipIntroForDebug = value; }

    // ─────────────────────────────────────────────────
    // 运行时状态
    // ─────────────────────────────────────────────────
    public TurnPhase        CurrentPhase  { get; private set; } = TurnPhase.GameStart;
    public TurnTiming  CurrentTiming { get; private set; } = TurnTiming.None;
    public int              TurnNumber    { get; private set; } = 0;

    /// <summary>阶段变化时广播</summary>
    public event Action<TurnPhase>       OnPhaseChanged;
    /// <summary>速攻窗口开闭时广播（timing, isOpen）</summary>
    public event Action<TurnTiming, bool> OnQuickPlayWindow;

    private bool _playerEndedWindow = false;
    private float   _originalCameraSize;
    private Vector3 _originalCameraPos;

    // ─────────────────────────────────────────────────
    // 初始化
    // ─────────────────────────────────────────────────
    private bool _gameOver = false;

    // 额外战斗阶段
    private bool   _extraBattleRequested = false;
    private System.Action _extraBattleOnComplete;
    private System.Func<System.Collections.IEnumerator> _extraBattleOnStart;

    /// <summary>回合结束时触发（供 WanWuGuiXu 等副作用注册）</summary>
    public event System.Action OnTurnEnd;

    /// <summary>战斗阶段结束时触发（供 ModuleInstance 冷却递减）</summary>
    public event System.Action OnCombatEnd;

    void Start()
    {
        if (endWindowButton != null)
            endWindowButton.onClick.AddListener(OnPlayerEndWindow);

        SetHandInteractable(false);
        ShowEndWindowButton(false);

        // 监听死亡事件，任意一方死亡立即触发结算
        if (playerState != null) playerState.OnDeath += OnAnyPlayerDeath;
        if (aiState     != null) aiState.OnDeath     += OnAnyPlayerDeath;

        // 如果有入场动画控制器，等它完成后启动；否则直接启动
        var entrance = FindObjectOfType<HopeEntranceController>();
        if (entrance != null)
        {
            entrance.onCompleteTarget = this;
            entrance.onCompleteMethod = nameof(BeginGameLoop);
        }
        else
        {
            StartCoroutine(GameLoop());
        }
    }

    /// <summary>供 HopeEntranceController 回调，启动主循环</summary>
    public void BeginGameLoop()
    {
        StartCoroutine(GameLoop());
    }

    void OnDestroy()
    {
        if (endWindowButton != null)
            endWindowButton.onClick.RemoveListener(OnPlayerEndWindow);

        if (playerState != null) playerState.OnDeath -= OnAnyPlayerDeath;
        if (aiState     != null) aiState.OnDeath     -= OnAnyPlayerDeath;
    }

    private void OnAnyPlayerDeath()
    {
        if (_gameOver) return;
        _gameOver = true;
        StopAllCoroutines();
        StartCoroutine(HandleGameOver());
    }

    // ─────────────────────────────────────────────────
    // 主循环
    // ─────────────────────────────────────────────────
    private IEnumerator GameLoop()
    {
        yield return EnterPhase(TurnPhase.GameStart);

        if (!skipIntroForDebug)
        {
            if (messageManager != null) yield return StartCoroutine(messageManager.ShowGameStartAndWait());
            else yield return new WaitForSeconds(2f);

            // 双方肖像同时翻开
            yield return StartCoroutine(FlipPortraitsOnGameStart());

            if (dialogueManager != null)
                yield return StartCoroutine(dialogueManager.PlayEntryDialogues());
        }
        else
        {
            Debug.Log("[QuickStart] 跳过开场动画和进场对话，直接进入游戏");
        }

        while (!IsGameOver())
        {
            TurnNumber++;

            // ── 回合开始 ──────────────────────────────
            yield return EnterPhase(TurnPhase.TurnStart);

            if (!skipIntroForDebug)
            {
                if (messageManager != null) yield return StartCoroutine(messageManager.ShowTurnStartAndWait(TurnNumber));
                else yield return new WaitForSeconds(1f);
            }

            // 时点① 回合开始时
            yield return OpenQuickPlayWindow(TurnTiming.TurnStart);

            // ── 抽牌 + 部署 ───────────────────────────
            yield return EnterPhase(TurnPhase.DrawAndDeploy);
            if (!skipIntroForDebug)
            {
                if (messageManager != null) yield return StartCoroutine(messageManager.ShowDrawAndWait());
                else yield return new WaitForSeconds(0.5f);
            }
            yield return RunDrawAndDeploy();

            // 时点② 抽牌部署后
            yield return OpenQuickPlayWindow(TurnTiming.AfterDraw);

            // 时点③ 战斗开始前
            yield return EnterPhase(TurnPhase.Combat);
            if (!skipIntroForDebug)
            {
                if (messageManager != null) yield return StartCoroutine(messageManager.ShowBattlePhaseAndWait());
                else yield return new WaitForSeconds(1f);
            }
            yield return OpenQuickPlayWindow(TurnTiming.BeforeBattle);

            // BeforeBattle 窗口关闭后：手牌从左往右扫描，传递战斗即将开始的信息
            yield return StartCoroutine(SweepHandCards());

            // ── 战斗阶段（逐格）─────────────────────
            yield return RunCombat();

            // 时点：战斗阶段全部结算完毕后
            yield return OpenQuickPlayWindow(TurnTiming.AfterBattle);

            // 战斗结束：模块区从右往左扫橙色，暗示进入新阶段
            yield return StartCoroutine(SweepModules(rightToLeft: true));

            // ── 导弹阶段 ──────────────────────────────
            yield return EnterPhase(TurnPhase.MissilePhase);

            // VFX：所有导弹模块发射器浮现
            yield return TriggerMissileVFXAppear();

            // 时点⑥ 导弹发射前
            yield return OpenQuickPlayWindow(TurnTiming.BeforeMissile);

            if (!skipIntroForDebug)
            {
                if (messageManager != null) yield return StartCoroutine(messageManager.ShowMissilePhaseAndWait());
                else yield return new WaitForSeconds(0.5f);
            }

            // 时点⑦ 导弹即将发射：橙色高亮所有导弹
            combatEngine.SetAllMissilesHighlight(true);

            // VFX：底座弹开→发射管升起→碎块飞散
            yield return TriggerMissileVFXOpening();

            yield return OpenQuickPlayWindow(TurnTiming.MissileReady);
            combatEngine.SetAllMissilesHighlight(false);

            yield return RunMissilePhase();

            // VFX：归位→下沉消失
            yield return TriggerMissileVFXDisappear();

            // 时点⑦ 导弹结算后
            yield return OpenQuickPlayWindow(TurnTiming.AfterMissile);

            // AfterMissile 到 TurnEnd 之间的过渡：全屏暗化，然后双方肖像血量光芒
            yield return StartCoroutine(TurnEndTransition());

            // ── 回合结束 ──────────────────────────────
            yield return EnterPhase(TurnPhase.TurnEnd);

            // 先弹出回合结束播报，再开速攻窗口
            if (!skipIntroForDebug)
                if (messageManager != null) yield return StartCoroutine(messageManager.ShowTurnEndAndWait());

            // 时点⑧ 回合结束时（message 之后）
            yield return OpenQuickPlayWindow(TurnTiming.TurnEnd);

            // 触发回合结束事件（供 WanWuGuiXu 等副作用）
            OnTurnEnd?.Invoke();

            yield return RunTurnEnd();

            // 额外战斗阶段（天允终偿）
            if (_extraBattleRequested)
            {
                _extraBattleRequested = false;
                if (!skipIntroForDebug)
                {
                    // 播放额外战斗阶段的消息（天允终偿会替换为"赐你希望！"）
                    if (messageManager != null)
                    {
                        var msgCoroutine = _extraBattleOnStart != null
                            ? messageManager.ShowHopeAndWait()
                            : messageManager.ShowBattlePhaseAndWait();
                        yield return StartCoroutine(msgCoroutine);
                    }
                }
                // onStart 特效（边缘金色脉冲等）
                if (_extraBattleOnStart != null)
                {
                    yield return StartCoroutine(_extraBattleOnStart());
                    _extraBattleOnStart = null;
                }
                yield return RunCombat();
                _extraBattleOnComplete?.Invoke();
                _extraBattleOnComplete = null;
            }
        }

        yield return StartCoroutine(HandleGameOver());
    }

    // ─────────────────────────────────────────────────
    // 各阶段实现
    // ─────────────────────────────────────────────────
    private IEnumerator RunDrawAndDeploy()
    {
        // 双方同时填入溢出模块牌
        bool aiOverflowDone = false, playerOverflowDone = false;

        if (playerDeckManager != null)
            StartCoroutine(CoroutineAndWait(playerDeckManager.DeployOverflowModules(), () => playerOverflowDone = true));
        else
            playerOverflowDone = true;

        if (aiDeckManager != null)
            StartCoroutine(CoroutineAndWait(aiDeckManager.DeployOverflowModules(), () => aiOverflowDone = true));
        else
            aiOverflowDone = true;

        yield return new WaitUntil(() => aiOverflowDone && playerOverflowDone);

        // 双方同时抽牌
        bool aiDone = false, playerDone = false;

        if (aiDeckManager != null)
            StartCoroutine(DrawAndWait(aiDeckManager, 5, () => aiDone = true));
        else
            aiDone = true;

        if (playerDeckManager != null)
            StartCoroutine(DrawAndWait(playerDeckManager, 5, () => playerDone = true));
        else
            playerDone = true;

        yield return new WaitUntil(() => aiDone && playerDone);
    }

    private IEnumerator DrawAndWait(DeckManager deck, int count, System.Action onDone)
    {
        // 抽牌前记录牌库数量
        // 如果牌库剩余少于需要抽的张数，说明牌库不够用，抽完后判负
        int before = deck.CardCount;

        deck.DrawAndDistribute(count);
        yield return new WaitUntil(() => !deck.IsDrawing);

        // 判负条件：需要抽 count 张，但牌库只有 before 张（不够）
        // 即"试图从空牌库里继续抽牌"
        if (before < count)
        {
            Debug.Log($"[GameManager] 牌库不足（需要{count}张，只有{before}张），触发判负");
            var refs = SceneRefs.Instance;
            if (refs != null)
            {
                bool isPlayer = (deck == refs.PlayerDeckManager);
                if (isPlayer || deck == refs.AiDeckManager)
                {
                    if (messageManager != null)
                        yield return StartCoroutine(messageManager.ShowAndWait("<wave><color=red>无牌可抽...</color></wave>"));
                    TriggerSpecialDefeat("Defeat");
                }
            }
        }

        onDone?.Invoke();
    }

    private IEnumerator CoroutineAndWait(IEnumerator coroutine, System.Action onDone)
    {
        yield return StartCoroutine(coroutine);
        onDone?.Invoke();
    }

    private IEnumerator RunCombat()
    {
        if (combatEngine == null)
        {
            Debug.LogWarning("[GameManager] CombatEngine 未赋值！");
            yield break;
        }

        int slotCount = Mathf.Max(
            playerDeployZone != null ? playerDeployZone.CurrentSlotCount : 0,
            aiDeployZone     != null ? aiDeployZone.CurrentSlotCount     : 0
        );

        // 记录原始镜头状态
        if (mainCamera != null)
        {
            _originalCameraSize = mainCamera.orthographicSize;
            _originalCameraPos  = mainCamera.transform.position;
        }

        // 战斗开始：模块区从左往右闪烁，表达翻牌即将开始
        yield return StartCoroutine(SweepModules());

        for (int i = 0; i < slotCount; i++)
        {
            if (IsGameOver()) yield break;

            // 双方该格都没有模块，跳过
            bool playerHasModule = playerDeployZone != null
                && playerDeployZone.GetSlot(i)?.GetComponent<DeploySlot>()?.OccupyingModuleInstance != null;
            bool aiHasModule = aiDeployZone != null
                && aiDeployZone.GetSlot(i)?.GetComponent<DeploySlot>()?.OccupyingModuleInstance != null;
            if (!playerHasModule && !aiHasModule) continue;

            // 时点④ 每格翻牌前：橙色高亮当前格，告知玩家此格即将结算
            combatEngine.SetSlotWindowHighlight(i, true);
            _currentCombatSlot = i;
            yield return OpenQuickPlayWindow(TurnTiming.BeforeSlot);

            if (IsGameOver()) yield break;

            // 速攻窗口关闭后：橙色平滑过渡到霓虹青，同时镜头推进
            combatEngine.TransitionSlotHighlight(i);
            yield return StartCoroutine(ZoomToSlot(i));

            // 时点：效果触发前（翻牌完成、伤害即将结算）
            yield return OpenQuickPlayWindow(TurnTiming.BeforeEffect);
            if (IsGameOver()) yield break;

            // 战斗引擎结算当前格
            yield return combatEngine.ResolveSlot(i);

            // 时点：效果触发后（伤害已结算）
            yield return OpenQuickPlayWindow(TurnTiming.AfterEffect);
            if (IsGameOver()) yield break;

            // 互攻阶段（实弹/冷兵器）
            yield return combatEngine.ResolveAttackPhase(i);
            if (IsGameOver()) yield break;

            // 结算完成：镜头复原
            yield return StartCoroutine(ZoomToOriginal());

            // 时点⑤ 每格完全结算后
            yield return OpenQuickPlayWindow(TurnTiming.AfterSlot);
        }

        // 战斗阶段全部结算完毕，触发冷却递减
        OnCombatEnd?.Invoke();
    }

    private IEnumerator ZoomToSlot(int slotIndex)
    {
        if (mainCamera == null || !mainCamera.orthographic) yield break;

        // 取双方对应格子的世界坐标中点
        Vector3 target = _originalCameraPos;
        int count = 0;

        GameObject playerSlot = playerDeployZone?.GetSlot(slotIndex);
        GameObject aiSlot     = aiDeployZone?.GetSlot(slotIndex);

        if (playerSlot != null) { target += playerSlot.transform.position; count++; }
        if (aiSlot     != null) { target += aiSlot.transform.position;     count++; }
        if (count > 0)            target /= count;

        target.z = _originalCameraPos.z; // 保持 Z 不变

        float targetSize = _originalCameraSize - slotZoomAmount;

        var seq = DG.Tweening.DOTween.Sequence();
        seq.Join(DG.Tweening.DOTween.To(
            () => mainCamera.orthographicSize,
            x  => mainCamera.orthographicSize = x,
            targetSize, slotZoomDuration).SetEase(DG.Tweening.Ease.OutCubic));
        seq.Join(mainCamera.transform.DOMove(target, slotZoomDuration)
            .SetEase(DG.Tweening.Ease.OutCubic));

        yield return seq.WaitForCompletion();
    }

    private IEnumerator ZoomToOriginal()
    {
        if (mainCamera == null || !mainCamera.orthographic) yield break;

        var seq = DG.Tweening.DOTween.Sequence();
        seq.Join(DG.Tweening.DOTween.To(
            () => mainCamera.orthographicSize,
            x  => mainCamera.orthographicSize = x,
            _originalCameraSize, slotZoomDuration).SetEase(DG.Tweening.Ease.InOutCubic));
        seq.Join(mainCamera.transform.DOMove(_originalCameraPos, slotZoomDuration)
            .SetEase(DG.Tweening.Ease.InOutCubic));

        yield return seq.WaitForCompletion();
    }

    private IEnumerator RunMissilePhase()
    {
        if (combatEngine != null)
            yield return combatEngine.RunMissilePhase();
    }

    private IEnumerator RunTurnEnd()
    {
        NotifyAllModulesTurnEnd();
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator FlipPortraitsOnGameStart()
    {
        // 确保卡背朝上
        playerPortraitRotation?.ShowBack();
        aiPortraitRotation?.ShowBack();

        yield return new WaitForSeconds(portraitFlipDelay);

        // 双方同时翻开
        bool p1Done = false, p2Done = false;

        if (playerPortraitRotation != null)
            StartCoroutine(FlipAndFlag(playerPortraitRotation, () => p1Done = true));
        else
            p1Done = true;

        if (aiPortraitRotation != null)
            StartCoroutine(FlipAndFlag(aiPortraitRotation, () => p2Done = true));
        else
            p2Done = true;

        yield return new WaitUntil(() => p1Done && p2Done);
    }

    private IEnumerator FlipAndFlag(BetterCardRotation rot, System.Action onDone)
    {
        rot.FlipWithAnimation(true);
        yield return new WaitForSeconds(rot.FlipTotalDuration);
        onDone?.Invoke();
    }

    // 回合结束过渡：全屏暗化 → 双方肖像按血量百分比亮起对应颜色光芒
    private IEnumerator TurnEndTransition()
    {
        // ── 第一步：全屏暗化再亮起 ──────────────────
        if (screenOverlay != null)
        {
            screenOverlay.gameObject.SetActive(true);
            screenOverlay.color = new Color(0f, 0f, 0f, 0f);

            // 淡入到半透明黑
            yield return DOTween.To(
                () => screenOverlay.color.a,
                a  => { var c = screenOverlay.color; c.a = a; screenOverlay.color = c; },
                0.6f, 0.35f).WaitForCompletion();

            yield return new WaitForSeconds(0.1f);

            // 淡出
            yield return DOTween.To(
                () => screenOverlay.color.a,
                a  => { var c = screenOverlay.color; c.a = a; screenOverlay.color = c; },
                0f, 0.35f).WaitForCompletion();

            screenOverlay.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        // ── 第二步：双方肖像按血量亮起对应颜色 ──────
        ShowPortraitHealthGlow(playerPortraitOCM, playerState);
        ShowPortraitHealthGlow(aiPortraitOCM,     aiState);

        yield return new WaitForSeconds(1.2f); // 让玩家看清血量颜色

        // 淡出肖像光芒
        FadeOutPortraitGlow(playerPortraitOCM);
        FadeOutPortraitGlow(aiPortraitOCM);

        yield return new WaitForSeconds(0.4f);
    }

    private void ShowPortraitHealthGlow(OneCardManager ocm, PlayerState state)
    {
        if (ocm == null || state == null) return;
        UnityEngine.UI.Image glow = ocm.CardFaceGlowImage;
        if (glow == null) return;

        float pct = (float)state.TotalHealth / state.MaxHealth; // 血量百分比

        Color glowColor;
        if (pct > 0.6f)
            glowColor = new Color(0f, 1f, 0.3f);       // 绿色：血量充足（>60%）
        else if (pct > 0.3f)
            glowColor = new Color(1f, 0.85f, 0f);      // 黄色：血量偏低（30~60%）
        else
            glowColor = new Color(1f, 0.15f, 0f);      // 红色：血量危险（<30%）

        glowColor.a = 0f;
        glow.DOKill();
        glow.color  = glowColor;
        glow.enabled = true;

        if (pct <= 0.3f)
        {
            // 危险状态：闪烁警示
            DOTween.Sequence()
                .Append(DOTween.To(() => glow.color.a,
                    a => { var c = glow.color; c.a = a; glow.color = c; }, 1f, 0.15f))
                .Append(DOTween.To(() => glow.color.a,
                    a => { var c = glow.color; c.a = a; glow.color = c; }, 0.3f, 0.15f))
                .SetLoops(3, LoopType.Yoyo);
        }
        else
        {
            // 正常状态：平滑亮起
            DOTween.To(() => glow.color.a,
                a => { var c = glow.color; c.a = a; glow.color = c; }, 1f, 0.3f);
        }
    }

    private void FadeOutPortraitGlow(OneCardManager ocm)
    {
        if (ocm == null) return;
        UnityEngine.UI.Image glow = ocm.CardFaceGlowImage;
        if (glow == null) return;

        glow.DOKill();
        DOTween.To(() => glow.color.a,
            a => { var c = glow.color; c.a = a; glow.color = c; }, 0f, 0.4f)
            .OnComplete(() => glow.enabled = false);
    }

    // 每个速攻窗口打开时调用：可打出的牌橙色常亮，不可打的熄灭
    /// <summary>玩家正在拖拽速攻牌时为 true，暂停手牌高亮更新避免覆盖拖拽发光</summary>
    public bool IsPlayerDragging { get; set; } = false;

    public void RefreshHandHighlight()
    {
        HighlightHandByPlayability(CurrentTiming);
    }

    /// <summary>
    /// 速攻窗口仍开启时重新激活手牌交互。
    /// 供天允终偿等在窗口内异步执行的效果调用，确保抽来的新牌可以拖动。
    /// </summary>
    public void ReactivateHandIfWindowOpen()
    {
        if (CurrentTiming == TurnTiming.None) return; // 窗口已关闭，不处理
        SetHandInteractable(true);
        ShowEndWindowButton(true);
        HighlightHandByPlayability(CurrentTiming);
    }

    private void HighlightHandByPlayability(TurnTiming timing)
    {
        if (playerHandManager == null) return;
        var cards = playerHandManager.GetHandCards();
        if (cards == null) return;

        foreach (var card in cards)
        {
            if (card == null) continue;

            // 正在拖拽的牌：跳过
            QuickPlayDraggable drag = card.GetComponent<QuickPlayDraggable>();
            if (IsPlayerDragging && drag != null && drag.IsBeingDragged) continue;

            OneCardManager ocm = card.GetComponent<OneCardManager>();
            HoverPreview   hp  = card.GetComponent<HoverPreview>();

            // 预览展开中的牌：跳过
            if (hp != null && hp.IsForcedOpen) continue;

            // 优先用 HoverPreview.smallCardGlow，没有则回退到 OCM 的 CardFaceGlowImage
            UnityEngine.UI.Image glow = (hp != null && hp.smallCardGlow != null)
                ? hp.smallCardGlow
                : (ocm != null ? ocm.CardFaceGlowImage : null);
            if (glow == null) { Debug.LogWarning($"[Highlight] {card.name} glow为null hp={hp!=null} smallCardGlow={hp?.smallCardGlow!=null} ocm={ocm!=null} CardFaceGlowImage={ocm?.CardFaceGlowImage!=null}"); continue; }

            // 有其他牌正在拖拽中：其余所有牌熄灭
            if (IsPlayerDragging)
            {
                DOTween.Kill(glow, complete: false);
                glow.enabled = false;
                continue;
            }

            QuickPlayTargetSelector selector = card.GetComponent<QuickPlayTargetSelector>();
            bool canPlay = selector != null
                ? selector.IsPlayableAt(timing, CurrentPhase)
                : (ocm != null && ocm.cardAsset != null && ocm.cardAsset.CardType != CardType.Module);

            Debug.Log($"[Highlight] {card.name} canPlay={canPlay} timing={timing} selector={selector!=null} cardAsset={selector?.cardAsset!=null}");

            if (canPlay)
            {
                DOTween.Kill(glow, complete: false);
                glow.gameObject.SetActive(true);
                glow.enabled = true;
                string glowId = "handglow_" + glow.GetInstanceID();
                DOTween.Kill(glowId);

                // 检查是否需要彩虹光效
                CardAsset cardAsset = ocm != null ? ocm.cardAsset : null;
                bool rainbow = cardAsset != null && cardAsset.RainbowGlowWhenPlayable;

                if (rainbow)
                {
                    // 每次呼吸（亮→暗→亮）完成后换下一个颜色
                    float breathDuration = 0.9f;
                    int   colorIndex     = 0;
                    float[] hues         = { 0f, 0.08f, 0.17f, 0.33f, 0.5f, 0.67f, 0.83f }; // 红橙黄绿青蓝紫

                    glow.color = new Color(
                        Color.HSVToRGB(hues[0], 1f, 1f).r,
                        Color.HSVToRGB(hues[0], 1f, 1f).g,
                        Color.HSVToRGB(hues[0], 1f, 1f).b, 1f);
                    glow.enabled = true;

                    System.Action breathCycle = null;
                    breathCycle = () =>
                    {
                        if (glow == null || !glow.enabled) return;
                        Color col = Color.HSVToRGB(hues[colorIndex % hues.Length], 1f, 1f);
                        glow.color = new Color(col.r, col.g, col.b, 1f);
                        DOTween.To(() => glow.color.a,
                            a => { var c = glow.color; c.a = a; glow.color = c; },
                            0.35f, breathDuration)
                            .SetEase(Ease.InOutSine)
                            .SetId(glowId)
                            .OnComplete(() =>
                            {
                                if (glow == null || !glow.enabled) return;
                                colorIndex++;
                                Color next = Color.HSVToRGB(hues[colorIndex % hues.Length], 1f, 1f);
                                glow.color = new Color(next.r, next.g, next.b, 0.35f);
                                DOTween.To(() => glow.color.a,
                                    a => { var c = glow.color; c.a = a; glow.color = c; },
                                    1f, breathDuration)
                                    .SetEase(Ease.InOutSine)
                                    .SetId(glowId)
                                    .OnComplete(() => breathCycle())
                                    .SetLink(glow.gameObject);
                            })
                            .SetLink(glow.gameObject);
                    };
                    breathCycle();
                }
                else
                {
                    // 普通绿色呼吸
                    glow.color = new Color(0.2f, 1f, 0.3f, 1f);
                    DOTween.To(() => glow.color.a,
                        a => { var c = glow.color; c.a = a; glow.color = c; },
                        0.35f, 0.9f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetId(glowId);
                }
            }
            else
            {
                DOTween.Kill(glow, complete: false);
                glow.enabled = false;
            }
        }
    }

    private IEnumerator SweepHandCards()
    {
        if (playerHandManager == null) yield break;

        var cards = playerHandManager.GetHandCards();
        if (cards == null || cards.Count == 0) yield break;

        int   count      = cards.Count;
        float stagger    = 0.08f;
        float holdDur    = 0.15f;
        float fadeOutDur = 0.25f;

        for (int i = 0; i < count; i++)
        {
            if (cards[i] == null) continue;
            OneCardManager ocm = cards[i].GetComponent<OneCardManager>();
            UnityEngine.UI.Image glow = ocm != null ? ocm.CardFaceGlowImage : null;
            if (glow == null) continue;

            float hue = count > 1 ? (float)i / (count - 1) * 0.75f : 0f;
            Color col = Color.HSVToRGB(hue, 1f, 1f);
            col.a = 0f;

            float delay = i * stagger;
            UnityEngine.UI.Image captured = glow;
            Color capturedCol = col;

            DG.Tweening.DOTween.Sequence()
                .AppendInterval(delay)
                .AppendCallback(() =>
                {
                    captured.enabled = true;
                    captured.color   = capturedCol; // alpha=0，从透明开始
                })
                .Append(DOTween.To(
                    () => captured.color.a,
                    a  => { var c = captured.color; c.a = a; captured.color = c; },
                    1f, 0.05f))
                .AppendInterval(holdDur)
                .Append(DOTween.To(
                    () => captured.color.a,
                    a  => { var c = captured.color; c.a = a; captured.color = c; },
                    0f, fadeOutDur).SetEase(DG.Tweening.Ease.InQuart))
                .OnComplete(() => { if (captured != null) captured.enabled = false; });
        }

        yield return new WaitForSeconds(count * stagger + holdDur + fadeOutDur);
    }

    // 模块区闪烁：rightToLeft=false 从左往右（战斗开始），true 从右往左（战斗结束）
    private IEnumerator SweepModules(bool rightToLeft = false)
    {
        int slotCount = Mathf.Max(
            playerDeployZone != null ? playerDeployZone.CurrentSlotCount : 0,
            aiDeployZone     != null ? aiDeployZone.CurrentSlotCount     : 0
        );
        if (slotCount == 0) yield break;

        float stagger  = 0.08f;
        float flashIn  = 0.05f;
        float flashOut = 0.3f;
        Color flashColor = new Color(1f, 0.55f, 0f); // 橙色

        for (int i = 0; i < slotCount; i++)
        {
            int idx = rightToLeft ? (slotCount - 1 - i) : i;
            FlashModuleGlow(playerDeployZone, idx, flashColor, i * stagger, flashIn, flashOut);
            FlashModuleGlow(aiDeployZone,     idx, flashColor, i * stagger, flashIn, flashOut);
        }

        yield return new WaitForSeconds(slotCount * stagger + flashOut);
    }

    private void FlashModuleGlow(DeployZone zone, int index, Color color,
        float delay, float flashIn, float flashOut)
    {
        if (zone == null) return;
        GameObject slotObj = zone.GetSlot(index);
        if (slotObj == null) return;
        DeploySlot slot = slotObj.GetComponent<DeploySlot>();
        if (slot == null || slot.OccupyingModule == null) return;

        Transform glowT = slot.OccupyingModule.transform.Find("Canvas/CardGlow");
        if (glowT == null) return;
        Image glow = glowT.GetComponent<Image>();
        if (glow == null) return;

        glow.enabled = true;
        glow.color = new Color(color.r, color.g, color.b, 0f);

        DG.Tweening.DOTween.Sequence()
            .AppendInterval(delay)
            .AppendCallback(() => glow.color = new Color(color.r, color.g, color.b, 0f))
            .Append(DOTween.To(() => glow.color.a, a => { var c = glow.color; c.a = a; glow.color = c; }, 1f, flashIn))
            .Append(DOTween.To(() => glow.color.a, a => { var c = glow.color; c.a = a; glow.color = c; }, 0f, flashOut).SetEase(DG.Tweening.Ease.InQuart))
            .OnComplete(() => { if (glow != null) glow.enabled = false; });
    }

    // ─────────────────────────────────────────────────
    // 速攻窗口
    // ─────────────────────────────────────────────────

    /// <summary>当前战斗格索引（BeforeSlot/AfterSlot 时点时有效）</summary>
    public int CurrentCombatSlot => _currentCombatSlot;
    private int _currentCombatSlot = -1;

    private IEnumerator OpenQuickPlayWindow(TurnTiming timing)
    {
        // 第一回合跳过回合开始速攻窗口
        if (skipFirstTurnWindows && TurnNumber == 1 && timing == TurnTiming.TurnStart)
            yield break;
        CurrentTiming      = timing;
        _playerEndedWindow = false;

        OnQuickPlayWindow?.Invoke(timing, true);

        // AI 先决定
        yield return RunAIQuickPlay(timing);

        // 开启玩家交互
        SetHandInteractable(true);
        ShowEndWindowButton(true);
        ropeVisual?.StartRope(quickPlayTimeLimit);
        HighlightHandByPlayability(timing);

        float elapsed = 0f;
        while (!_playerEndedWindow)
        {
            elapsed += Time.deltaTime;
            if (quickPlayTimeLimit > 0 && elapsed >= quickPlayTimeLimit)
                break;
            yield return null;
        }

        // 关闭窗口
        SetHandInteractable(false);
        ShowEndWindowButton(false);
        ropeVisual?.StopRope();

        CurrentTiming = TurnTiming.None;
        OnQuickPlayWindow?.Invoke(timing, false);
    }

    private IEnumerator RunAIQuickPlay(TurnTiming timing)
    {
        // TODO: 接入 AI 速攻牌逻辑，传入 timing 判断能打哪些牌
        yield return null;
    }

    public void OnPlayerEndWindow()
    {
        _playerEndedWindow = true;
    }

    // ─────────────────────────────────────────────────
    // 手牌区交互开关
    // ─────────────────────────────────────────────────
    private void SetHandInteractable(bool interactable)
    {
        if (playerHandManager == null) return;
        foreach (var card in playerHandManager.GetHandCards())
        {
            if (card == null) continue;

            CanvasGroup cg = card.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.interactable   = interactable;
                cg.blocksRaycasts = interactable;
            }

            QuickPlayDraggable drag = card.GetComponent<QuickPlayDraggable>();
            if (drag != null)
                drag.CanDrag = interactable;
        }
    }

    private void ShowEndWindowButton(bool show)
    {
        if (endWindowButton != null)
            endWindowButton.gameObject.SetActive(show);
    }

    // ─────────────────────────────────────────────────
    // 阶段切换
    // ─────────────────────────────────────────────────
    private IEnumerator EnterPhase(TurnPhase phase)
    {
        CurrentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
        if (logPhaseChanges)
            Debug.Log($"[GameManager] Turn {TurnNumber} → {phase}");
        yield return null;
    }

    // 回合结束：清除过热
    private void NotifyAllModulesTurnEnd()
    {
        NotifyZoneTurnEnd(playerDeployZone);
        NotifyZoneTurnEnd(aiDeployZone);
    }

    private void NotifyZoneTurnEnd(DeployZone zone)
    {
        if (zone == null) return;
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            GameObject slotObj = zone.GetSlot(i);
            if (slotObj == null) continue;
            DeploySlot slot = slotObj.GetComponent<DeploySlot>();
            ModuleInstance m = slot?.OccupyingModuleInstance;
            if (m == null) continue;
            m.ClearOverheat(); // 过热冻结持续一回合，回合结束时解除
        }
    }

    // ─────────────────────────────────────────────────
    // 胜负
    // ─────────────────────────────────────────────────
    private bool IsGameOver() => _gameOver;

    /// <summary>
    /// 以自定义消息触发玩家败北（万物归墟死亡副作用）。
    /// </summary>
    public void TriggerSpecialDefeat(string message)
    {
        if (_gameOver) return;
        _gameOver = true;
        StopAllCoroutines();
        StartCoroutine(HandleSpecialDefeat(message));
    }

    private IEnumerator HandleSpecialDefeat(string message)
    {
        SetHandInteractable(false);
        ShowEndWindowButton(false);
        ropeVisual?.StopRope();

        if (deathCameraEffect != null)
            yield return StartCoroutine(deathCameraEffect.PlayDeathEffect(true, false));

        dialogueManager?.PlayDeathDialogue(true, false);
        yield return new WaitForSeconds(deathDialogueWait);
        if (dialogueManager != null) yield return StartCoroutine(dialogueManager.FadeOutAll());

        if (messageManager != null) yield return StartCoroutine(messageManager.ShowAndWait($"<wave><palette>{message}</palette></wave>"));
        Debug.Log($"[GameManager] 特殊败北：{message}");
    }

    /// <summary>
    /// 请求在本回合结束后额外执行一次战斗阶段（天允终偿）。
    /// onComplete 在额外阶段结束后调用。
    /// </summary>
    /// <summary>
    /// onStart：额外战斗阶段消息播完后、战斗开始前执行的协程（用于特效）。
    /// onComplete：额外战斗阶段结束后执行的回调。
    /// </summary>
    public void RequestExtraBattlePhase(
        System.Action onComplete = null,
        System.Func<System.Collections.IEnumerator> onStart = null)
    {
        _extraBattleRequested  = true;
        _extraBattleOnComplete = onComplete;
        _extraBattleOnStart    = onStart;
    }

    /// <summary>
    /// 根据模块归属返回敌方部署区。
    /// </summary>
    public DeployZone GetEnemyDeployZone(PlayerState owner)
    {
        if (owner == playerState) return aiDeployZone;
        if (owner == aiState)     return playerDeployZone;
        return null;
    }

    /// <summary>根据归属返回对应 DeckManager</summary>
    public DeckManager GetDeckManager(PlayerState owner)
    {
        if (owner == playerState) return playerDeckManager;
        if (owner == aiState)     return aiDeckManager;
        return null;
    }

    /// <summary>根据归属返回敌方 HandManager</summary>
    public HandManager GetEnemyHandManager(PlayerState owner)
    {
        if (owner == playerState) return aiHandManager;
        if (owner == aiState)     return playerHandManager;
        return null;
    }

    /// <summary>玩家 PlayerState（供自定义脚本查询）</summary>
    public PlayerState PlayerState => playerState;

    /// <summary>玩家 HandManager（供自定义脚本查询）</summary>
    public HandManager GetPlayerHandManager() => playerHandManager;

    /// <summary>MessageManager（供自定义脚本直接调用）</summary>
    public MessageManager MessageManager => messageManager;

    /// <summary>DialogueManager（供自定义脚本直接调用）</summary>
    public DialogueManager DialogueManager => dialogueManager;

    private IEnumerator HandleGameOver()
    {
        bool playerDead = playerState != null && playerState.IsDead;
        bool aiDead     = aiState     != null && aiState.IsDead;

        if (!playerDead && !aiDead) yield break;

        SetHandInteractable(false);
        ShowEndWindowButton(false);
        ropeVisual?.StopRope();

        if (deathCameraEffect != null)
            yield return StartCoroutine(deathCameraEffect.PlayDeathEffect(playerDead, aiDead));

        // 死亡对话（翻面动画结束后触发）
        dialogueManager?.PlayDeathDialogue(playerDead, aiDead);

        if (playerDead && aiDead)
        {
            yield return new WaitForSeconds(deathDialogueWait);
            if (dialogueManager != null) yield return StartCoroutine(dialogueManager.FadeOutAll());
            if (messageManager != null) yield return StartCoroutine(messageManager.ShowDrawAndWait());
            Debug.Log("[GameManager] 双方同时败北，平局！");
        }
        else if (playerDead)
        {
            yield return new WaitForSeconds(deathDialogueWait);
            if (dialogueManager != null) yield return StartCoroutine(dialogueManager.FadeOutAll());
            if (messageManager != null) yield return StartCoroutine(messageManager.ShowDefeatAndWait());
            Debug.Log("[GameManager] 玩家败北！");
        }
        else
        {
            yield return new WaitForSeconds(deathDialogueWait);
            if (dialogueManager != null) yield return StartCoroutine(dialogueManager.FadeOutAll());
            if (messageManager != null) yield return StartCoroutine(messageManager.ShowVictoryAndWait());
            Debug.Log("[GameManager] 玩家胜利！");
        }
    }

    // ─────────────────────────────────────────────────
    // 导弹VFX辅助方法
    // ─────────────────────────────────────────────────

    private IEnumerator TriggerMissileVFXAppear()
    {
        var vfxList = CollectMissileVFX();
        if (vfxList.Count == 0) yield break;
        var coroutines = new System.Collections.Generic.List<Coroutine>();
        foreach (var vfx in vfxList)
            coroutines.Add(StartCoroutine(vfx.CallAppear()));
        foreach (var c in coroutines)
            yield return c;
    }

    private IEnumerator TriggerMissileVFXOpening()
    {
        var vfxList = CollectMissileVFX();
        if (vfxList.Count == 0) yield break;
        var coroutines = new System.Collections.Generic.List<Coroutine>();
        foreach (var vfx in vfxList)
            coroutines.Add(StartCoroutine(vfx.CallOpeningAnimation()));
        foreach (var c in coroutines)
            yield return c;
    }

    private IEnumerator TriggerMissileVFXDisappear()
    {
        var vfxList = CollectMissileVFX();
        if (vfxList.Count == 0) yield break;
        var coroutines = new System.Collections.Generic.List<Coroutine>();
        foreach (var vfx in vfxList)
            coroutines.Add(StartCoroutine(vfx.CallDisappear()));
        foreach (var c in coroutines)
            yield return c;
    }

    private System.Collections.Generic.List<MissileModuleVFX> CollectMissileVFX()
    {
        var result = new System.Collections.Generic.List<MissileModuleVFX>();
        CollectVFXFromZone(playerDeployZone, result);
        CollectVFXFromZone(aiDeployZone,     result);
        return result;
    }

    private void CollectVFXFromZone(
        DeployZone zone,
        System.Collections.Generic.List<MissileModuleVFX> result)
    {
        if (zone == null) return;
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            GameObject slotObj = zone.GetSlot(i);
            if (slotObj == null) continue;
            DeploySlot slot = slotObj.GetComponent<DeploySlot>();
            ModuleInstance m = slot?.OccupyingModuleInstance;
            if (m == null || !m.IsAlive || m.Asset.ModuleType != ModuleType.Missile) continue;
            MissileModuleVFX vfx = m.GetComponent<MissileModuleVFX>();
            if (vfx != null) result.Add(vfx);
        }
    }
}