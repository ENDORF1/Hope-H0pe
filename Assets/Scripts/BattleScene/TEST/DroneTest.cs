using UnityEngine;

public class DroneDeployZoneTest : MonoBehaviour
{
    private DroneDeployZone droneZone;

    [Header("测试设置")]
    [SerializeField] private KeyCode addColumnKey    = KeyCode.A;
    [SerializeField] private KeyCode removeColumnKey = KeyCode.R;
    [SerializeField] private KeyCode addDroneKey     = KeyCode.D;
    [SerializeField] private KeyCode removeDroneKey  = KeyCode.X;

    [Header("当前操作列（数字键 0-8 切换）")]
    [SerializeField] private int targetColumn = 0;

    [Header("测试用无人机 Prefab（可不赋值，自动生成占位方块）")]
    [SerializeField] private GameObject testDronePrefab;

    private static readonly KeyCode[] numberKeys = new KeyCode[]
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
        KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8,
        KeyCode.Alpha9
    };

    void Awake()
    {
        droneZone = GetComponent<DroneDeployZone>();
    }

    void Start()
    {
        if (droneZone == null)
        {
            Debug.LogError("测试失败：没有找到 DroneDeployZone 组件！");
            return;
        }
        Debug.Log("=== DroneDeployZone 测试开始 ===");
        Debug.Log("数字键 0-8：切换目标列");
        Debug.Log($"{addColumnKey}：新增列  |  {removeColumnKey}：删除最后列");
        Debug.Log($"{addDroneKey}：目标列加无人机  |  {removeDroneKey}：目标列删无人机");
    }

    void Update()
    {
        if (droneZone == null) return;

        for (int i = 0; i < numberKeys.Length; i++)
        {
            if (Input.GetKeyDown(numberKeys[i]))
            {
                targetColumn = i;  // Alpha1→列0, Alpha2→列1 ...
                Debug.Log($"切换到列 {targetColumn}（共 {droneZone.ColumnCount} 列）");
                break;
            }
        }

        if (Input.GetKeyDown(addColumnKey))    TestAddColumn();
        if (Input.GetKeyDown(removeColumnKey)) TestRemoveColumn();
        if (Input.GetKeyDown(addDroneKey))     TestAddDrone();
        if (Input.GetKeyDown(removeDroneKey))  TestRemoveDrone();
    }

    void TestAddColumn()
    {
        int newIndex = droneZone.AddColumn();
        Debug.Log($"新增列：索引={newIndex}，总列数={droneZone.ColumnCount}");
        ShowInfo();
    }

    void TestRemoveColumn()
    {
        bool success = droneZone.RemoveLastColumn();
        Debug.Log(success
            ? $"删除最后一列，总列数={droneZone.ColumnCount}"
            : "删除失败：已无列可删");
        ShowInfo();
    }

    void TestAddDrone()
    {
        if (targetColumn < 0 || targetColumn >= droneZone.ColumnCount)
        {
            Debug.LogWarning($"targetColumn={targetColumn} 超出范围（共 {droneZone.ColumnCount} 列）");
            return;
        }

        GameObject drone = testDronePrefab != null
            ? Instantiate(testDronePrefab)
            : CreatePlaceholderDrone(targetColumn);

        GameObject slot = droneZone.AddDroneToColumn(targetColumn, drone);

        Debug.Log(slot != null
            ? $"列{targetColumn} 加入无人机，该列数量={droneZone.GetDroneCountInColumn(targetColumn)}"
            : "加入失败");

        ShowInfo();
    }

    void TestRemoveDrone()
    {
        if (targetColumn < 0 || targetColumn >= droneZone.ColumnCount)
        {
            Debug.LogWarning($"targetColumn={targetColumn} 超出范围");
            return;
        }

        var drones = droneZone.GetDronesInColumn(targetColumn);
        if (drones == null || drones.Count == 0)
        {
            Debug.Log($"列{targetColumn} 没有无人机可删");
            return;
        }

        GameObject lastSlot = drones[drones.Count - 1] as GameObject;
        bool success = droneZone.RemoveDroneSlot(targetColumn, lastSlot);

        Debug.Log(success
            ? $"列{targetColumn} 删除最后一架，剩余={droneZone.GetDroneCountInColumn(targetColumn)}"
            : "删除失败");

        ShowInfo();
    }

    void ShowInfo()
    {
        Debug.Log($"── 共 {droneZone.ColumnCount} 列，目标列={targetColumn} ──");
        for (int i = 0; i < droneZone.ColumnCount; i++)
        {
            string marker = (i == targetColumn) ? " <- 目标" : "";
            Debug.Log($"  列{i}：{droneZone.GetDroneCountInColumn(i)} 架{marker}");
        }
    }

    private GameObject CreatePlaceholderDrone(int columnIndex)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"TestDrone_Col{columnIndex}";
        go.transform.localScale = Vector3.one * 0.3f;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
            r.material.color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f);
        return go;
    }
}