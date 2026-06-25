using UnityEngine;

/// <summary>
/// 部署区测试脚本 - 测试格子的添加和删除功能
/// 挂在 DeployZone 对象上
/// </summary>
public class DeployZoneTester : MonoBehaviour
{
    private DeployZone deployZone;

    [Header("测试设置")]
    [SerializeField] private KeyCode addKey = KeyCode.A;
    [SerializeField] private KeyCode removeKey = KeyCode.R;
    [SerializeField] private KeyCode resetKey = KeyCode.Space;
    [SerializeField] private int resetCount = 6;

    void Awake()
    {
        deployZone = GetComponent<DeployZone>();
    }

    void Start()
    {
        if (deployZone == null)
        {
            Debug.LogError("测试失败：没有找到 DeployZone 组件！请确保本脚本和 DeployZone 挂在同一个对象上。");
            return;
        }

        // 【修复】resetCount 超出 maxSlots 时自动修正，避免运行时报错
        if (resetCount > deployZone.MaxSlots)
        {
            Debug.LogWarning($"resetCount ({resetCount}) 超过 MaxSlots ({deployZone.MaxSlots})，已自动修正。");
            resetCount = deployZone.MaxSlots;
        }

        Debug.Log("=== DeployZone 测试开始 ===");
        Debug.Log($"最大格子数量: {deployZone.MaxSlots}");
        Debug.Log($"初始格子数量: {deployZone.InitialSlots}");
        Debug.Log($"按 {addKey} 键：添加格子");
        Debug.Log($"按 {removeKey} 键：删除最后一个格子");
        Debug.Log($"按 {resetKey} 键：重置到 {resetCount} 个格子");

        // 【修复】等两帧再显示信息，确保 DeployZone.Start() 已执行完毕
        Invoke(nameof(ShowSlotInfo), 0.1f);
    }

    void Update()
    {
        if (deployZone == null) return;

        if (Input.GetKeyDown(addKey))
            TestAddSlot();

        if (Input.GetKeyDown(removeKey))
            TestRemoveSlot();

        if (Input.GetKeyDown(resetKey))
            TestReset();
    }

    void TestAddSlot()
    {
        Debug.Log("\n--- 测试添加格子 ---");

        int newIndex = deployZone.AddSlot();

        if (newIndex >= 0)
            Debug.Log($"✅ 添加成功：新格子索引={newIndex}，当前总数={deployZone.CurrentSlotCount}");
        else
            Debug.Log($"❌ 添加失败：已达到最大数量 {deployZone.MaxSlots}");

        ShowSlotInfo();
    }

    void TestRemoveSlot()
    {
        Debug.Log("\n--- 测试删除格子 ---");

        bool success = deployZone.RemoveLastSlot();

        if (success)
            Debug.Log($"✅ 删除成功：当前总数={deployZone.CurrentSlotCount}");
        else
            Debug.Log($"❌ 删除失败：已是最少格子数（{deployZone.InitialSlots} 个）");

        ShowSlotInfo();
    }

    void TestReset()
    {
        Debug.Log($"\n--- 测试重置到 {resetCount} 个格子 ---");

        deployZone.ResetToCount(resetCount);

        Debug.Log($"重置完成：当前总数={deployZone.CurrentSlotCount}");
        ShowSlotInfo();
    }

    void ShowSlotInfo()
    {
        Debug.Log($"\n当前格子总数: {deployZone.CurrentSlotCount}");

        if (deployZone.CurrentSlotCount == 0)
        {
            Debug.Log("  没有格子");
            return;
        }

        Debug.Log($"DeployZone 位置: {deployZone.transform.position}");

        for (int i = 0; i < deployZone.CurrentSlotCount; i++)
        {
            GameObject slot = deployZone.GetSlot(i);
            if (slot != null)
                Debug.Log($"  格子{i}: 世界位置={slot.transform.position}, 激活={slot.activeInHierarchy}");
            else
                Debug.LogError($"  格子{i}: 引用为空！");
        }

        if (deployZone.CurrentSlotCount >= 2)
        {
            GameObject slot0 = deployZone.GetSlot(0);
            GameObject slot1 = deployZone.GetSlot(1);
            if (slot0 != null && slot1 != null)
            {
                float spacing = Vector3.Distance(slot0.transform.position, slot1.transform.position);
                Debug.Log($"格子间距: {spacing:F3}");
            }
        }

        Debug.Log("---");
    }

    private void OnDrawGizmos()
    {
        if (deployZone == null) return;

        for (int i = 0; i < deployZone.CurrentSlotCount; i++)
        {
            GameObject slot = deployZone.GetSlot(i);
            if (slot != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(slot.transform.position, 0.2f);

#if UNITY_EDITOR
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(slot.transform.position + Vector3.up * 0.5f, $"Slot {i}");
#endif
            }
        }

#if UNITY_EDITOR
        UnityEditor.Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 260, 160), GUI.skin.box);
        GUILayout.Label("=== DeployZone 测试 ===");
        GUILayout.Label($"格子数量: {deployZone.CurrentSlotCount} / {deployZone.MaxSlots}");
        GUILayout.Label($"初始数量: {deployZone.InitialSlots}");
        GUILayout.Label($"DeployZone 位置: {deployZone.transform.position}");
        GUILayout.Label($"{addKey}: 添加格子");
        GUILayout.Label($"{removeKey}: 删除格子");
        GUILayout.Label($"Space: 重置到 {resetCount}");
        GUILayout.EndArea();
        UnityEditor.Handles.EndGUI();
#endif
    }
}
