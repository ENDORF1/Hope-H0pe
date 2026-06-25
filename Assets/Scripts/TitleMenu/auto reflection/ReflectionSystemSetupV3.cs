#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 水面倒影系统 V3 — World Space Canvas + 双相机
///
/// 原理：
///   World Space Canvas 存在于三维空间，任何相机都可以渲染它。
///   MainCamera  → 渲染 Canvas → 屏幕
///   ReflCamera  → 渲染 Canvas → RenderTexture → WaterReflectionImage
///
/// 菜单：XiWang → Setup → Build Reflection System V3
/// </summary>
public class ReflectionSystemSetupV3 : EditorWindow
{
    private Canvas        _mainCanvas;
    private Camera        _mainCamera;
    private RectTransform _buttonContainer;

    private float _resolutionScale = 1f;
    private bool  _createWaterLine = true;

    private Vector2 _scroll;

    [MenuItem("XiWang/Setup/Build Reflection System V3")]
    public static void ShowWindow()
    {
        var w = GetWindow<ReflectionSystemSetupV3>("倒影系统 V3");
        w.minSize = new Vector2(420, 420);
        w.Show();
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Label("水面倒影系统 V3 — World Space Canvas", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        _mainCanvas      = (Canvas)EditorGUILayout.ObjectField(
            "主 Canvas", _mainCanvas, typeof(Canvas), true);
        _mainCamera      = (Camera)EditorGUILayout.ObjectField(
            "主相机", _mainCamera, typeof(Camera), true);
        _buttonContainer = (RectTransform)EditorGUILayout.ObjectField(
            "ButtonContainer（可选，排除出倒影）", _buttonContainer, typeof(RectTransform), true);

        EditorGUILayout.Space(8);
        _resolutionScale = EditorGUILayout.Slider("RT 分辨率缩放", _resolutionScale, 0.25f, 1f);
        _createWaterLine = EditorGUILayout.Toggle("创建动态水面线", _createWaterLine);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "此工具会：\n" +
            "1. 把主 Canvas 改为 World Space 模式\n" +
            "2. 挂 WorldSpaceCanvasScaler 保持分辨率自适应\n" +
            "3. 创建 ReflCamera（渲染同一 Canvas → RT）\n" +
            "4. 在 Canvas 底层创建 WaterReflectionImage\n" +
            "5. 创建 WaterReflectionMat（需 WaterReflection.shader 已导入）",
            MessageType.Info);

        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
        bool clicked = GUILayout.Button("▶  一键搭建 V3", GUILayout.Height(42));
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();

        if (clicked) Build();
    }

    void Build()
    {
        if (_mainCanvas == null || _mainCamera == null)
        {
            EditorUtility.DisplayDialog("缺少引用", "请填写主 Canvas 和主相机", "OK");
            return;
        }

        try
        {
            // 0. 清理历史残留
            CleanupLegacy();

            // 1. Canvas → World Space
            ConvertCanvasToWorldSpace();

            // 2. 创建 ReflCamera
            var reflCam = CreateReflCamera();

            // 3. Material
            var mat = CreateOrGetMaterial();

            // 4. WaterReflectionImage
            CreateWaterReflectionDisplay(mat, reflCam);

            // 5. 水面线
            if (_createWaterLine) CreateWaterSurfaceLine();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("完成",
                "V3 搭建完毕！\n\nPlay 查看效果。\n如画面偏移，调整 WorldSpaceCanvasScaler 的 referenceResolution。",
                "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ReflV3] 失败: " + e.Message + "\n" + e.StackTrace);
            EditorUtility.DisplayDialog("错误", e.Message, "OK");
        }
    }

    // ── Step 1: Canvas → World Space ────────────────────
    void ConvertCanvasToWorldSpace()
    {
        Undo.RecordObject(_mainCanvas, "Convert Canvas to World Space");

        // 记录原 CanvasScaler 参数
        var scaler = _mainCanvas.GetComponent<CanvasScaler>();
        Vector2 refRes = new Vector2(2560, 1440);
        if (scaler != null)
        {
            refRes = scaler.referenceResolution;
            // 禁用旧 CanvasScaler（不删除，保留参数备查）
            scaler.enabled = false;
        }

        // 切换到 World Space
        _mainCanvas.renderMode = RenderMode.WorldSpace;
        _mainCanvas.worldCamera = _mainCamera;

        var rt = _mainCanvas.GetComponent<RectTransform>();
        rt.sizeDelta  = refRes;
        rt.localScale = Vector3.one;

        // 挂 WorldSpaceCanvasScaler
        var wsScaler = _mainCanvas.GetComponent<WorldSpaceCanvasScaler>();
        if (wsScaler == null)
            wsScaler = _mainCanvas.gameObject.AddComponent<WorldSpaceCanvasScaler>();

        wsScaler.referenceResolution = refRes;
        wsScaler.targetCamera        = _mainCamera;
        wsScaler.UpdateSize();

        EditorUtility.SetDirty(_mainCanvas.gameObject);
        Debug.Log("[ReflV3] Canvas 已切换为 World Space");
    }

    // ── Step 2: ReflCamera ──────────────────────────────
    ReflectionCamera CreateReflCamera()
    {
        GameObject go = GameObject.Find("ReflCamera");
        if (go == null)
        {
            go = new GameObject("ReflCamera");
            Undo.RegisterCreatedObjectUndo(go, "Create ReflCamera");
        }

        // 相机组件
        Camera cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();

        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.depth           = _mainCamera.depth - 1;
        cam.cullingMask     = _mainCamera.cullingMask; // 渲染同样的层
        cam.orthographic        = _mainCamera.orthographic;
        cam.orthographicSize    = _mainCamera.orthographicSize;
        cam.nearClipPlane       = _mainCamera.nearClipPlane;
        cam.farClipPlane        = _mainCamera.farClipPlane;
        go.transform.position   = _mainCamera.transform.position;
        go.transform.rotation   = _mainCamera.transform.rotation;

        // ReflectionCamera 脚本
        var reflCam = go.GetComponent<ReflectionCamera>();
        if (reflCam == null) reflCam = go.AddComponent<ReflectionCamera>();

        var so = new SerializedObject(reflCam);
        so.FindProperty("mainCamera").objectReferenceValue = _mainCamera;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(go);

        Debug.Log("[ReflV3] ReflCamera 已创建");
        return reflCam;
    }

    // ── Step 3: WaterReflectionImage ────────────────────
    void CreateWaterReflectionDisplay(Material mat, ReflectionCamera reflCam)
    {
        var canvasRT = _mainCanvas.GetComponent<RectTransform>();

        // ReflectionContainer
        Transform ctrTr = canvasRT.Find("ReflectionContainer");
        RectTransform ctrRT;
        if (ctrTr != null)
        {
            ctrRT = ctrTr as RectTransform;
        }
        else
        {
            var go = new GameObject("ReflectionContainer", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create ReflectionContainer");
            ctrRT = go.GetComponent<RectTransform>();
            ctrRT.SetParent(canvasRT, false);
        }

        ctrRT.anchorMin = new Vector2(0f, 0f);
        ctrRT.anchorMax = new Vector2(1f, 0.5f);
        ctrRT.offsetMin = Vector2.zero;
        ctrRT.offsetMax = Vector2.zero;
        ctrRT.SetAsFirstSibling(); // 最底层，不遮挡按钮

        // WaterReflectionImage
        Transform imgTr = ctrRT.Find("WaterReflectionImage");
        RectTransform imgRT;
        RawImage rawImg;
        if (imgTr != null)
        {
            imgRT  = imgTr as RectTransform;
            rawImg = imgTr.GetComponent<RawImage>();
        }
        else
        {
            var imgGO = new GameObject("WaterReflectionImage",
                                       typeof(RectTransform), typeof(RawImage));
            Undo.RegisterCreatedObjectUndo(imgGO, "Create WaterReflectionImage");
            imgRT  = imgGO.GetComponent<RectTransform>();
            rawImg = imgGO.GetComponent<RawImage>();
            imgRT.SetParent(ctrRT, false);
        }

        imgRT.anchorMin      = Vector2.zero;
        imgRT.anchorMax      = Vector2.one;
        imgRT.offsetMin      = Vector2.zero;
        imgRT.offsetMax      = Vector2.zero;
        rawImg.raycastTarget = false;
        if (mat != null) rawImg.material = mat;

        // ReflectionWaterRenderer
        var renderer = imgRT.GetComponent<ReflectionWaterRenderer>();
        if (renderer == null)
            renderer = imgRT.gameObject.AddComponent<ReflectionWaterRenderer>();

        var so = new SerializedObject(renderer);
        so.FindProperty("reflectionCamera").objectReferenceValue = reflCam;
        so.FindProperty("waterMaterial").objectReferenceValue    = mat;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(renderer);
    }

    // ── Step 4: 水面线 ───────────────────────────────────
    void CreateWaterSurfaceLine()
    {
        var canvasRT = _mainCanvas.GetComponent<RectTransform>();
        Transform ctr = canvasRT.Find("ReflectionContainer");
        if (ctr == null) return;

        Transform surfTr = ctr.Find("WaterSurfaceLine");
        RectTransform surfRT;
        if (surfTr != null)
        {
            surfRT = surfTr as RectTransform;
        }
        else
        {
            var go = new GameObject("WaterSurfaceLine", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create WaterSurfaceLine");
            surfRT = go.GetComponent<RectTransform>();
            surfRT.SetParent(ctr, false);
        }

        surfRT.anchorMin = new Vector2(0f, 1f);
        surfRT.anchorMax = new Vector2(1f, 1f);
        surfRT.pivot     = new Vector2(0.5f, 0.5f);
        surfRT.offsetMin = new Vector2(0f, -6f);
        surfRT.offsetMax = new Vector2(0f,  6f);

        if (surfRT.GetComponent<WaterSurface>() == null)
            surfRT.gameObject.AddComponent<WaterSurface>();
    }

    // ── Material ─────────────────────────────────────────
    Material CreateOrGetMaterial()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        const string path  = "Assets/Materials/WaterReflectionMat.mat";
        const string shaderName = "XiWang/WaterReflection";

        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning("[ReflV3] 找不到 XiWang/WaterReflection，用 UI/Default 暂替。");
            shader = Shader.Find("UI/Default");
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = "WaterReflectionMat" };
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
        }
        else
        {
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }
    // ── 清理历史残留 ─────────────────────────────────────
    void CleanupLegacy()
    {
        // 修复主相机被之前 Setup 脚本破坏的字段
        if (_mainCamera != null)
        {
            if (_mainCamera.targetTexture != null)
            {
                _mainCamera.targetTexture = null;
                Debug.Log("[ReflV3] 已清除主相机的 Target Texture");
            }
            if (_mainCamera.cullingMask == 0)
            {
                _mainCamera.cullingMask = ~0; // Everything
                Debug.Log("[ReflV3] 已恢复主相机的 Culling Mask 为 Everything");
            }
            if (_mainCamera.depth < -1)
            {
                _mainCamera.depth = 0;
                Debug.Log("[ReflV3] 已恢复主相机的 Depth 为 0");
            }
            EditorUtility.SetDirty(_mainCamera.gameObject);
        }

        // 删除旧的 ReflectionCapture 脚本（V1 残留）
        if (_mainCamera != null)
        {
            var cap = _mainCamera.GetComponent<ReflectionCapture>();
            if (cap != null)
            {
                DestroyImmediate(cap);
                Debug.Log("[ReflV3] 已删除主相机上的 ReflectionCapture（V1残留）");
            }
        }

        // 删除旧的独立相机对象
        foreach (var name in new[] { "ReflectionSourceCamera", "ReflCanvas" })
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                DestroyImmediate(go);
                Debug.Log($"[ReflV3] 已删除旧对象: {name}");
            }
        }
    }

}
#endif