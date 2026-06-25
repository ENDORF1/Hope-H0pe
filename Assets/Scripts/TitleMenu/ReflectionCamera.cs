using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class ReflectionCamera : MonoBehaviour
{
    [Header("主相机")]
    public Camera mainCamera;

    [Range(0.25f, 1f)]
    public float resolutionScale = 1f;

    [Header("排除倒影的对象（通过 CanvasGroup.alpha 隐藏，不影响动画）")]
    public List<GameObject> excludeObjects = new List<GameObject>();

    private Camera        _cam;
    private RenderTexture _rt;
    private int           _lastW, _lastH;
    private int           _frameCount = 0;

    // 运行时自动获取/添加的 CanvasGroup
    private List<CanvasGroup> _excludeGroups = new List<CanvasGroup>();

    public RenderTexture RT => _rt;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.enabled = false;
    }

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        // 为每个排除对象确保有 CanvasGroup
        _excludeGroups.Clear();
        foreach (var go in excludeObjects)
        {
            if (go == null) { _excludeGroups.Add(null); continue; }
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            _excludeGroups.Add(cg);
        }

        SyncWithMainCamera();
        RebuildRT();
    }

    void LateUpdate()
    {
        SyncWithMainCamera();

        int w = Mathf.Max(1, Mathf.RoundToInt(Screen.width  * resolutionScale));
        int h = Mathf.Max(1, Mathf.RoundToInt(Screen.height * resolutionScale));
        if (w != _lastW || h != _lastH)
            RebuildRT();

        _frameCount++;
        if (_frameCount % 2 != 0) return;

        // alpha=0 隐藏 → 渲染 → 恢复
        SetAlpha(0f);
        _cam.Render();
        SetAlpha(1f);
    }

    void SetAlpha(float alpha)
    {
        foreach (var cg in _excludeGroups)
            if (cg != null) cg.alpha = alpha;
    }

    void SyncWithMainCamera()
    {
        if (mainCamera == null || _cam == null) return;
        _cam.orthographic     = mainCamera.orthographic;
        _cam.orthographicSize = mainCamera.orthographicSize;
        _cam.nearClipPlane    = mainCamera.nearClipPlane;
        _cam.farClipPlane     = mainCamera.farClipPlane;
        // 强制透明背景，不同步主相机的 clearFlags
        _cam.clearFlags      = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        transform.SetPositionAndRotation(
            mainCamera.transform.position,
            mainCamera.transform.rotation);
    }

    void RebuildRT()
    {
        if (_cam == null) _cam = GetComponent<Camera>();

        _lastW = Mathf.Max(1, Mathf.RoundToInt(Screen.width  * resolutionScale));
        _lastH = Mathf.Max(1, Mathf.RoundToInt(Screen.height * resolutionScale));

        if (_rt != null)
        {
            _cam.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        _rt = new RenderTexture(_lastW, _lastH, 0, RenderTextureFormat.ARGB32)
        {
            filterMode   = FilterMode.Bilinear,
            wrapMode     = TextureWrapMode.Clamp,
            antiAliasing = 1,
        };
        _rt.Create();
        _cam.targetTexture = _rt;

        // 清空 RT，确保背景完全透明
        var prev = RenderTexture.active;
        RenderTexture.active = _rt;
        GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
        RenderTexture.active = prev;
    }

    /// <summary>渲染一帧到指定 RT（供 GlitchController 捕获熄忘倒影）</summary>
    public void RenderOnce(RenderTexture target)
    {
        if (_cam == null) return;
        var prev           = _cam.targetTexture;
        _cam.targetTexture = target;
        SetAlpha(0f);
        _cam.Render();
        SetAlpha(1f);
        _cam.targetTexture = prev;
    }

    void OnDestroy()
    {
        if (_cam != null) _cam.targetTexture = null;
        if (_rt  != null) { _rt.Release(); Destroy(_rt); }
    }
}