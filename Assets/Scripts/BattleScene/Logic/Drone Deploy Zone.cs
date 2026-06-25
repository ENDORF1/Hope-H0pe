using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 无人机部署区
/// 职责：管理所有无人机列，列数与 DeployZone 保持同步
/// 每列对应一个模块格子，同列无人机往下叠牌排列
/// 不负责无人机的战斗逻辑（由战斗引擎读取）
/// </summary>
public class DroneDeployZone : MonoBehaviour
{
    [Header("列设置")]
    [SerializeField] private float columnSpacing = 1.2f;   // 与 DeployZone.slotSpacing 保持一致
    [SerializeField] private bool  expandRight   = true;   // 与 DeployZone 方向一致

    [Header("叠牌设置")]
    [SerializeField] private float droneSlotHeight  = 0f;
    [SerializeField] private float droneStackOffset = 20f;  // 每多一架偏移多少像素（正数）
    [SerializeField] private StackDirection stackDirection = StackDirection.Down;

    public enum StackDirection { Up, Down, Left, Right }

    [Header("Prefab")]
    [SerializeField] private GameObject droneSlotPrefab;

    [Header("初始列数（应与 DeployZone 的 initialSlots 一致）")]
    [SerializeField] private int initialColumns = 5;

    // 外层 List：每个元素代表一列（对应一个模块格子索引）
    // 内层 List：该列里所有无人机槽 GameObject，按生成顺序排列
    private List<List<GameObject>> columns = new List<List<GameObject>>();

    public int ColumnCount => columns.Count;

    void Start()
    {
        for (int i = 0; i < initialColumns; i++)
            columns.Add(new List<GameObject>());
    }

    // ─────────────────────────────────────────────────────
    // 列管理（由 DeployZone 驱动，格子增加时调用）
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 新增一列（对应 DeployZone.AddSlot）
    /// 返回新列的索引
    /// </summary>
    public int AddColumn()
    {
        columns.Add(new List<GameObject>());
        RearrangeColumns();
        return columns.Count - 1;
    }

    /// <summary>
    /// 移除最后一列（对应 DeployZone.RemoveLastSlot）
    /// 同时销毁该列所有无人机槽
    /// </summary>
    public bool RemoveLastColumn()
    {
        if (columns.Count == 0) return false;

        int last = columns.Count - 1;
        DestroyColumn(last);
        columns.RemoveAt(last);
        RearrangeColumns();
        return true;
    }

    /// <summary>
    /// 将列数同步到目标数量（对应 DeployZone.ResetToCount）
    /// </summary>
    public void SyncToCount(int targetCount)
    {
        if (targetCount < 0)
        {
            Debug.LogError("DroneDeployZone: targetCount 不能为负数");
            return;
        }
        while (columns.Count < targetCount) AddColumn();
        while (columns.Count > targetCount) RemoveLastColumn();
    }

    // ─────────────────────────────────────────────────────
    // 无人机槽管理
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 在指定列新增一个无人机槽，返回新槽的 GameObject
    /// 传入的 droneObject 会被放入槽中（占位，之后替换为 DroneBase）
    /// </summary>
    public GameObject AddDroneToColumn(int columnIndex, GameObject droneObject)
    {
        if (!IsValidColumn(columnIndex)) return null;
        if (droneSlotPrefab == null)
        {
            Debug.LogError("DroneDeployZone: droneSlotPrefab 未赋值！");
            return null;
        }

        // 获取该列的 X 位置（复用列排列逻辑）
        float xPos = GetColumnX(columnIndex);

        // 生成槽，默认隐藏
        GameObject newSlot = Instantiate(droneSlotPrefab, transform);
        newSlot.name = $"DroneSlot_{columnIndex}_{columns[columnIndex].Count}";
        newSlot.SetActive(false);

        // 放入无人机对象（作为子物体）
        if (droneObject != null)
        {
            droneObject.transform.SetParent(newSlot.transform, false);
            droneObject.transform.localPosition = Vector3.zero;

            // 有无人机才显示槽
            newSlot.SetActive(true);

            // 自动启用 HoverPreview（如果无人机上挂有该组件）
            HoverPreview hp = droneObject.GetComponent<HoverPreview>();
            if (hp != null)
                hp.ThisPreviewEnabled = true;
        }
        else
        {
            // 没有无人机，不加入列表，直接销毁空槽
            Destroy(newSlot);
            Debug.LogWarning($"DroneDeployZone: 列{columnIndex} 传入的 droneObject 为空，槽未生成。");
            return null;
        }

        columns[columnIndex].Add(newSlot);

        // 重新排列该列的所有槽
        RearrangeColumn(columnIndex, xPos);

        return newSlot;
    }

    /// <summary>
    /// 从指定列移除一个无人机槽（无人机被摧毁时调用）
    /// 不负责销毁无人机 GameObject，只销毁槽并重排
    /// </summary>
    public bool RemoveDroneSlot(int columnIndex, GameObject droneSlot)
    {
        if (droneSlot == null) return false;

        // 先在指定列里查找（columnIndex=-1 时直接跳到全列扫描）
        int foundColumn = -1;
        if (IsValidColumn(columnIndex) && columns[columnIndex].Contains(droneSlot))
        {
            foundColumn = columnIndex;
        }
        else
        {
            // 指定列找不到时全列扫描兜底
            // 原因：SpawnAndAttachDrone 用 target.SlotIndex 注册，
            // DestroyDroneWithSlot 用宿主模块的 SlotIndex 移除，
            // 两者在 SelectDroneTarget 把无人机分配到不同格时会不一致。
            Debug.LogWarning($"DroneDeployZone: 列{columnIndex} 中找不到指定槽，全列扫描中...");
            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i].Contains(droneSlot))
                {
                    foundColumn = i;
                    break;
                }
            }
        }

        if (foundColumn < 0)
        {
            Debug.LogWarning($"DroneDeployZone: 全列扫描也找不到指定槽，已跳过");
            // 槽可能已被销毁但 GameObject 引用仍残留，强制隐藏+销毁兜底
            droneSlot.SetActive(false);
            if (Application.isPlaying) Destroy(droneSlot);
            else                       DestroyImmediate(droneSlot);
            return false;
        }

        columns[foundColumn].Remove(droneSlot);

        // 立即隐藏，防止 Destroy 延迟销毁期间被 RearrangeColumn 重新定位显示
        droneSlot.SetActive(false);

        if (Application.isPlaying)
            Destroy(droneSlot);
        else
            DestroyImmediate(droneSlot);

        RearrangeColumn(foundColumn, GetColumnX(foundColumn));
        return true;
    }

    /// <summary>
    /// 将包含指定无人机的槽置顶（HoverPreview 悬停时调用）
    /// </summary>
    public void BringDroneToTop(GameObject droneObject)
    {
        for (int col = 0; col < columns.Count; col++)
        {
            foreach (GameObject slot in columns[col])
            {
                if (slot == null) continue;
                // 检查无人机是否是这个槽的子物体
                if (droneObject.transform.IsChildOf(slot.transform))
                {
                    slot.transform.SetAsLastSibling();
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 恢复槽的原始顺序（HoverPreview 离开时调用）
    /// </summary>
    public void RestoreSlotOrder()
    {
        // 按列重新排列所有槽的 sibling index
        // 列0的所有槽在前，列1的在后，以此类推
        int siblingIndex = 0;
        for (int col = 0; col < columns.Count; col++)
        {
            foreach (GameObject slot in columns[col])
            {
                if (slot == null) continue;
                slot.transform.SetSiblingIndex(siblingIndex++);
            }
        }
    }
    public IReadOnlyList<GameObject> GetDronesInColumn(int columnIndex)
    {
        if (!IsValidColumn(columnIndex)) return null;
        return columns[columnIndex].AsReadOnly();
    }

    /// <summary>
    /// 获取指定列的无人机数量
    /// </summary>
    public int GetDroneCountInColumn(int columnIndex)
    {
        if (!IsValidColumn(columnIndex)) return 0;
        return columns[columnIndex].Count;
    }

    // ─────────────────────────────────────────────────────
    // 排列逻辑
    // ─────────────────────────────────────────────────────

    /// <summary>重新计算所有列的 X 位置（列数变化时调用）</summary>
    private void RearrangeColumns()
    {
        for (int i = 0; i < columns.Count; i++)
            RearrangeColumn(i, GetColumnX(i));
    }

    /// <summary>重新排列单列内所有无人机槽的位置</summary>
    private void RearrangeColumn(int columnIndex, float xPos)
    {
        List<GameObject> column = columns[columnIndex];
        for (int i = 0; i < column.Count; i++)
        {
            if (column[i] == null) continue;

            float offset = i * droneStackOffset;
            float dx = 0f, dy = 0f;
            switch (stackDirection)
            {
                case StackDirection.Up:    dy =  offset; break;
                case StackDirection.Down:  dy = -offset; break;
                case StackDirection.Left:  dx = -offset; break;
                case StackDirection.Right: dx =  offset; break;
            }

            RectTransform rt = column[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(xPos + dx, droneSlotHeight + dy);
            }
            else
            {
                float worldY = transform.position.y + droneSlotHeight + dy;
                float worldX = xPos + dx;
                column[i].transform.position = new Vector3(worldX, worldY, transform.position.z);
            }
        }
    }

    /// <summary>计算第 columnIndex 列的 X 锚点位置</summary>
    private float GetColumnX(int columnIndex)
    {
        RectTransform parentRect = GetComponent<RectTransform>();
        if (parentRect != null)
        {
            // UI 模式：与 DeployZone 相同的计算方式
            float canvasWidth = parentRect.rect.width;
            // 用 columnSpacing 作为列宽参考，避免 Instantiate 后 rect.width 未初始化的问题
            float slotWidth = columnSpacing;

            float dir    = expandRight ? 1f : -1f;
            float startX = expandRight
                ? -canvasWidth / 2f + slotWidth / 2f
                :  canvasWidth / 2f - slotWidth / 2f;

            return startX + dir * columnIndex * columnSpacing;
        }
        else
        {
            // 非 UI 模式
            float totalWidth = (columns.Count - 1) * columnSpacing;
            float startX     = transform.position.x - totalWidth / 2f;
            return startX + columnIndex * columnSpacing;
        }
    }

    // ─────────────────────────────────────────────────────
    // 工具方法
    // ─────────────────────────────────────────────────────

    private bool IsValidColumn(int index)
    {
        if (index < 0 || index >= columns.Count)
        {
            Debug.LogWarning($"DroneDeployZone: 列索引 {index} 越界（当前共 {columns.Count} 列）");
            return false;
        }
        return true;
    }

    private void DestroyColumn(int index)
    {
        foreach (GameObject slot in columns[index])
        {
            if (slot == null) continue;
            if (Application.isPlaying)
                Destroy(slot);
            else
                DestroyImmediate(slot);
        }
        columns[index].Clear();
    }
}