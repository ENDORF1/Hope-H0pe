using UnityEngine;
using System.Collections;

public class DragTargetLine : MonoBehaviour
{
    public Color startColor = Color.white;
    public Color endColor = Color.red;
    public float lineWidth = 0.2f;
    
    private LineRenderer lineRenderer;
    private bool isActive = false;
    private Transform startTransform;
    private Camera mainCamera;
    
    void Awake()
    {
        mainCamera = Camera.main;
        
        // 创建LineRenderer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        
        // 关键设置
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingLayerName = "AboveEverything";  // 确保在最上层
        lineRenderer.sortingOrder = 999;  // 最高层级
        
        // 设置材质和颜色
        SetColors(startColor, endColor);
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material.renderQueue = 3000;  // 确保渲染顺序
        
        // 默认隐藏
        lineRenderer.enabled = false;
    }
    
    void Update()
    {
        if (isActive && startTransform != null && mainCamera != null)
        {
            Vector3 startPos = startTransform.position; startPos.z = 0;
            // 终点 = Target 对象自身位置（由 QuickPlayTargetSelector 驱动跟鼠标）
            Vector3 endPos = transform.position; endPos.z = 0;

            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(1, endPos);

            if (!lineRenderer.enabled)
                lineRenderer.enabled = true;
        }
    }
    
    public void ShowLine(Transform start)
    {
        startTransform = start;
        isActive = true;
        
        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
            
            // 初始化一次位置
            if (mainCamera != null)
            {
                Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mousePos.z = 0;
                Vector3 startPos = startTransform.position;
                startPos.z = 0;
                
                lineRenderer.SetPosition(0, startPos);
                lineRenderer.SetPosition(1, mousePos);
            }
        }
        
        Debug.Log("线条显示");
    }
    
    public void HideLine()
    {
        isActive = false;
        if (lineRenderer != null)
            lineRenderer.enabled = false;
        startTransform = null;
    }
    
    public void SetColors(Color start, Color end)
    {
        startColor = start;
        endColor = end;
        startColor.a = 1f;
        endColor.a = 1f;
        
        if (lineRenderer != null)
        {
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
        }
    }
    
    public void SetWidth(float width)
    {
        lineWidth = width;
        
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
        }
    }
    
    void OnEnable()
    {
        // 确保启用时能显示
        if (isActive && lineRenderer != null)
            lineRenderer.enabled = true;
    }
    
    void OnDisable()
    {
        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }
}