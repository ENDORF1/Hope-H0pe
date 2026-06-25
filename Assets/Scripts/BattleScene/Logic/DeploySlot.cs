using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 部署格逻辑脚本
/// 职责：管理单个格子的状态（是否占用、持有哪个模块、属于哪个玩家）
/// 不负责视觉排列（由 DeployZone 管理）
/// 不负责战斗结算（由战斗引擎读取）
/// </summary>
public class DeploySlot : MonoBehaviour
{
    // ── 归属 ──────────────────────────────────────────────
    /// <summary>该格子属于哪个玩家（Player 类实现后取消注释）</summary>
    // public Player Owner { get; private set; }

    /// <summary>该格子属于哪个玩家（用于初始化 ModuleInstance）</summary>
    public PlayerState Owner { get; private set; }

    /// <summary>该格子在部署区中的索引（由 DeployZone 在生成时写入）</summary>
    public int SlotIndex { get; private set; }

    /// <summary>当前格子上的 ModuleInstance（运行时数据）</summary>
    public ModuleInstance OccupyingModuleInstance { get; private set; }

    // ── 占位：之后替换为 ModuleBase ───────────────────────
    /// <summary>当前格子上的模块（占位用 GameObject，之后换成 ModuleBase）</summary>
    public GameObject OccupyingModule { get; private set; }

    /// <summary>该格子是否已被占用</summary>
    public bool IsOccupied => OccupyingModule != null;

    // ── 盖伏状态 ──────────────────────────────────────────
    /// <summary>
    /// 该格子上的模块是否处于盖伏（face-down）状态
    /// 只有格子被占用时此值才有意义
    /// </summary>
    public bool IsFaceDown { get; private set; }

    // ── 视觉 ──────────────────────────────────────────────
    [Header("格子颜色")]
    [SerializeField] private Color emptyColor    = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color occupiedColor = new Color(0.3f, 0.7f, 1.0f, 1f);

    /// <summary>用于着色的 Image 组件，自动从自身获取，也可在 Inspector 手动指定</summary>
    [SerializeField] private Image slotImage;

    private void Awake()
    {
        if (slotImage == null)
            slotImage = GetComponent<Image>();

        if (slotImage == null)
            Debug.LogWarning($"DeploySlot: 未找到 Image 组件，无法显示格子颜色。请在 Slot Prefab 上挂载 Image。");
    }

    // ─────────────────────────────────────────────────────
    // 初始化
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 由 DeployZone 在生成格子后立即调用
    /// </summary>
    public void Initialize(int slotIndex, PlayerState owner = null)
    {
        SlotIndex       = slotIndex;
        Owner           = owner;
        OccupyingModule = null;
        OccupyingModuleInstance = null;
        IsFaceDown      = false;
        RefreshVisual();
    }

    /// <summary>根据当前占用状态刷新格子颜色</summary>
    private void RefreshVisual()
    {
        if (slotImage == null) return;
        slotImage.color = IsOccupied ? occupiedColor : emptyColor;
    }

    // ─────────────────────────────────────────────────────
    // 模块的放置与移除
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 将模块放入该格子。新部署的模块默认为盖伏状态。
    /// 返回 false 表示格子已被占用，放置失败。
    /// </summary>
    public bool PlaceModule(GameObject module, bool startFaceDown = true)
    {
        if (IsOccupied)
        {
            Debug.LogWarning($"DeploySlot [{SlotIndex}]: 放置失败，格子已被占用。");
            return false;
        }

        OccupyingModule = module;
        IsFaceDown      = startFaceDown;

        // 初始化 ModuleInstance（如果模块上挂有该组件）
        ModuleInstance mi = module.GetComponent<ModuleInstance>();
        if (mi != null)
        {
            CardAsset asset = module.GetComponent<OneCardManager>()?.cardAsset;
            if (asset != null)
                mi.Initialize(asset, Owner, SlotIndex);
            OccupyingModuleInstance = mi;

            // 监听模块销毁事件，自动清空格子
            mi.OnDestroyed += OnModuleDestroyed;

            // 通知 Bridge 刷新初始 UI
            ModuleRuntimeBridge bridge = module.GetComponent<ModuleRuntimeBridge>();
            if (bridge != null) bridge.OnInitialized();
        }

        RefreshVisual();
        return true;
    }

    /// <summary>
    /// 将模块从该格子移除（模块被摧毁时调用）。
    /// 不负责销毁 GameObject，只清空引用。
    /// </summary>
    public void RemoveModule()
    {
        if (!IsOccupied)
        {
            Debug.LogWarning($"DeploySlot [{SlotIndex}]: 尝试移除模块，但格子本来就是空的。");
            return;
        }

        // 取消事件监听
        if (OccupyingModuleInstance != null)
            OccupyingModuleInstance.OnDestroyed -= OnModuleDestroyed;

        OccupyingModule         = null;
        OccupyingModuleInstance = null;
        IsFaceDown              = false;
        RefreshVisual();
    }

    /// <summary>模块被摧毁时自动清空格子</summary>
    private void OnModuleDestroyed(ModuleInstance mi)
    {
        OccupyingModule         = null;
        OccupyingModuleInstance = null;
        IsFaceDown              = false;
        mi.OnDestroyed         -= OnModuleDestroyed;
        RefreshVisual();
        Debug.Log($"DeploySlot [{SlotIndex}]: 模块被摧毁，格子已清空。");
    }

    // ─────────────────────────────────────────────────────
    // 翻开 / 盖伏
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 翻开该格子上的模块。
    /// 返回 false 表示格子为空或模块已经是翻开状态。
    /// </summary>
    public bool FlipFaceUp()
    {
        if (!IsOccupied)
        {
            Debug.LogWarning($"DeploySlot [{SlotIndex}]: 翻开失败，格子为空。");
            return false;
        }
        if (!IsFaceDown)
        {
            Debug.LogWarning($"DeploySlot [{SlotIndex}]: 翻开失败，模块已经是翻开状态。");
            return false;
        }

        IsFaceDown = false;

        // 通知卡牌翻面
        BetterCardRotation rot = OccupyingModule?.GetComponentInChildren<BetterCardRotation>(true);
        if (rot != null) rot.FlipWithAnimation(true);

        return true;
    }
    /// 返回 false 表示格子为空或模块已经是盖伏状态。
    /// </summary>
    public bool FlipFaceDown()
    {
        if (!IsOccupied)
        {
            Debug.LogWarning($"DeploySlot [{SlotIndex}]: 盖伏失败，格子为空。");
            return false;
        }
        if (IsFaceDown)
        {
            Debug.LogWarning($"DeploySlot [{SlotIndex}]: 盖伏失败，模块已经是盖伏状态。");
            return false;
        }

        IsFaceDown = true;

        // 通知卡牌翻回卡背
        BetterCardRotation rot = OccupyingModule?.GetComponent<BetterCardRotation>();
        if (rot != null) rot.ShowBack();

        return true;
    }

    // ─────────────────────────────────────────────────────
    // 调试
    // ─────────────────────────────────────────────────────

    public override string ToString()
    {
        string moduleInfo = IsOccupied
            ? $"{OccupyingModule.name}({(IsFaceDown ? "盖伏" : "翻开")})"
            : "空";
        return $"[Slot {SlotIndex} | {moduleInfo}]";
        // Owner 实现后恢复：$"[Slot {SlotIndex} | Owner:{Owner?.name ?? "未设置"} | {moduleInfo}]"
    }
}