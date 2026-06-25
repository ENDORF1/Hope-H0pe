using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 挂在专用的 ReflectionSourceCamera 上。
/// 把 Content 层（所有非按钮 UI）渲染到 RenderTexture，
/// 供 ReflectionWater 层采样做水波扰动。
///
/// 架构：
///   MainCamera           → 正常渲染整个场景
///   ReflectionSourceCam  → 只渲染 ContentLayer（不含按钮），输出到 ReflectionRT
///   ReflectionWater      → RawImage，采样 ReflectionRT + 扰动 Shader
///
/// 使用：
///   1. 创建专用 Camera，Layer 只勾选 ContentLayer
///   2. 挂此脚本，拖入 targetCanvas
///   3. 脚本自动创建/复用 ReflectionRT
/// </summary>
[RequireComponent(typeof(Camera))]
public class ReflectionCapture : MonoBehaviour
{
    [Header("目标 Canvas")]
    [SerializeField] private Canvas targetCanvas;

    [Header("RT 分辨率缩放（1.0 = 全分辨率，0.5 = 半分辨率节省性能）")]
    [Range(0.25f, 1f)]
    [SerializeField] private float resolutionScale = 1f;

    [Header("排除的对象（按钮容器等，其子对象也会被排除）")]
    [SerializeField] private List<GameObject> excludeObjects = new List<GameObject>();

    private Camera    _cam;
    private RenderTexture _rt;
    private int       _lastW, _lastH;

    // 对外暴露 RT，供 ReflectionWaterController 采样
    public RenderTexture ReflectionRT => _rt;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        SetupCamera();
    }

    void Start()
    {
        EnsureRT();
    }

    void LateUpdate()
    {
        // 分辨率变化时重建 RT
        int w = Mathf.RoundToInt(Screen.width  * resolutionScale);
        int h = Mathf.RoundToInt(Screen.height * resolutionScale);
        if (w != _lastW || h != _lastH)
            EnsureRT();
    }

    private void SetupCamera()
    {
        _cam.clearFlags      = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.clear;
        _cam.orthographic    = true;
        _cam.depth           = -10; // 在主相机之前渲染
        _cam.cullingMask     = 0;   // 先清空，由外部或 Setup 工具配置
        _cam.enabled         = true;
    }

    private void EnsureRT()
    {
        _lastW = Mathf.Max(1, Mathf.RoundToInt(Screen.width  * resolutionScale));
        _lastH = Mathf.Max(1, Mathf.RoundToInt(Screen.height * resolutionScale));

        if (_rt != null)
        {
            _cam.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
        }

        _rt = new RenderTexture(_lastW, _lastH, 0, RenderTextureFormat.ARGB32)
        {
            name        = "ReflectionRT",
            filterMode  = FilterMode.Bilinear,
            wrapMode    = TextureWrapMode.Clamp,
            antiAliasing = 1,
        };
        _rt.Create();
        _cam.targetTexture = _rt;
    }

    void OnDestroy()
    {
        if (_rt != null)
        {
            _cam.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
        }
    }
}