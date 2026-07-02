using UnityEngine;
using System.Collections;

public class CharacterSelectEntry : MonoBehaviour
{
    public static CharacterSelectEntry Instance { get; private set; }
    public static float CanvasWorldX { get; private set; } = 0f;

    [Header("场景引用（自动查找，留空也可）")]
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Canvas sceneCanvas;

    [Header("淡入时长（秒）")]
    [SerializeField] public float fadeInDuration = 0.3f;

    [Header("过渡背景")]
    [SerializeField] private UnityEngine.UI.RawImage transitionBackground;
    [SerializeField] private UnityEngine.UI.RawImage transitionBlackBackground;

    private CanvasGroup            _canvasGroup;
    private WorldSpaceCanvasScaler _canvasScaler;
    private bool _standalone = false; // 独立运行（无 TitleScene）
    public static bool Standalone { get; private set; }

    // Boot 预加载时 PositionNextToTitleCanvas 会覆盖 Canvas 的 localScale 和 Z 位置，
    // 保存原始值在 Reveal 时恢复，保持与 Standalone 一致
    private Vector3 _origLocalScale;
    private float _origCanvasZ;

    void Awake()
    {
        Instance = this;

        var scene = gameObject.scene;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (sceneCamera == null)
                sceneCamera = root.GetComponentInChildren<Camera>(true);
            if (sceneCanvas == null)
                sceneCanvas = root.GetComponentInChildren<Canvas>(true);
            if (sceneCamera != null && sceneCanvas != null) break;
        }

        if (sceneCanvas != null)
        {
            _canvasGroup = sceneCanvas.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = sceneCanvas.gameObject.AddComponent<CanvasGroup>();
            _canvasScaler = sceneCanvas.GetComponent<WorldSpaceCanvasScaler>();
        }

        // 检测是否独立运行。用 FindObjectOfType 而非 Instance，避免 Awake 执行顺序
        // 导致的竞态：预加载时 TitleScreenManager.Awake 可能晚于本脚本执行
        _standalone = (FindFirstObjectByType<TitleScreenManager>() == null);
        Standalone  = _standalone;

        if (sceneCamera  != null) sceneCamera.enabled = _standalone;
        if (_canvasGroup != null) _canvasGroup.alpha   = _standalone ? 1f : 0f;

        if (!_standalone)
        {
            if (transitionBackground != null)
            {
                var c = transitionBackground.color; c.a = 1f;
                transitionBackground.color = c;
            }
            if (transitionBlackBackground != null)
            {
                var c = transitionBlackBackground.color; c.a = 1f;
                transitionBlackBackground.color = c;
            }

            // 保存原始值，PositionNextToTitleCanvas 会用 Title Canvas 的值覆盖
            if (sceneCanvas != null)
            {
                var rt = sceneCanvas.GetComponent<RectTransform>();
                _origLocalScale = rt.localScale;
                _origCanvasZ    = rt.position.z;
            }

            PositionNextToTitleCanvas();
        }
    }

    void Start()
    {
        if (sceneCanvas != null)
        {
            var scaler = sceneCanvas.GetComponent<WorldSpaceCanvasScaler>();
            if (scaler != null) scaler.UpdateSize();

            CanvasWorldX = sceneCanvas.transform.position.x;
            Debug.Log($"[CharacterSelectEntry] CanvasWorldX = {CanvasWorldX}");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void PositionNextToTitleCanvas()
    {
        if (sceneCanvas == null) return;

        Canvas titleCanvas = null;
        if (TitleScreenManager.Instance != null)
        {
            titleCanvas = TitleScreenManager.Instance.GetComponentInParent<Canvas>(true);
            if (titleCanvas == null)
                titleCanvas = TitleScreenManager.Instance.GetComponent<Canvas>();
        }

        if (titleCanvas == null)
        {
            Debug.LogWarning("[CharacterSelectEntry] 找不到主界面 Canvas，无法定位。");
            return;
        }

        if (_canvasScaler != null)
            _canvasScaler.lockPosition = true;

        var   titleRT     = titleCanvas.GetComponent<RectTransform>();
        float canvasWidth = titleRT.sizeDelta.x * titleRT.localScale.x;

        var myRT        = sceneCanvas.GetComponent<RectTransform>();
        myRT.position   = titleRT.position + Vector3.left * canvasWidth;
        myRT.rotation   = titleRT.rotation;
        myRT.localScale = titleRT.localScale;

        if (sceneCamera != null && Camera.main != null && Camera.main != sceneCamera)
        {
            var titleCam = Camera.main;
            sceneCamera.transform.position   = titleCam.transform.position;
            sceneCamera.transform.rotation   = titleCam.transform.rotation;
            sceneCamera.orthographic         = titleCam.orthographic;
            sceneCamera.orthographicSize     = titleCam.orthographicSize;
            sceneCamera.nearClipPlane        = titleCam.nearClipPlane;
            sceneCamera.farClipPlane         = titleCam.farClipPlane;
            sceneCamera.backgroundColor      = titleCam.backgroundColor;

            if (_canvasScaler != null)
                _canvasScaler.targetCamera = sceneCamera;
        }

        if (transitionBlackBackground != null)
        {
            transitionBlackBackground.transform.SetParent(titleCanvas.transform, true);
            transitionBlackBackground.transform.SetAsFirstSibling();
        }
        if (transitionBackground != null)
        {
            transitionBackground.transform.SetParent(titleCanvas.transform, true);
            transitionBackground.transform.SetSiblingIndex(1);
            transitionBackground.material = new Material(transitionBackground.material);
            transitionBackground.material.SetFloat("_UVOffsetX", 1f);
        }
    }

    public void Reveal(System.Action onComplete = null)
    {
        StartCoroutine(RevealRoutine(onComplete));
    }

    private IEnumerator RevealRoutine(System.Action onComplete)
    {
        // 推镜已结束，此刻同步新相机到 TitleScene 相机的终点位置
        if (sceneCamera != null && Camera.main != null && Camera.main != sceneCamera)
        {
            var titleCam = Camera.main;
            sceneCamera.transform.position = titleCam.transform.position;
            sceneCamera.transform.rotation = titleCam.transform.rotation;
            sceneCamera.orthographicSize   = titleCam.orthographicSize;
        }

        if (sceneCamera != null)
        {
            sceneCamera.enabled = true;
            // 确保场景相机始终渲染在 Title 相机之上
            sceneCamera.depth = 1;
        }

        // 恢复 PositionNextToTitleCanvas 覆盖的 Canvas 属性
        if (!_standalone && sceneCanvas != null)
        {
            var rt = sceneCanvas.GetComponent<RectTransform>();
            rt.localScale = _origLocalScale;
            var pos = rt.position;
            pos.z = _origCanvasZ;
            rt.position = pos;
        }

        // 把黑板从 Title Canvas 移到场景 Canvas 底层（场景相机清屏是黑色，
        // 黑板随 CanvasGroup 淡入时叠加在黑底上视觉上始终是纯黑底色）
        if (!_standalone)
        {
            if (transitionBlackBackground != null)
            {
                transitionBlackBackground.transform.SetParent(sceneCanvas.transform, true);
                transitionBlackBackground.transform.SetAsFirstSibling();
            }
            if (transitionBackground != null)
            {
                transitionBackground.transform.SetParent(sceneCanvas.transform, true);
                transitionBackground.transform.SetSiblingIndex(1);
            }
        }

        // CanvasGroup 淡入场景 UI，黑板书于底层提供黑色底色
        if (_canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        // 淡入完成后移除黑板
        if (transitionBackground != null)
            Destroy(transitionBackground.gameObject);
        if (transitionBlackBackground != null)
            Destroy(transitionBlackBackground.gameObject);

        // 场景淡入完成，触发选角动画序列
        var mgr = FindFirstObjectByType<CharacterSelectManager>();
        if (mgr != null) mgr.BeginIntro();

        onComplete?.Invoke();
    }
}