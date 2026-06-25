using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class WorldSpaceCanvasScaler : MonoBehaviour
{
    [Header("参考分辨率（与原 CanvasScaler 保持一致）")]
    public Vector2 referenceResolution = new Vector2(2560, 1440);

    [Header("绑定的相机（留空自动使用 Canvas.worldCamera）")]
    public Camera targetCamera;

    [Header("位置偏移（参考分辨率单位）")]
    [Tooltip("相对于相机中心的偏移，单位与 referenceResolution 一致。\n" +
             "例如填 (-2560, 0) 表示此 Canvas 永远在相机左边一个屏幕宽度的位置。\n" +
             "TitleScene 的 Canvas 保持 (0, 0) 不变。")]
    public Vector2 positionOffset = Vector2.zero;

    [Header("转场控制")]
    [Tooltip("为 true 时停止更新 Canvas 位置（让 Canvas 留在原地，相机可以自由移动）")]
    public bool lockPosition = false;

    private Canvas        _canvas;
    private RectTransform _rect;
    private int           _lastScreenW;
    private int           _lastScreenH;

    RectTransform Rect
    {
        get
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            return _rect;
        }
    }

    Canvas CanvasComp
    {
        get
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            return _canvas;
        }
    }

    void Start()
    {
        if (targetCamera == null)
            targetCamera = CanvasComp.worldCamera;
        UpdateSize();
    }

    void Update()
    {
        if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
            UpdateSize();
    }

    public void UpdateSize()
    {
        if (targetCamera == null || !targetCamera.orthographic) return;
        if (Rect == null) return;

        _lastScreenW = Screen.width;
        _lastScreenH = Screen.height;

        float aspect      = (float)Screen.width / Screen.height;
        float orthoHeight = targetCamera.orthographicSize * 2f;
        float orthoWidth  = orthoHeight * aspect;

        float scaleX = orthoWidth  / referenceResolution.x;
        float scaleY = orthoHeight / referenceResolution.y;
        float scale  = Mathf.Min(scaleX, scaleY);

        Rect.sizeDelta  = referenceResolution;
        Rect.localScale = new Vector3(scale, scale, scale);

        if (!lockPosition)
        {
            Vector3 basePos = targetCamera.transform.position
                            + targetCamera.transform.forward
                            * (targetCamera.nearClipPlane + 0.1f);

            Vector3 worldOffset = targetCamera.transform.right * (positionOffset.x * scale)
                                + targetCamera.transform.up    * (positionOffset.y * scale);

            Rect.position = basePos + worldOffset;
            Rect.rotation = targetCamera.transform.rotation;
        }
    }
}