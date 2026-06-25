using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SameDistanceChildren : MonoBehaviour 
{
    public Transform[] Children;
    
    [Header("对齐设置")]
    public bool AlignX = true;
    public bool AlignY = false;
    public bool AlignZ = false;
    
    [Header("预览设置")]
    public Color GizmoColor = Color.green;
    public float GizmoSize = 0.5f;
    public bool ShowGizmos = true;

    #if UNITY_EDITOR
    // 在 Inspector 中添加按钮
    [CustomEditor(typeof(SameDistanceChildren))]
    public class SameDistanceChildrenEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            SameDistanceChildren script = (SameDistanceChildren)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("工具", EditorStyles.boldLabel);
            
            if (GUILayout.Button("自动排列子物体", GUILayout.Height(30)))
            {
                script.ArrangeChildren();
            }
            
            if (GUILayout.Button("获取所有子物体", GUILayout.Height(25)))
            {
                script.GetAllChildren();
            }
            
            if (GUILayout.Button("清除数组", GUILayout.Height(25)))
            {
                script.ClearChildren();
            }
            
            EditorGUILayout.Space();
            
            if (script.Children != null && script.Children.Length > 0)
            {
                EditorGUILayout.LabelField($"当前子物体数量: {script.Children.Length}", EditorStyles.boldLabel);
            }
        }
    }
    #endif

    void Awake() 
    {
        ArrangeChildren();
    }
    
    [ContextMenu("自动排列子物体")]
    public void ArrangeChildren()
    {
        if (Children == null || Children.Length < 2)
        {
            Debug.LogWarning("需要至少2个子物体才能自动排列");
            return;
        }
        
        // 检查是否有空引用
        for (int i = 0; i < Children.Length; i++)
        {
            if (Children[i] == null)
            {
                Debug.LogError($"第 {i} 个子物体为空，请检查数组");
                return;
            }
        }
        
        Vector3 firstElementPos = Children[0].position;
        Vector3 lastElementPos = Children[Children.Length - 1].position;
        
        // 计算间距
        float XDist = 0f;
        float YDist = 0f;
        float ZDist = 0f;
        
        if (AlignX)
            XDist = (lastElementPos.x - firstElementPos.x) / (float)(Children.Length - 1);
        
        if (AlignY)
            YDist = (lastElementPos.y - firstElementPos.y) / (float)(Children.Length - 1);
        
        if (AlignZ)
            ZDist = (lastElementPos.z - firstElementPos.z) / (float)(Children.Length - 1);
        
        Vector3 Dist = new Vector3(XDist, YDist, ZDist);
        
        // 记录操作以便撤销
        #if UNITY_EDITOR
        Undo.RecordObjects(Children, "Arrange Children");
        #endif
        
        for (int i = 1; i < Children.Length; i++)
        {
            Children[i].position = Children[i - 1].position + Dist;
        }
        
        Debug.Log($"已自动排列 {Children.Length} 个子物体");
    }
    
    [ContextMenu("获取所有子物体")]
    public void GetAllChildren()
    {
        Children = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            Children[i] = transform.GetChild(i);
        }
        
        Debug.Log($"已获取 {Children.Length} 个子物体");
    }
    
    [ContextMenu("清除数组")]
    public void ClearChildren()
    {
        Children = new Transform[0];
        Debug.Log("已清除子物体数组");
    }
    
    // 可视化辅助
    void OnDrawGizmos()
    {
        if (!ShowGizmos || Children == null || Children.Length < 2)
            return;
        
        // 绘制连接线
        Gizmos.color = GizmoColor;
        for (int i = 0; i < Children.Length - 1; i++)
        {
            if (Children[i] != null && Children[i + 1] != null)
            {
                Gizmos.DrawLine(Children[i].position, Children[i + 1].position);
            }
        }
        
        // 绘制每个点的位置
        Gizmos.color = Color.yellow;
        foreach (Transform child in Children)
        {
            if (child != null)
            {
                Gizmos.DrawSphere(child.position, GizmoSize * 0.3f);
            }
        }
        
        // 标记起点和终点
        if (Children[0] != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(Children[0].position, GizmoSize);
        }
        
        if (Children[Children.Length - 1] != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(Children[Children.Length - 1].position, GizmoSize);
        }
    }
    
    // 在 Scene 视图中显示标签
    void OnDrawGizmosSelected()
    {
        if (!ShowGizmos || Children == null)
            return;
        
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        
        for (int i = 0; i < Children.Length; i++)
        {
            if (Children[i] != null)
            {
                #if UNITY_EDITOR
                Handles.Label(Children[i].position + Vector3.up * 0.5f, $" [{i}]", style);
                #endif
            }
        }
    }
}