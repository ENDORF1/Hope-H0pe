// 此文件仅在 Unity Editor 里使用，打包时自动排除
#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 编辑器工具：一键在当前场景里搭建完整的水面倒影系统。
///
/// 使用方式：
///   顶部菜单 → XiWang → Setup Water Reflection System
///
/// 工具会做的事：
///   1. 检查 Canvas，若为 Overlay 则提示改为 Screen Space - Camera
///   2. 创建 UI Camera（若不存在）
///   3. 创建 ReflectionCamera GameObject + Camera 组件
///   4. 创建 ReflectionContent 层（若不存在）并提示配置
///   5. 在 Canvas 里创建 WaterDisplay（RawImage）+ WaterDivider（Image）
///   6. 在 WaterDisplay 上挂 ReflectionCamera 脚本
///   7. 在 WaterDivider 上挂 WaterDivider 脚本
///   8. 输出完整的「接下来你需要手动做的事」清单
/// </summary>
public class WaterReflectionSetup : EditorWindow
{
    [MenuItem("XiWang/Setup Water Reflection System")]
    public static void ShowWindow()
    {
        GetWindow<WaterReflectionSetup>("Water Reflection Setup");
    }

    // ─────────────────────────────────────────────────
    // 引用（在 Window 里拖入）
    // ─────────────────────────────────────────────────

    private Canvas         _targetCanvas;
    private RectTransform  _switchButtonRect;
    private float          _reflectionHeightRatio = 0.4f;
    private bool           _showLog = true;

    private Vector2 _scrollPos;

    void OnGUI()
    {
        EditorGUILayout.Space(8);
        GUILayout.Label("Water Reflection System Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "此工具会自动在场景里创建水面倒影所需的所有对象。\n" +
            "请先确认 Canvas 已改为 Screen Space - Camera 模式。",
            MessageType.Info);

        EditorGUILayout.Space(6);

        _targetCanvas = (Canvas)EditorGUILayout.ObjectField(
            "目标 Canvas", _targetCanvas, typeof(Canvas), true);

        _switchButtonRect = (RectTransform)EditorGUILayout.ObjectField(
            "切换阵营按钮 RectTransform", _switchButtonRect, typeof(RectTransform), true);

        _reflectionHeightRatio = EditorGUILayout.Slider(
            "倒影区高度比例", _reflectionHeightRatio, 0.1f, 0.6f);

        _showLog = EditorGUILayout.Toggle("输出详细日志", _showLog);

        EditorGUILayout.Space(8);

        GUI.enabled = _targetCanvas != null;
        if (GUILayout.Button("▶ 一键创建水面倒影系统", GUILayout.Height(36)))
            RunSetup();
        GUI.enabled = true;

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "运行后请查看 Console 里的「下一步」清单。",
            MessageType.None);
    }

    // ─────────────────────────────────────────────────
    // 主流程
    // ─────────────────────────────────────────────────

    private void RunSetup()
    {
        Undo.SetCurrentGroupName("Setup Water Reflection");
        int group = Undo.GetCurrentGroup();

        Log("=== Water Reflection Setup 开始 ===");

        // ── 1. 检查 Canvas 模式 ──────────────────────
        if (_targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Canvas 模式警告",
                "你的 Canvas 当前是 Screen Space - Overlay。\n\n" +
                "RenderTexture 倒影需要 Screen Space - Camera 模式才能工作。\n\n" +
                "是否继续？（你需要之后手动修改 Canvas 模式）",
                "继续创建", "取消");

            if (!proceed) return;
            Log("⚠ Canvas 仍为 Overlay，请手动改为 Screen Space - Camera 并指定 UI Camera");
        }

        // ── 2. 创建 UI Camera（若不存在）──────────────
        Camera uiCam = EnsureUICamera();

        // ── 3. 创建 ReflectionCamera ─────────────────
        Camera reflCam = EnsureReflectionCamera();

        // ── 4. 创建对立阵营摄像机 ────────────────────
        Camera oppCam = EnsureOppositeCamera();

        // ── 5. 创建 WaterDisplay ──────────────────────
        RawImage waterDisplay = EnsureWaterDisplay();

        // ── 6. 创建 WaterDivider ──────────────────────
        Image waterDivider = EnsureWaterDivider();

        // ── 7. 挂载 ReflectionCamera 脚本 ─────────────
        ReflectionCamera reflScript = reflCam.GetComponent<ReflectionCamera>();
        if (reflScript == null)
            reflScript = Undo.AddComponent<ReflectionCamera>(reflCam.gameObject);

        // 通过 SerializedObject 设置引用（支持 Undo）
        SerializedObject so = new SerializedObject(reflScript);
        so.FindProperty("waterDisplay").objectReferenceValue       = waterDisplay;
        so.FindProperty("oppositeCam").objectReferenceValue        = oppCam;
        so.FindProperty("reflectionHeightRatio").floatValue        = _reflectionHeightRatio;
        if (_switchButtonRect != null)
            so.FindProperty("switchButtonRect").objectReferenceValue = _switchButtonRect;
        so.ApplyModifiedProperties();

        // ── 8. 挂载 WaterDivider 脚本 ─────────────────
        WaterDivider divScript = waterDivider.GetComponent<WaterDivider>();
        if (divScript == null)
            divScript = Undo.AddComponent<WaterDivider>(waterDivider.gameObject);

        SerializedObject soDiv = new SerializedObject(divScript);
        soDiv.FindProperty("reflectionHeightRatio").floatValue = _reflectionHeightRatio;
        soDiv.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(group);

        // ── 9. 输出下一步清单 ─────────────────────────
        PrintNextSteps(uiCam, reflCam, oppCam);

        Log("=== Setup 完成 ===");
        EditorUtility.DisplayDialog("完成", "水面倒影系统已创建！\n请查看 Console 里的「下一步」清单。", "OK");
    }

    // ─────────────────────────────────────────────────
    // 子对象创建
    // ─────────────────────────────────────────────────

    private Camera EnsureUICamera()
    {
        GameObject existing = GameObject.Find("UICamera");
        if (existing != null) return existing.GetComponent<Camera>();

        GameObject go = new GameObject("UICamera");
        Undo.RegisterCreatedObjectUndo(go, "Create UICamera");

        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.Depth;
        cam.cullingMask      = LayerMask.GetMask("UI");
        cam.orthographic     = true;
        cam.depth            = 1;
        cam.allowHDR         = false;
        cam.allowMSAA        = false;

        Log("✓ 创建了 UICamera（depth=1，只渲染 UI 层）");
        return cam;
    }

    private Camera EnsureReflectionCamera()
    {
        GameObject existing = GameObject.Find("ReflectionCamera");
        if (existing != null) return existing.GetComponent<Camera>();

        GameObject go = new GameObject("ReflectionCamera");
        Undo.RegisterCreatedObjectUndo(go, "Create ReflectionCamera");

        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags  = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.depth       = -1;
        cam.orthographic = true;

        // 默认只渲染 ReflectionContent 层（需要手动创建该层）
        int layer = LayerMask.NameToLayer("ReflectionContent");
        cam.cullingMask = layer >= 0 ? (1 << layer) : 0;

        Log("✓ 创建了 ReflectionCamera（depth=-1）");
        if (layer < 0)
            Log("⚠ ReflectionContent 层不存在，请在 Project Settings → Tags & Layers 里手动创建，然后把倒影内容对象的 Layer 设为 ReflectionContent");

        return cam;
    }

    private Camera EnsureOppositeCamera()
    {
        GameObject existing = GameObject.Find("OppositeReflectionCamera");
        if (existing != null) return existing.GetComponent<Camera>();

        GameObject go = new GameObject("OppositeReflectionCamera");
        Undo.RegisterCreatedObjectUndo(go, "Create OppositeReflectionCamera");

        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.depth           = -2;
        cam.orthographic    = true;

        int layer = LayerMask.NameToLayer("OppositeContent");
        cam.cullingMask = layer >= 0 ? (1 << layer) : 0;

        Log("✓ 创建了 OppositeReflectionCamera（depth=-2）");
        if (layer < 0)
            Log("⚠ OppositeContent 层不存在，请在 Project Settings → Tags & Layers 里手动创建，然后把对立阵营内容对象的 Layer 设为 OppositeContent");

        return cam;
    }

    private RawImage EnsureWaterDisplay()
    {
        Transform existing = _targetCanvas.transform.Find("WaterDisplay");
        if (existing != null) return existing.GetComponent<RawImage>();

        GameObject go = new GameObject("WaterDisplay", typeof(RectTransform), typeof(RawImage));
        Undo.RegisterCreatedObjectUndo(go, "Create WaterDisplay");
        go.transform.SetParent(_targetCanvas.transform, false);

        RawImage ri = go.GetComponent<RawImage>();
        ri.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, _reflectionHeightRatio);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Log("✓ 创建了 WaterDisplay（RawImage，锚点在屏幕下方）");
        Log("  → 请在 WaterDisplay 的 Material 槽里指定用 WaterSurface Shader 创建的 Material");

        return ri;
    }

    private Image EnsureWaterDivider()
    {
        Transform existing = _targetCanvas.transform.Find("WaterDivider");
        if (existing != null) return existing.GetComponent<Image>();

        GameObject go = new GameObject("WaterDivider", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create WaterDivider");
        go.transform.SetParent(_targetCanvas.transform, false);

        Image img = go.GetComponent<Image>();
        img.color         = new Color(0.29f, 0.62f, 1f, 0.9f);
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        float r = _reflectionHeightRatio;
        rt.anchorMin = new Vector2(0f, r);
        rt.anchorMax = new Vector2(1f, r);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0f, -1f);
        rt.offsetMax = new Vector2(0f,  1f);

        Log("✓ 创建了 WaterDivider（Image，位于水面分割线位置）");

        return img;
    }

    // ─────────────────────────────────────────────────
    // 下一步清单
    // ─────────────────────────────────────────────────

    private void PrintNextSteps(Camera uiCam, Camera reflCam, Camera oppCam)
    {
        Debug.Log(
            "═══════════════════════════════════════════════\n" +
            "  Water Reflection Setup —— 下一步你需要手动做：\n" +
            "═══════════════════════════════════════════════\n\n" +

            "【第一步：Canvas 模式】\n" +
            "  · 选中 Canvas → Render Mode 改为 Screen Space - Camera\n" +
            "  · Render Camera 槽拖入 UICamera\n\n" +

            "【第二步：Layer 创建】\n" +
            "  · Edit → Project Settings → Tags & Layers\n" +
            "  · 新建两个 Layer：ReflectionContent 和 OppositeContent\n\n" +

            "【第三步：摄像机 Layer Mask 确认】\n" +
            $"  · UICamera ({uiCam.name})：Culling Mask 确保包含 UI，排除 ReflectionContent 和 OppositeContent\n" +
            $"  · ReflectionCamera ({reflCam.name})：Culling Mask 只保留 ReflectionContent\n" +
            $"  · OppositeReflectionCamera ({oppCam.name})：Culling Mask 只保留 OppositeContent\n\n" +

            "【第四步：倒影内容场景层】\n" +
            "  · 在 Hierarchy 里新建一套倒影用的 UI 内容（可以是现有 Content 的副本）\n" +
            "  · 把所有倒影对象的 Layer 设为 ReflectionContent\n" +
            "  · 对立阵营预览对象的 Layer 设为 OppositeContent\n\n" +

            "【第五步：WaterDisplay Material】\n" +
            "  · Project 里右键 → Create → Material，Shader 选 XiWang/WaterSurface\n" +
            "  · 把这个 Material 拖入 WaterDisplay 的 Material 槽\n\n" +

            "【第六步：ReflectionCamera 参数】\n" +
            "  · 选中 ReflectionCamera，检查 ReflectionCamera 脚本的 Inspector\n" +
            "  · 调整波纹参数 / 倒影透明度等至你满意\n\n" +

            "【第七步（可选）：联动 TitleScreenManager】\n" +
            "  · 切换阵营时调用 ReflectionCamera.Instance?.SetPreviewBlend(0) 重置预览\n\n" +

            "═══════════════════════════════════════════════"
        );
    }

    // ─────────────────────────────────────────────────
    // 工具
    // ─────────────────────────────────────────────────

    private void Log(string msg)
    {
        if (_showLog) Debug.Log($"[WaterReflectionSetup] {msg}");
    }
}

#endif
