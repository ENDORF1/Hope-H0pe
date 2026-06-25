using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 牌库管理脚本
/// 职责：持有卡牌列表、序列抽牌动画、分发到部署区或手牌区
/// </summary>
public class DeckManager : MonoBehaviour
{
    [Header("牌库卡牌列表")]
    [SerializeField] private List<CardAsset> initialDeck = new List<CardAsset>();

    [Header("视觉：整个 DeckCardBack 的 Transform（控制Z值）")]
    [SerializeField] private Transform deckCardBack;

    [Header("视觉：牌库抽光时隐藏的对象（立方体+卡背图，不含 DeckManager 本身）")]
    [SerializeField] private GameObject[] deckVisualObjects;  // 拖入立方体和卡背图等子对象

    [Header("视觉：Z 值范围")]
    [SerializeField] private float zFull  = -1f;   // 满牌库时的 Z
    [SerializeField] private float zEmpty =  0f;   // 牌摸完时的 Z

    [Header("分发引用")]
    [SerializeField] private HandManager handManager;
    [SerializeField] private DeployZone  deployZone;
    [SerializeField] private GameObject  quickPlayTargetRoot; // 拖入场景中的 Target 对象（速攻牌目标指示器）

    [Header("判负引用")]
    [Tooltip("拖入该牌库归属的 PlayerState。玩家的 DeckManager 填玩家的 PlayerState，AI 的 DeckManager 填 AI 的 PlayerState。牌库抽空时自动触发该玩家判负。")]
    [SerializeField] private PlayerState ownerState;

    [Header("手牌高亮")]
    [Tooltip("拖入场景中的 GameManager。每张牌落入手牌后立即刷新手牌高亮状态。")]
    [SerializeField] private GameManager gameManager;

    [Header("速攻牌场景引用注入（仅玩家DeckManager填，Prefab Variant用）")]
    [SerializeField] private DeployZone  injectPlayerZone;
    [SerializeField] private DeployZone  injectEnemyZone;
    [SerializeField] private PlayerState injectCasterState;
    [SerializeField] private PlayerState injectEnemyState;
    [SerializeField] private HandManager injectCasterHand;
    [SerializeField] private DeckManager injectCasterDeck;

    [System.Serializable]
    public class CardPrefabOverride
    {
        [Tooltip("需要特殊Prefab的CardAsset")]
        public CardAsset Card;
        [Tooltip("对应的Prefab Variant")]
        public GameObject Prefab;
    }

    [Header("Prefab")]
    [SerializeField] private GameObject modulePrefab;
    [SerializeField] private GameObject quickPlayPrefab;

    [Header("特殊卡牌Prefab覆写（天允终偿等）")]
    [SerializeField] private List<CardPrefabOverride> prefabOverrides = new List<CardPrefabOverride>();

    [Header("动画：暂停区")]
    [SerializeField] private Transform stagingArea;        // 卡牌短暂停留的位置
    [SerializeField] private float     drawDuration   = 0.3f;  // 从牌库飞到暂停区的时间
    [SerializeField] private float     stagingWait    = 0.5f;  // 在暂停区停留时间
    [SerializeField] private float     flyOutDuration = 0.4f;  // 从暂停区飞出的时间
    [SerializeField] private float     flipDuration   = 0.35f; // 翻转动画时间（ScaleX 压扁+展开各半）
    [SerializeField] private float     flipDelay      = 0.15f; // 卡牌落槽后延迟多久开始翻转

    // 运行时牌堆
    private List<CardAsset> cards = new List<CardAsset>();
    public int  CardCount => cards.Count;
    public bool IsEmpty   => cards.Count == 0;
    private int fullCount;

    private bool isDrawing = false; // 防止重复抽牌
    public bool IsDrawing => isDrawing;

    // ─────────────────────────────────────────────────
    void Start()
    {
        cards.Clear();
        cards.AddRange(initialDeck);
        fullCount = Mathf.Max(cards.Count, 1);
        Shuffle();

        // 初始化时 Z 设为 zEmpty，等开场动画推到 zFull
        if (deckCardBack != null)
        {
            Vector3 pos = deckCardBack.localPosition;
            deckCardBack.localPosition = new Vector3(pos.x, pos.y, zEmpty);
        }

        // 确保视觉对象可见（卡背、Cube 等始终显示，Z 值控制厚度感）
        if (deckVisualObjects != null)
            foreach (var obj in deckVisualObjects)
                if (obj != null) obj.SetActive(true);
    }

    // ─────────────────────────────────────────────────
    // 公开入口：序列抽 count 张
    // ─────────────────────────────────────────────────
    public void DrawAndDistribute(int count = 5)
    {
        if (isDrawing) return;
        StartCoroutine(DrawSequence(count));
    }

    // ─────────────────────────────────────────────────
    // 序列抽牌协程：一张抽完飞出后再抽下一张
    // ─────────────────────────────────────────────────
    private IEnumerator DrawSequence(int count)
    {
        isDrawing = true;

        for (int i = 0; i < count; i++)
        {
            if (IsEmpty) break; // 牌库空了就停止，判负逻辑由调用方负责

            // 任何牌：手牌已满则停止抽牌，不消耗牌库
            bool nextIsModule = cards[0].CardType == CardType.Module;
            bool deployZoneHasSpace = nextIsModule && deployZone != null && deployZone.GetNextEmptySlot() != null;
            if (!deployZoneHasSpace && handManager != null && handManager.GetNextEmptySlot() == null)
            {
                Debug.Log("[DeckManager] 手牌已满，停止抽牌。");
                break;
            }

            CardAsset asset = PopOne();
            if (asset == null) break;

            // 生成卡牌（卡背朝上，从牌库位置出发）
            bool isModule = asset.CardType == CardType.Module;
            // 速攻牌：先查覆写列表，找不到再用默认
            GameObject prefab;
            if (isModule)
            {
                prefab = modulePrefab;
            }
            else
            {
                prefab = quickPlayPrefab;
                foreach (var ov in prefabOverrides)
                {
                    if (ov.Card == asset && ov.Prefab != null)
                    {
                        prefab = ov.Prefab;
                        break;
                    }
                }
            }
            if (prefab == null)
            {
                string cardTypeName = isModule ? "模块" : "速攻";
                Debug.LogError($"DeckManager: {cardTypeName}牌 Prefab 未赋值！");
                continue;
            }

            GameObject cardObj = Instantiate(prefab, deckCardBack.position, deckCardBack.rotation);
            cardObj.name = $"Drawing_{asset.GetDisplayName()}";
            cardObj.SetActive(true);

            // 实例化后立刻压制所有 Glow，防止 Awake/OnEnable 期间被点亮
            foreach (var img in cardObj.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (!img.gameObject.name.Contains("Glow")) continue;
                DG.Tweening.DOTween.Kill(img);
                Color ic = img.color; ic.a = 0f; img.color = ic;
                img.enabled = false;
            }

            // 写入数据
            OneCardManager ocm = cardObj.GetComponent<OneCardManager>();
            if (ocm != null)
            {
                ocm.cardAsset = asset;
                ocm.ReadCardFromAsset();
            }

            // 根据 CardAsset.ModuleScriptName 动态挂载自定义效果脚本
            if (!string.IsNullOrEmpty(asset.ModuleScriptName))
            {
                System.Type scriptType = System.Type.GetType(asset.ModuleScriptName);
                if (scriptType != null && typeof(MonoBehaviour).IsAssignableFrom(scriptType))
                    cardObj.AddComponent(scriptType);
                else
                    Debug.LogWarning($"[DeckManager] ModuleScriptName '{asset.ModuleScriptName}' 找不到对应脚本类型");
            }

            // 卡背朝上
            BetterCardRotation rot = cardObj.GetComponent<BetterCardRotation>();
            if (rot != null) rot.ShowBack();

            // 等待这张牌完成整个流程（飞到暂停区→等待→飞出）
            yield return StartCoroutine(ProcessCard(cardObj, asset, isModule));
        }

        isDrawing = false;
    }

    // ─────────────────────────────────────────────────
    // 单张卡处理：飞到暂停区 → 等待 → 飞到目标
    // ─────────────────────────────────────────────────
    protected virtual IEnumerator ProcessCard(GameObject cardObj, CardAsset asset, bool isModule)
    {
        // 1. 从牌库飞到暂停区
        if (stagingArea != null)
        {
            yield return cardObj.transform
                .DOMove(stagingArea.position, drawDuration)
                .SetEase(Ease.OutQuart)
                .WaitForCompletion();

            // 对齐旋转
            yield return cardObj.transform
                .DORotate(stagingArea.eulerAngles, drawDuration * 0.5f)
                .WaitForCompletion();
        }

        // 2. 在暂停区停留
        yield return new WaitForSeconds(stagingWait);

        // 3. 飞到目标位置
        if (isModule)
            yield return StartCoroutine(FlyToDeployZone(cardObj, asset));
        else
            yield return StartCoroutine(FlyToHand(cardObj, asset));
    }

    // ─────────────────────────────────────────────────
    // 飞入部署区；部署区满时转入手牌区（卡背朝上暂存）
    // ─────────────────────────────────────────────────
    private IEnumerator FlyToDeployZone(GameObject cardObj, CardAsset asset)
    {
        if (deployZone == null)
        {
            Debug.LogError("DeckManager: deployZone 未赋值！");
            Destroy(cardObj);
            yield break;
        }

        DeploySlot targetSlot = deployZone.GetNextEmptySlot();

        // 部署区满：飞入手牌区卡背朝上暂存，下回合有空位时自动填入
        if (targetSlot == null)
        {
            Debug.LogWarning("DeckManager: 部署区已满，模块牌转入手牌区暂存。");
            if (handManager == null)
            {
                Debug.LogError("DeckManager: handManager 未赋值，无法暂存溢出模块牌！");
                Destroy(cardObj);
                yield break;
            }

            Transform overflowSlot = handManager.GetNextEmptySlot();
            if (overflowSlot == null)
            {
                Debug.LogWarning("DeckManager: 手牌区也已满，溢出模块牌丢弃。");
                Destroy(cardObj);
                yield break;
            }

            yield return cardObj.transform
                .DOMove(overflowSlot.position, flyOutDuration)
                .SetEase(Ease.InOutQuart)
                .WaitForCompletion();

            cardObj.transform.rotation = overflowSlot.rotation;

            // 卡背朝上，不翻转
            BetterCardRotation rot = cardObj.GetComponent<BetterCardRotation>();
            if (rot != null) rot.ShowBack();

            handManager.RegisterOverflowModule(cardObj);
            yield break;
        }

        yield return cardObj.transform
            .DOMove(targetSlot.transform.position, flyOutDuration)
            .SetEase(Ease.InOutQuart)
            .WaitForCompletion();

        targetSlot.PlaceModule(cardObj, startFaceDown: true);
    }

    // ─────────────────────────────────────────────────
    // 飞入手牌区，落槽后播放翻转动画（ScaleX 压扁→切面→展开）
    // ─────────────────────────────────────────────────
    private IEnumerator FlyToHand(GameObject cardObj, CardAsset asset)
    {
        if (handManager == null)
        {
            Debug.LogError("DeckManager: handManager 未赋值！");
            Destroy(cardObj);
            yield break;
        }

        Transform targetSlot = handManager.GetNextEmptySlot();
        if (targetSlot == null)
        {
            // 正常不应到达这里（DrawSequence 已提前检查），作为兜底
            Debug.LogWarning("DeckManager: FlyToHand 兜底触发，手牌已满，卡牌放回牌库顶。");
            AddToTop(cardObj.GetComponent<OneCardManager>()?.cardAsset);
            Destroy(cardObj);
            yield break;
        }

        // 1. 飞到槽位（卡背朝上）
        yield return cardObj.transform
            .DOMove(targetSlot.position, flyOutDuration)
            .SetEase(Ease.InOutQuart)
            .WaitForCompletion();

        cardObj.transform.rotation = targetSlot.rotation;

        // 2. 注册到 HandManager
        handManager.RegisterCard(cardObj, asset);

        // 3. 短暂停顿，让卡牌"落定"
        yield return new WaitForSeconds(flipDelay);

        // 4. ScaleX 翻转动画：压扁 → 切换卡面 → 展开
        BetterCardRotation rot = cardObj.GetComponent<BetterCardRotation>();
        if (rot != null)
        {
            rot.PrepareFlip();   // 禁用 HoverPreview，forceOverride=true

            float half = flipDuration * 0.5f;

            // 记录原始 scale，动画结束后精确还原（prefab scale 不一定是 1）
            Vector3 originalScale = cardObj.transform.localScale;

            // 前半段：X 轴压扁到 0（卡背消失）
            yield return cardObj.transform
                .DOScaleX(0f, half)
                .SetEase(Ease.InQuart)
                .WaitForCompletion();

            // 切换到卡面（此时 ScaleX=0，画面不可见，切换无闪烁）
            rot.ShowFront();

            // 后半段：X 轴从 0 展开回原始 X（卡面出现）
            yield return cardObj.transform
                .DOScaleX(originalScale.x, half)
                .SetEase(Ease.OutBack)
                .WaitForCompletion();

            // 精确还原完整 scale，消除 OutBack 过冲残留
            cardObj.transform.localScale = originalScale;

            // 恢复自动检测，归还预览对象
            rot.FlipToFrontAndResume();
            rot.RestorePreviewParent();
        }
        else
        {
            // 没有 BetterCardRotation 时直接切换（兜底）
            yield return new WaitForSeconds(flipDuration);
        }

        // 赋值 cardAsset 到 QuickPlayTargetSelector，并注入所有场景引用
        QuickPlayTargetSelector selector = cardObj.GetComponent<QuickPlayTargetSelector>();
        if (selector != null)
        {
            selector.cardAsset = asset;
            if (quickPlayTargetRoot != null)
                selector.SetTargetRoot(quickPlayTargetRoot);
            // 注入场景引用（Prefab Variant 无法预填，统一由 DeckManager 代码注入）
            // 只在有注入配置时才执行（AI DeckManager 不需要注入）
            if (injectPlayerZone != null || injectEnemyZone != null
                || injectCasterState != null || injectEnemyState != null)
            {
                selector.InjectSceneRefs(
                    gameManager,
                    injectPlayerZone,   injectEnemyZone,
                    injectCasterState,  injectEnemyState,
                    injectCasterHand ?? handManager,
                    injectCasterDeck ?? this);
            }
        }

        // 翻面完成后立即刷新手牌高亮（包括天允终偿抽上来的牌）
        gameManager?.RefreshHandHighlight();
    }

    // ─────────────────────────────────────────────────
    // 往牌库塞牌
    // ─────────────────────────────────────────────────
    public void AddToTop(CardAsset card)    { if (card != null) { cards.Insert(0, card); RefreshVisual(); } }
    public void AddToBottom(CardAsset card) { if (card != null) { cards.Add(card);       RefreshVisual(); } }
    public void AddRandom(CardAsset card)   { if (card != null) { cards.Insert(Random.Range(0, cards.Count + 1), card); RefreshVisual(); } }

    /// <summary>
    /// 永久摧毁牌库顶 N 张牌（万物归墟副作用）。
    /// 直接从列表移除，不进入弃牌堆。
    /// </summary>
    public void DestroyTopCards(int count)
    {
        int actual = Mathf.Min(count, cards.Count);
        if (actual <= 0) return;
        cards.RemoveRange(0, actual);
        RefreshVisual();
        Debug.Log($"[DeckManager] 永久摧毁牌库顶 {actual} 张牌，剩余：{cards.Count}");
    }

    public void Shuffle()
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    // ─────────────────────────────────────────────────
    private CardAsset PopOne()
    {
        if (IsEmpty) return null;
        CardAsset card = cards[0];
        cards.RemoveAt(0);
        RefreshVisual();
        return card;
    }

    // ─────────────────────────────────────────────────
    // 溢出模块填入：每回合抽牌阶段开始前调用
    // 从左到右遍历部署区空格，将手牌中的溢出模块依次飞入
    // ─────────────────────────────────────────────────
    public IEnumerator DeployOverflowModules()
    {
        if (handManager == null || !handManager.HasOverflowModules) yield break;
        if (deployZone == null) yield break;

        // 遍历溢出列表，有空格就填，没有就留着
        var overflow = new List<GameObject>(handManager.PeekOverflowModules());
        Debug.Log($"[DeckManager] 检查 {overflow.Count} 张溢出模块牌，尝试填入空位");

        foreach (GameObject cardObj in overflow)
        {
            if (cardObj == null) { handManager.RemoveOverflowModule(cardObj); continue; }

            DeploySlot targetSlot = deployZone.GetNextEmptySlot();
            if (targetSlot == null)
            {
                Debug.Log("[DeckManager] 部署区无空位，剩余溢出模块牌继续暂存。");
                yield break; // 后续的也放不下，直接停止
            }

            handManager.RemoveOverflowModule(cardObj);
            handManager.RearrangeHand();

            yield return cardObj.transform
                .DOMove(targetSlot.transform.position, flyOutDuration)
                .SetEase(Ease.InOutQuart)
                .WaitForCompletion();

            targetSlot.PlaceModule(cardObj, startFaceDown: true);
            Debug.Log($"[DeckManager] 溢出模块牌填入格子 {targetSlot.SlotIndex}");

            yield return new WaitForSeconds(0.1f);
        }
    }

    // 供子类（AIDeckManager）访问
    protected Transform StagingArea  => stagingArea;
    protected float     DrawDuration => drawDuration;
    protected float     StagingWait  => stagingWait;

    // ─────────────────────────────────────────────────
    // 开场动画：模拟卡牌不断叠入
    // ─────────────────────────────────────────────────

    [Header("开场填充动画")]
    [SerializeField] private float fillAnimDuration  = 1.2f;  // 整体动画时长
    [SerializeField] private float fillAnimBounce    = 0.08f; // 每次弹跳的 Z 偏移量
    [SerializeField] private int   fillAnimSteps     = 8;     // 弹跳次数（模拟叠入的卡数感）

    /// <summary>
    /// 开场时播放卡组"卡牌不断叠入"动画。
    /// 从 zEmpty 分步跳到 zFull，每步带一个微小弹跳。
    /// 由 GameManager 在肖像翻面时同步启动。
    /// </summary>
    public IEnumerator PlayFillAnimation()
    {
        if (deckCardBack == null) yield break;

        // 从 zEmpty 出发
        Vector3 pos = deckCardBack.localPosition;
        deckCardBack.localPosition = new Vector3(pos.x, pos.y, zEmpty);

        float stepDuration = fillAnimDuration / fillAnimSteps;

        for (int i = 0; i < fillAnimSteps; i++)
        {
            float t       = (float)(i + 1) / fillAnimSteps;
            float targetZ = Mathf.Lerp(zEmpty, zFull, t);

            // 先超冲一点再落回目标 Z，模拟卡牌落入时的弹跳感
            float overZ = targetZ - fillAnimBounce;
            Sequence seq = DOTween.Sequence();
            seq.Append(deckCardBack.DOLocalMoveZ(overZ,   stepDuration * 0.4f).SetEase(Ease.OutQuad));
            seq.Append(deckCardBack.DOLocalMoveZ(targetZ, stepDuration * 0.6f).SetEase(Ease.InQuad));
            yield return seq.WaitForCompletion();
        }

        // 最终精确对齐到满牌库 Z 值
        pos = deckCardBack.localPosition;
        deckCardBack.localPosition = new Vector3(pos.x, pos.y, zFull);
    }

    // ─────────────────────────────────────────────────
    // 牌库操作
    // ─────────────────────────────────────────────────
    // 视觉：控制 DeckCardBack 的 Z 值，抽光时隐藏视觉对象
    // ─────────────────────────────────────────────────
    private void RefreshVisual()
    {
        if (deckCardBack == null) return;

        if (IsEmpty)
        {
            // 只隐藏视觉对象，不隐藏 DeckManager 自身
            if (deckVisualObjects != null)
                foreach (var obj in deckVisualObjects)
                    if (obj != null) obj.SetActive(false);
            return;
        }

        // 确保视觉对象可见
        if (deckVisualObjects != null)
            foreach (var obj in deckVisualObjects)
                if (obj != null) obj.SetActive(true);

        float t = (float)cards.Count / fullCount;
        float z = Mathf.Lerp(zEmpty, zFull, t);
        Vector3 pos = deckCardBack.localPosition;
        deckCardBack.localPosition = new Vector3(pos.x, pos.y, z);
    }
}