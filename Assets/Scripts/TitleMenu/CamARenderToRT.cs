using UnityEngine;

/// <summary>
/// 挂在 Cam A 上。
/// 只在 Start 时对齐一次，避免循环跳变。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CamARenderToRT : MonoBehaviour
{
    [Header("渲染目标")]
    [SerializeField] public RenderTexture reflectionRT;

    [Header("需要拍摄的 Canvas")]
    [Tooltip("拖入 Canvas (1)")]
    [SerializeField] private RectTransform targetCanvas;

    private Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;

        if (reflectionRT != null)
            _cam.targetTexture = reflectionRT;
        else
            Debug.LogError("[CamARenderToRT] reflectionRT 未赋值！");
    }

    void Start()
    {
        AlignToCanvas();
    }

    private void AlignToCanvas()
    {
        if (targetCanvas == null) return;

        Vector3[] corners = new Vector3[4];
        targetCanvas.GetWorldCorners(corners);

        float minX = corners[0].x;
        float maxX = corners[2].x;
        float minY = corners[0].y;
        float maxY = corners[2].y;

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        float height   = maxY - minY;

        transform.position        = new Vector3(centerX, centerY, corners[0].z - 10f);
        _cam.orthographicSize     = height * 0.5f;

        if (reflectionRT != null)
            _cam.aspect = (float)reflectionRT.width / reflectionRT.height;
    }
}