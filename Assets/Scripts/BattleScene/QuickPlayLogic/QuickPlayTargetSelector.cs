using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// 解析 CardAsset 目标类型，高亮可选目标，返回玩家选中的目标。
///
/// 无目标牌：向上拖超过阈值直接打出。
/// 有目标牌：Target 子对象跟随鼠标，线段从卡牌中心到 Target。
///
/// 模块/槽：运行时自动查找（动态生成，无法手填）。
/// 肖像：Inspector 直接填入引用（固定场景对象，曾是搜索 Bug 根源）。
/// </summary>
[RequireComponent(typeof(QuickPlayExecutor))]
public class QuickPlayTargetSelector : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    // 肖像直接引用（Inspector 填，只需填一次）
    // ═══════════════════════════════════════════════════
    [System.Serializable]
    public class PortraitRef
    {
        [Tooltip("射线命中的碰撞体 GameObject（Player Portrait 本体）")]
        public GameObject colliderObject;

        [Tooltip("Player Portrait 本体的 CardGlow Image（非悬浮时单独用，不随 Preview 移动）")]
        public UnityEngine.UI.Image bodyCardGlow;

        [Tooltip("Player Portrait 上的 HoverPreview 组件（悬浮时弹出预览 + Preview 层 Glow）")]
        public HoverPreview hoverPreview;
    }

    [Header("卡牌数据")]
    public CardAsset cardAsset;

    [Header("场景引用")]
    [SerializeField] public GameManager  gameManager;
    [SerializeField] private DeployZone   playerDeployZone;
    [SerializeField] private DeployZone   aiDeployZone;
    [SerializeField] private PlayerState  playerState;
    [SerializeField] private PlayerState  aiState;
    [SerializeField] private HandManager  playerHandManager;
    [SerializeField] private DeckManager  playerDeckManager;

    [Header("肖像直接引用（固定填一次，所有有目标卡牌共用）")]
    [SerializeField] private PortraitRef playerPortrait;
    [SerializeField] private PortraitRef aiPortrait;

    [Header("无目标牌：向上拖拽触发阈值（世界单位）")]
    [SerializeField] private float noTargetThreshold = 1.5f;
    public float NoTargetThreshold => noTargetThreshold;

    [Header("高亮脉冲")]
    [SerializeField] private float glowPulseMin   = 0.5f;
    [SerializeField] private float glowPulseSpeed = 1.2f;

    // ═══════════════════════════════════════════════════
    // 运行时状态
    // ═══════════════════════════════════════════════════
    private GameObject     _targetRoot;
    private DragTargetLine _dragLine;

    /// <summary>
    /// 由 DeckManager 在实例化后调用，注入所有场景引用。
    /// 用于 Prefab Variant 无法预填引用的情况。
    /// 已填的字段不会被覆盖（非空则跳过）。
    /// </summary>
    public void InjectSceneRefs(
        GameManager  gm,
        DeployZone   playerZone, DeployZone   aiZone,
        PlayerState  player,     PlayerState  ai,
        HandManager  hand,       DeckManager  deck)
    {
        if (gm          != null && gameManager        == null) gameManager        = gm;
        if (playerZone  != null && playerDeployZone   == null) playerDeployZone   = playerZone;
        if (aiZone      != null && aiDeployZone       == null) aiDeployZone       = aiZone;
        if (player      != null && playerState        == null) playerState        = player;
        if (ai          != null && aiState            == null) aiState            = ai;
        if (hand        != null && playerHandManager  == null) playerHandManager  = hand;
        if (deck        != null && playerDeckManager  == null) playerDeckManager  = deck;
    }

    public void SetTargetRoot(GameObject targetRoot)
    {
        _targetRoot = targetRoot;
        _dragLine   = targetRoot?.GetComponent<DragTargetLine>();
        if (_targetRoot != null) _targetRoot.SetActive(false);
    }
    private QuickPlayExecutor  _executor;
    private QuickPlayDraggable _draggable;
    private Vector3            _cardOrigin;

    private float _targetZDepth;

    private List<GameObject>  _highlightedModules  = new List<GameObject>();
    private List<GameObject>  _highlightedSlots    = new List<GameObject>();
    private List<PortraitRef> _highlightedPortraits = new List<PortraitRef>();

    private GameObject  _hoveredModule  = null;
    private PortraitRef _hoveredPortrait = null;

    private Dictionary<GameObject, UnityEngine.UI.Image> _moduleGlowCache
        = new Dictionary<GameObject, UnityEngine.UI.Image>();

    private HashSet<GameObject> _previewingModules = new HashSet<GameObject>();

    private const string PortraitGlowIdPrefix = "portraitbodyglow_";

    // ═══════════════════════════════════════════════════
    void Awake()
    {
        _executor  = GetComponent<QuickPlayExecutor>();
        _draggable = GetComponent<QuickPlayDraggable>();
    }

    private Vector3 MouseToWorld()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        Vector3 screen = Input.mousePosition;
        screen.z = _targetZDepth;
        return cam.ScreenToWorldPoint(screen);
    }

    // ═══════════════════════════════════════════════════
    // 拖拽回调
    // ═══════════════════════════════════════════════════

    public void OnDragStart()
    {
        _cardOrigin = transform.position;

        Camera cam = Camera.main;
        if (cam != null)
            _targetZDepth = cam.WorldToScreenPoint(_cardOrigin).z;

        if (NeedsTarget())
        {
            HighlightValidTargets();
            if (_targetRoot != null)
            {
                _targetRoot.transform.position = _cardOrigin;
                _targetRoot.SetActive(true);
            }
            _dragLine?.ShowLine(transform);
        }
    }

    public void OnDragging(Vector3 worldPos)
    {
        if (!NeedsTarget()) return;

        if (_targetRoot != null)
        {
            Vector3 mouseWorld = MouseToWorld();
            _targetRoot.transform.position = new Vector3(
                mouseWorld.x, mouseWorld.y, _targetRoot.transform.position.z);
        }

        GameObject  hitModule   = null;
        PortraitRef hitPortrait = null;
        RaycastHit(out hitModule, out hitPortrait);

        bool hitValid = (hitModule  != null && _highlightedModules.Contains(hitModule))
                     || (hitPortrait != null && _highlightedPortraits.Contains(hitPortrait));

        bool hoverChanged = hitModule != _hoveredModule || hitPortrait != _hoveredPortrait;

        if (hoverChanged)
        {
            if (_hoveredModule != null && _previewingModules.Contains(_hoveredModule))
                HideModulePreview(_hoveredModule);

            if (_hoveredPortrait != null)
            {
                if (_hoveredPortrait.hoverPreview != null)
                    _hoveredPortrait.hoverPreview.SkipSmallCardGlowOnPreview = false;
                PortraitGlowOff(_hoveredPortrait);
                if (_hoveredPortrait.hoverPreview?.previewCardGlow != null)
                {
                    _hoveredPortrait.hoverPreview.previewCardGlow.DOKill();
                    _hoveredPortrait.hoverPreview.previewCardGlow.enabled = false;
                }
                _hoveredPortrait.hoverPreview?.ForceHide();
                PortraitBodyGlowBreath(_hoveredPortrait);
            }

            _hoveredModule   = hitModule;
            _hoveredPortrait = hitPortrait;

            if (hitValid)
            {
                foreach (var m in _highlightedModules)
                {
                    if (m == hitModule) continue;
                    ModuleGlowDim(m);
                }

                foreach (var p in _highlightedPortraits)
                {
                    if (p == hitPortrait) continue;
                    PortraitBodyGlowDim(p);
                }

                _draggable?.SetValidTargetHover(true);

                if (hitModule != null)
                {
                    ShowModulePreview(hitModule, Color.green);
                    ModuleGlowBreathGreen(hitModule);
                }
                if (hitPortrait != null)
                {
                    if (hitPortrait.hoverPreview != null)
                        hitPortrait.hoverPreview.SkipSmallCardGlowOnPreview = true;

                    PortraitBodyGlowOff(hitPortrait);
                    hitPortrait.hoverPreview?.ForceShow(ignoreLock: true);

                    if (hitPortrait.hoverPreview != null)
                        hitPortrait.hoverPreview.SkipSmallCardGlowOnPreview = false;

                    // 悬停肖像时显示绿光
                    PortraitBodyGlowBreathGreen(hitPortrait);
                }
            }
            else
            {
                foreach (var m in _highlightedModules)
                    ModuleGlowBreath(m);
                foreach (var p in _highlightedPortraits)
                    PortraitBodyGlowBreath(p);
            }

            if (!hitValid)
            {
                _draggable?.SetValidTargetHover(false);
                var selfHp = _draggable?.GetComponent<HoverPreview>();
                if (selfHp != null && selfHp.previewGameObject != null
                    && !selfHp.previewGameObject.activeSelf)
                    selfHp.ForceShowNoGlow();
            }
        }
    }

    public bool OnDragEnd(Vector3 worldPos)
    {
        if (!NeedsTarget())
        {
            ClearHighlights();
            HideTargetIndicator();

            Vector3 mouseWorld = MouseToWorld();
            if (mouseWorld.y - _cardOrigin.y >= noTargetThreshold)
            {
                if (!IsTimingValid()) return false;
                Play(null);
                return true;
            }
            return false;
        }
        else
        {
            // 最终命中目标以拖拽期间缓存的悬停目标为准
            GameObject  cachedModule   = _hoveredModule;
            PortraitRef cachedPortrait = _hoveredPortrait;

            bool hitModuleValid   = cachedModule   != null && _highlightedModules.Contains(cachedModule);
            bool hitPortraitValid = cachedPortrait != null && _highlightedPortraits.Contains(cachedPortrait);

            if (hitModuleValid)   { Play(cachedModule);                  ClearHighlights(); HideTargetIndicator(); return true; }
            if (hitPortraitValid) { Play(cachedPortrait.colliderObject); ClearHighlights(); HideTargetIndicator(); return true; }
            ClearHighlights();
            HideTargetIndicator();
            return false;
        }
    }

    public void CancelTargeting()
    {
        ClearHighlights();
        HideTargetIndicator();
    }

    // ═══════════════════════════════════════════════════
    // 高亮
    // ═══════════════════════════════════════════════════

    private void HighlightValidTargets()
    {
        if (cardAsset == null) return;
        _highlightedModules.Clear();
        _highlightedSlots.Clear();
        _highlightedPortraits.Clear();
        _moduleGlowCache.Clear();
        _previewingModules.Clear();

        foreach (var e in cardAsset.Effects)
        {
            switch (e.Target)
            {
                case QuickPlayTarget.EnemyModule:
                    AddModules(aiDeployZone);     break;
                case QuickPlayTarget.YourModule:
                    AddModules(playerDeployZone); break;
                case QuickPlayTarget.AnyModule:
                    AddModules(playerDeployZone); AddModules(aiDeployZone); break;

                case QuickPlayTarget.EnemyObject:
                    AddModules(aiDeployZone);     AddPortrait(aiPortrait);     break;
                case QuickPlayTarget.YourObject:
                    AddModules(playerDeployZone); AddPortrait(playerPortrait); break;
                case QuickPlayTarget.AnyObject:
                    AddModules(playerDeployZone); AddModules(aiDeployZone);
                    AddPortrait(playerPortrait);  AddPortrait(aiPortrait);     break;

                case QuickPlayTarget.EnemySlot:
                    AddSlots(aiDeployZone,     requireEmpty: false); break;
                case QuickPlayTarget.YourSlot:
                    AddSlots(playerDeployZone, requireEmpty: false); break;
                case QuickPlayTarget.AnySlot:
                    AddSlots(playerDeployZone, requireEmpty: false);
                    AddSlots(aiDeployZone,     requireEmpty: false); break;
                case QuickPlayTarget.EnemyEmptySlot:
                    AddSlots(aiDeployZone,     requireEmpty: true);  break;
                case QuickPlayTarget.YourEmptySlot:
                    AddSlots(playerDeployZone, requireEmpty: true);  break;
            }
        }
    }

    private void AddModules(DeployZone zone)
    {
        if (zone == null) return;
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            GameObject slotObj = zone.GetSlot(i);
            if (slotObj == null) continue;
            DeploySlot slot = slotObj.GetComponent<DeploySlot>();
            if (slot?.OccupyingModuleInstance == null || !slot.OccupyingModuleInstance.IsAlive) continue;
            GameObject moduleGO = slot.OccupyingModuleInstance.gameObject;
            if (_highlightedModules.Contains(moduleGO)) continue;
            _highlightedModules.Add(moduleGO);
            ModuleGlowBreath(moduleGO);
        }
    }

    private void AddSlots(DeployZone zone, bool requireEmpty)
    {
        if (zone == null) return;
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            GameObject slotObj = zone.GetSlot(i);
            if (slotObj == null) continue;
            DeploySlot slot = slotObj.GetComponent<DeploySlot>();
            if (slot == null) continue;
            if (requireEmpty && slot.IsOccupied) continue;
            if (_highlightedSlots.Contains(slotObj)) continue;
            _highlightedSlots.Add(slotObj);
            ModuleGlowBreath(slotObj);
        }
    }

    private void AddPortrait(PortraitRef portrait)
    {
        if (portrait == null || portrait.colliderObject == null) return;
        if (_highlightedPortraits.Contains(portrait)) return;
        _highlightedPortraits.Add(portrait);
        PortraitBodyGlowBreath(portrait);
    }

    private void ClearHighlights()
    {
        var previewingCopy = new List<GameObject>(_previewingModules);
        foreach (var m in previewingCopy)
            HideModulePreview(m);
        _previewingModules.Clear();

        foreach (var m in _highlightedModules)
            ModuleGlowOff(m);
        foreach (var s in _highlightedSlots)
            ModuleGlowOff(s);
        foreach (var p in _highlightedPortraits)
        {
            if (p.hoverPreview != null)
                p.hoverPreview.SkipSmallCardGlowOnPreview = false;
            PortraitBodyGlowOff(p);
            p.hoverPreview?.StopPreviewGlow();
            p.hoverPreview?.ForceHide();
        }
        _highlightedModules.Clear();
        _highlightedSlots.Clear();
        _highlightedPortraits.Clear();
        _moduleGlowCache.Clear();
        _hoveredModule   = null;
        _hoveredPortrait = null;
    }

    // ═══════════════════════════════════════════════════
    // 模块 Glow
    // ═══════════════════════════════════════════════════

    private UnityEngine.UI.Image GetModuleGlow(GameObject obj)
    {
        if (obj == null) return null;
        if (_moduleGlowCache.TryGetValue(obj, out var cached) && cached != null) return cached;
        var t = obj.transform.Find("Canvas/CardGlow");
        if (t != null)
        {
            var img = t.GetComponent<UnityEngine.UI.Image>();
            if (img != null) { _moduleGlowCache[obj] = img; return img; }
        }
        return null;
    }

    private void ModuleGlowFull(GameObject obj, Color? color = null)
    {
        var img = GetModuleGlow(obj);
        if (img == null) return;
        img.DOKill();
        if (!img.enabled) { img.enabled = true; SetAlpha(img, 0f); }
        if (color.HasValue) img.color = new Color(color.Value.r, color.Value.g, color.Value.b, img.color.a);
        img.DOFade(1f, 0.1f);
    }

    private void ModuleGlowBreathGreen(GameObject obj)
    {
        if (obj == null) return;
        HoverPreview hp = obj.GetComponent<HoverPreview>();
        if (hp == null || hp.previewCardFaceGlow == null) return;
        var img = hp.previewCardFaceGlow;
        img.DOKill();
        img.enabled = true;
        img.color = new Color(0f, 1f, 0.3f, 0f);
        img.DOFade(1f, glowPulseSpeed * 0.3f).OnComplete(() =>
        {
            if (img != null && img.gameObject != null && img.gameObject.activeInHierarchy)
                img.DOFade(glowPulseMin, glowPulseSpeed).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        });
    }

    private void ModuleGlowBreath(GameObject obj)
    {
        var img = GetModuleGlow(obj);
        if (img == null) return;
        img.DOKill();
        if (!img.enabled)
        {
            img.enabled = true;
            SetAlpha(img, 0f);
        }
        img.color = new Color(0f, 0.922f, 1f, img.color.a);
        var captured = img;
        img.DOFade(1f, glowPulseSpeed * 0.3f).OnComplete(() =>
        {
            if (captured != null && captured.enabled
                && Mathf.Approximately(captured.color.r, 0f)
                && captured.color.b > 0.9f
                && captured.color.g > 0.8f)
                captured.DOFade(glowPulseMin, glowPulseSpeed).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        });
    }

    private void ModuleGlowDim(GameObject obj)
    {
        var img = GetModuleGlow(obj);
        if (img == null || !img.enabled) return;
        img.DOKill();
        img.DOFade(0.15f, 0.15f);
    }

    private void ModuleGlowOff(GameObject obj)
    {
        var img = GetModuleGlow(obj);
        if (img == null) return;
        img.DOKill();
        SetAlpha(img, 0f);
        img.enabled = false;
    }

    // ═══════════════════════════════════════════════════
    // 肖像 Glow — 用唯一 ID 管理，防止被外部 DOKill 误杀
    // ═══════════════════════════════════════════════════

    private static string PortraitGlowId(UnityEngine.UI.Image img)
        => PortraitGlowIdPrefix + img.GetInstanceID();

    private void PortraitBodyGlowBreath(PortraitRef p)
    {
        var img = p?.bodyCardGlow;
        if (img == null) return;

        string id = PortraitGlowId(img);
        DOTween.Kill(id);

        img.enabled = true;
        SetAlpha(img, 0f);
        img.color = new Color(0f, 0.922f, 1f, img.color.a);

        DOTween.To(() => img.color.a,
            a => { var c = img.color; c.a = a; img.color = c; },
            1f, glowPulseSpeed * 0.3f)
            .SetId(id)
            .OnComplete(() =>
            {
                if (img != null && img.enabled)
                    DOTween.To(() => img.color.a,
                        a => { var c = img.color; c.a = a; img.color = c; },
                        glowPulseMin, glowPulseSpeed)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetId(id);
            });
    }

    private void PortraitBodyGlowBreathGreen(PortraitRef p)
    {
        if (p?.hoverPreview?.previewCardGlow == null) return;
        var img = p.hoverPreview.previewCardGlow;
        img.DOKill();
        img.gameObject.SetActive(true);
        img.enabled = true;
        img.color = new Color(0f, 1f, 0.3f, 0f);
        img.DOFade(1f, glowPulseSpeed * 0.3f).OnComplete(() =>
        {
            if (img != null && img.enabled)
                img.DOFade(glowPulseMin, glowPulseSpeed).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        });
    }

    private void PortraitBodyGlowFull(PortraitRef p, Color? color = null)
    {
        var img = p?.bodyCardGlow;
        if (img == null) return;

        string id = PortraitGlowId(img);
        DOTween.Kill(id);

        img.enabled = true;
        if (color.HasValue)
            img.color = new Color(color.Value.r, color.Value.g, color.Value.b, img.color.a);

        DOTween.To(() => img.color.a,
            a => { var c = img.color; c.a = a; img.color = c; },
            1f, 0.1f)
            .SetId(id);
    }

    private void PortraitBodyGlowDim(PortraitRef p)
    {
        var img = p?.bodyCardGlow;
        if (img == null) return;

        string id = PortraitGlowId(img);
        DOTween.Kill(id);

        img.enabled = true;
        DOTween.To(() => img.color.a,
            a => { var c = img.color; c.a = a; img.color = c; },
            0.15f, 0.15f)
            .SetId(id);
    }

    private void PortraitBodyGlowOff(PortraitRef p)
    {
        var img = p?.bodyCardGlow;
        if (img == null) return;

        string id = PortraitGlowId(img);
        DOTween.Kill(id);

        SetAlpha(img, 0f);
        img.enabled = false;
    }

    private void PortraitGlowFull(PortraitRef p)   => p?.hoverPreview?.PreviewGlowFull();
    private void PortraitGlowBreath(PortraitRef p) => p?.hoverPreview?.PreviewGlowBreath();
    private void PortraitGlowDim(PortraitRef p)    => p?.hoverPreview?.PreviewGlowDim();
    private void PortraitGlowOff(PortraitRef p)    => p?.hoverPreview?.StopPreviewGlow();

    // ═══════════════════════════════════════════════════
    // Raycast
    // ═══════════════════════════════════════════════════

    private void RaycastHit(out GameObject hitModule, out PortraitRef hitPortrait)
    {
        hitModule   = null;
        hitPortrait = null;

        Camera cam = Camera.main;
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            Transform hitT = h.collider != null ? h.collider.transform : null;
            if (hitT == null) continue;
            if (hitT == transform || hitT.IsChildOf(transform)) continue;

            if (playerPortrait?.colliderObject != null)
            {
                Transform pt = playerPortrait.colliderObject.transform;
                if (hitT == pt || hitT.IsChildOf(pt)) { hitPortrait = playerPortrait; return; }
            }
            if (aiPortrait?.colliderObject != null)
            {
                Transform at = aiPortrait.colliderObject.transform;
                if (hitT == at || hitT.IsChildOf(at)) { hitPortrait = aiPortrait; return; }
            }

            foreach (var m in _highlightedModules)
            {
                if (m == null) continue;
                if (hitT == m.transform || hitT.IsChildOf(m.transform)) { hitModule = m; return; }
            }
            foreach (var s in _highlightedSlots)
            {
                if (s == null) continue;
                if (hitT == s.transform || hitT.IsChildOf(s.transform)) { hitModule = s; return; }
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 辅助
    // ═══════════════════════════════════════════════════

    private static void SetAlpha(UnityEngine.UI.Image img, float a)
    {
        Color c = img.color; c.a = a; img.color = c;
    }

    private void HideTargetIndicator()
    {
        _dragLine?.HideLine();
        if (_targetRoot != null)
        {
            _targetRoot.transform.position = new Vector3(
                _cardOrigin.x, _cardOrigin.y,
                _targetRoot.transform.position.z);
            _targetRoot.SetActive(false);
        }
    }

    private void Play(GameObject target)
    {
        if (cardAsset == null) return;
        if (!IsTimingValid())
        {
            Debug.LogWarning($"[QuickPlay] {cardAsset.GetDisplayName()} 当前时机不可使用");
            return;
        }
        _executor.Execute(cardAsset, target, playerState, aiState,
                          playerDeployZone, aiDeployZone, playerHandManager,
                          playerDeckManager);
        playerHandManager?.RemoveCard(gameObject);
    }

    public bool NeedsTarget()
    {
        if (cardAsset == null) return false;
        return cardAsset.RequiresTarget;
    }

    public bool IsPlayableAt(TurnTiming timing, GameManager.TurnPhase phase)
    {
        if (cardAsset == null) return false;
        if (cardAsset.UsableTiming == QuickPlayTiming.AnyTime) return true;
        switch (cardAsset.UsableTiming)
        {
            case QuickPlayTiming.BeforeDraw:    return timing == TurnTiming.TurnStart;
            case QuickPlayTiming.AfterDraw:     return timing == TurnTiming.AfterDraw;
            case QuickPlayTiming.AfterDeploy:   return timing == TurnTiming.BeforeBattle;
            case QuickPlayTiming.BeforeFlip:    return timing == TurnTiming.BeforeSlot;
            case QuickPlayTiming.AfterFlip:     return timing == TurnTiming.AfterSlot;
            case QuickPlayTiming.BeforeEffect:  return timing == TurnTiming.BeforeEffect;
            case QuickPlayTiming.AfterEffect:   return timing == TurnTiming.AfterEffect;
            case QuickPlayTiming.AfterBattle:   return timing == TurnTiming.AfterBattle;
            case QuickPlayTiming.BeforeMissile: return timing == TurnTiming.BeforeMissile;
            case QuickPlayTiming.AfterMissile:  return timing == TurnTiming.AfterMissile;
            case QuickPlayTiming.TurnEnd:       return timing == TurnTiming.TurnEnd;

            case QuickPlayTiming.DuringBattle:
                return phase == GameManager.TurnPhase.Combat;
            case QuickPlayTiming.DuringMissile:
                return phase == GameManager.TurnPhase.MissilePhase;
            case QuickPlayTiming.DuringBattleAndMissile:
                return phase == GameManager.TurnPhase.Combat
                    || phase == GameManager.TurnPhase.MissilePhase;
            case QuickPlayTiming.DuringDrawAndBattle:
                return phase == GameManager.TurnPhase.DrawAndDeploy
                    || phase == GameManager.TurnPhase.Combat;
            case QuickPlayTiming.AfterDrawPhase:
                return phase == GameManager.TurnPhase.Combat
                    || phase == GameManager.TurnPhase.MissilePhase
                    || phase == GameManager.TurnPhase.TurnEnd;

            default: return true;
        }
    }

    public bool IsTimingValid()
    {
        if (gameManager == null) return true;
        return IsPlayableAt(gameManager.CurrentTiming, gameManager.CurrentPhase);
    }

    // ═══════════════════════════════════════════════════
    // 模块目标预览
    // ═══════════════════════════════════════════════════

    private void ShowModulePreview(GameObject moduleGO, Color glowColor)
    {
        if (moduleGO == null) return;
        HoverPreview hp = moduleGO.GetComponent<HoverPreview>();
        if (hp == null || hp.previewGameObject == null) return;

        Transform previewT = hp.previewGameObject.transform;

        previewT.DOKill();
        previewT.localPosition = Vector3.zero;
        previewT.localScale    = Vector3.one;
        previewT.localRotation = Quaternion.identity;

        hp.previewGameObject.SetActive(true);

        previewT.DOScale(hp.TargetScale, hp.AnimationDuration).SetEase(Ease.OutBack);
        if (hp.TargetPosition != Vector3.zero)
            previewT.DOLocalMove(hp.TargetPosition, hp.AnimationDuration).SetEase(hp.MoveEase);

        _previewingModules.Add(moduleGO);
    }

    private void HideModulePreview(GameObject moduleGO)
    {
        if (moduleGO == null) return;

        _previewingModules.Remove(moduleGO);

        HoverPreview hp = moduleGO.GetComponent<HoverPreview>();
        if (hp == null || hp.previewGameObject == null) return;

        Transform previewT = hp.previewGameObject.transform;

        previewT.DOKill();

        if (!hp.previewGameObject.activeSelf) return;

        if (hp.TurnThisOffWhenPreviewing != null)
            hp.TurnThisOffWhenPreviewing.SetActive(true);

        Sequence seq = DOTween.Sequence();
        seq.Join(previewT.DOScale(1f, hp.AnimationDuration).SetEase(hp.MoveEase));
        seq.Join(previewT.DOLocalMove(Vector3.zero, hp.AnimationDuration).SetEase(hp.MoveEase));
        seq.OnComplete(() =>
        {
            if (hp != null && hp.previewGameObject != null)
                hp.previewGameObject.SetActive(false);
        });

        if (hp.previewCardFaceGlow != null)
        {
            hp.previewCardFaceGlow.DOKill();
            hp.previewCardFaceGlow.enabled = false;
        }
    }
}