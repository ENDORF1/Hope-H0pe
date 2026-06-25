using UnityEngine;

/// <summary>
/// 挂在 ReflCamera 上。
/// 持有 RenderTexture 引用，供 ReflectionWaterController 在运行时获取。
/// 同时负责在屏幕分辨率变化时自动重建 RT（保持清晰度）。
/// </summary>
[RequireComponent(typeof(Camera))]
public class ReflectionRTProvider : MonoBehaviour
{
    [SerializeField] private RenderTexture _rt;
    [SerializeField] [Range(0.25f, 1f)] private float _resolutionScale = 1f;

    private Camera _cam;
    private int    _lastW, _lastH;

    /// <summary>当前 RenderTexture，供外部读取。</summary>
    public RenderTexture RT => _rt;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_rt != null)
            _cam.targetTexture = _rt;
    }

    void LateUpdate()
    {
        int w = Mathf.RoundToInt(Screen.width  * _resolutionScale);
        int h = Mathf.RoundToInt(Screen.height * _resolutionScale);
        if (w == _lastW && h == _lastH) return;

        _lastW = w;
        _lastH = h;

        // 分辨率变化 → 重建 RT
        if (_rt != null)
        {
            _cam.targetTexture = null;
            _rt.Release();
            _rt.width  = Mathf.Max(1, w);
            _rt.height = Mathf.Max(1, h);
            _rt.Create();
            _cam.targetTexture = _rt;
        }
    }
}
