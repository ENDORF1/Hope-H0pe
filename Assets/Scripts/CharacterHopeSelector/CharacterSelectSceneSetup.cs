#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Hope_CharacterSelect 场景搭建工具。
///
/// 菜单：XiWang → Setup → Build CharacterSelect Scene
///
/// 搭建内容：
///   Canvas（World Space，与 TitleScene 一致）
///     ├─ Background（背景图占位，你替换 sprite）
///     ├─ RippleLayer（涟漪控制器）
///     ├─ Header（标题区）
///     │    ├─ FactionTag
///     │    ├─ MainTitle
///     │    ├─ TitleUnderline（蓝色下划线，初始宽度0）
///     │    └─ SubTitle
///     ├─ CardStage（卡牌生成容器）
///     ├─ PromptText（底部提示）
///     ├─ TransitionOverlay（过渡遮罩）
///     │    └─ TransitionText
///     └─ FlashImage（全屏白色闪光）
///
///   CharacterSelectManager（挂在 Manager 空对象上）
///   MainCamera
///
/// 需要手动填入 Inspector：
///   - CharacterSelectManager.cardPrefab（角色卡 Prefab）
///   - CharacterSelectManager.characters（CharacterAsset 列表）
///   - RippleController.ripplePrefab（涟漪圈 Prefab，可选）
/// </summary>
public class CharacterSelectSceneSetup : EditorWindow
{
    private Camera _mainCamera;
    private float  _referenceWidth  = 2560f;
    private float  _referenceHeight = 1440f;

    [MenuItem("XiWang/Setup/Build CharacterSelect Scene")]
    public static void ShowWindow()
    {
        var w = GetWindow<CharacterSelectSceneSetup>("选角场景搭建");
        w.minSize = new Vector2(380, 260);
        w.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Hope_CharacterSelect 场景搭建", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        _mainCamera = (Camera)EditorGUILayout.ObjectField(
            "主相机（可不填，自动创建）", _mainCamera, typeof(Camera), true);

        EditorGUILayout.Space(4);
        _referenceWidth  = EditorGUILayout.FloatField("参考分辨率 宽", _referenceWidth);
        _referenceHeight = EditorGUILayout.FloatField("参考分辨率 高", _referenceHeight);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "将在当前场景中搭建完整的选角界面结构。\n\n" +
            "搭建完成后需手动填写：\n" +
            "  · CharacterSelectManager → cardPrefab\n" +
            "  · CharacterSelectManager → characters\n" +
            "  · RippleController → ripplePrefab（可选）",
            MessageType.Info);

        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.4f, 0.75f, 1f);
        bool clicked = GUILayout.Button("▶  一键搭建选角场景", GUILayout.Height(42));
        GUI.backgroundColor = Color.white;

        if (clicked) Build();
    }

    // ─────────────────────────────────────────────────
    void Build()
    {
        try
        {
            // 1. 主相机
            Camera cam = EnsureMainCamera();

            // 2. Canvas
            Canvas canvas = CreateCanvas(cam);
            RectTransform canvasRT = canvas.GetComponent<RectTransform>();

            // 3. Canvas 内部结构
            RawImage    bgImage       = CreateBackground(canvasRT);
            RippleController ripple   = CreateRippleLayer(canvasRT);
            CreateNoiseLayer(canvasRT);
            CreateHeader(canvasRT, out RectTransform underline, out TextMeshProUGUI subTitle);
            RectTransform cardStage   = CreateCardStage(canvasRT);
            TextMeshProUGUI prompt    = CreatePromptText(canvasRT);
            CanvasGroup overlay; TextMeshProUGUI overlayText;
            CreateTransitionOverlay(canvasRT, out overlay, out overlayText);
            Image flashImg            = CreateFlashImage(canvasRT);

            // 4. SceneEntry（转场入口，控制场景定位和淡入）
            CreateSceneEntry(canvasRT);

            // 5. Manager 对象
            CreateManager(canvas, cardStage, underline, subTitle,
                          prompt, overlay, overlayText, flashImg, ripple);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("完成",
                "选角场景搭建完毕！\n\n" +
                "请在 CharacterSelectManager 的 Inspector 中填写：\n" +
                "  · Card Prefab\n" +
                "  · Characters 列表",
                "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[CharSelectSetup] 失败: " + e.Message + "\n" + e.StackTrace);
            EditorUtility.DisplayDialog("错误", e.Message, "OK");
        }
    }

    // ─────────────────────────────────────────────────
    // 主相机
    // ─────────────────────────────────────────────────

    Camera EnsureMainCamera()
    {
        if (_mainCamera != null) return _mainCamera;

        var existing = Camera.main;
        if (existing != null) return existing;

        var go = new GameObject("Main Camera");
        Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = ToLinear(0.040f, 0.040f, 0.040f, 1f); // HTML #080808
        cam.orthographic    = true;
        go.transform.position = new Vector3(0, 0, -10f);
        return cam;
    }

    // ─────────────────────────────────────────────────
    // Canvas（World Space，与 TitleScene 保持一致）
    // ─────────────────────────────────────────────────

    Canvas CreateCanvas(Camera cam)
    {
        // 检查是否已存在
        var existing = Object.FindFirstObjectByType<Canvas>();
        if (existing != null)
        {
            Debug.Log("[CharSelectSetup] 场景中已有 Canvas，跳过创建。");
            return existing;
        }

        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                                          typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Create Canvas");

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = cam;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta  = new Vector2(_referenceWidth, _referenceHeight);
        rt.localScale = Vector3.one * (1f / _referenceHeight * cam.orthographicSize * 2f);

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(_referenceWidth, _referenceHeight);
        scaler.enabled             = false; // World Space 下禁用，手动控制

        return canvas;
    }

    // ─────────────────────────────────────────────────
    // 背景图占位
    // ─────────────────────────────────────────────────

    RawImage CreateBackground(RectTransform parent)
    {
        var go = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
        Undo.RegisterCreatedObjectUndo(go, "Create Background");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.SetAsFirstSibling();
        SetFullStretch(rt);

        var img = go.GetComponent<RawImage>();
        img.color         = ToLinear(0.040f, 0.040f, 0.040f, 1f);
        img.raycastTarget = false;

        // 说明注释：之后替换 texture 为你的背景图
        go.name = "Background [替换为你的背景图]";
        return img;
    }

    // ─────────────────────────────────────────────────
    // 涟漪层
    // ─────────────────────────────────────────────────

    RippleController CreateRippleLayer(RectTransform parent)
    {
        var go = new GameObject("RippleLayer", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create RippleLayer");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.SetSiblingIndex(1);
        SetFullStretch(rt);

        var ripple = go.AddComponent<RippleController>();
        // 默认参数已在脚本里设置好，ripplePrefab 留空由用户填
        return ripple;
    }

    // ─────────────────────────────────────────────────
    // 标题区
    // ─────────────────────────────────────────────────

    void CreateHeader(RectTransform parent,
                      out RectTransform underline,
                      out TextMeshProUGUI subTitle)
    {
        // Header 容器
        var headerGO = new GameObject("Header", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(headerGO, "Create Header");
        var headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.SetParent(parent, false);
        // 左上角锚点
        headerRT.anchorMin = new Vector2(0f, 0.65f);
        headerRT.anchorMax = new Vector2(0.5f, 1f);
        headerRT.offsetMin = new Vector2(44f, 0f);
        headerRT.offsetMax = new Vector2(0f, -28f);

        // FactionTag
        var tagGO = new GameObject("FactionTag", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(tagGO, "Create FactionTag");
        tagGO.transform.SetParent(headerRT, false);
        var tagRT  = tagGO.GetComponent<RectTransform>();
        tagRT.anchorMin    = new Vector2(0f, 0.75f);
        tagRT.anchorMax    = new Vector2(1f, 1f);
        tagRT.offsetMin    = Vector2.zero;
        tagRT.offsetMax    = Vector2.zero;
        var tagTMP = tagGO.GetComponent<TextMeshProUGUI>();
        tagTMP.text      = "— 救世方 · Hope Faction —";
        tagTMP.fontSize  = 22f;
        tagTMP.color     = new Color(1f, 1f, 1f, 0.2f);
        tagTMP.alignment = TextAlignmentOptions.Left;

        // MainTitle
        var titleGO = new GameObject("MainTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(titleGO, "Create MainTitle");
        titleGO.transform.SetParent(headerRT, false);
        var titleRT  = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.35f);
        titleRT.anchorMax = new Vector2(1f, 0.78f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        var titleTMP = titleGO.GetComponent<TextMeshProUGUI>();
        titleTMP.text      = "选择角色";
        titleTMP.fontSize  = 86f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color     = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Left;

        // TitleShadow（模拟 HTML text-shadow: 4px 4px 0 #000）
        // 放在 MainTitle 下面作为第一个子节点
        var shadowGO = new GameObject("TitleShadow", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(shadowGO, "Create TitleShadow");
        shadowGO.transform.SetParent(headerRT, false);
        shadowGO.transform.SetSiblingIndex(titleGO.transform.GetSiblingIndex()); // 紧贴 MainTitle 前面
        var shadowRT  = shadowGO.GetComponent<RectTransform>();
        shadowRT.anchorMin = new Vector2(0f, 0.35f);
        shadowRT.anchorMax = new Vector2(1f, 0.78f);
        shadowRT.offsetMin = new Vector2(3f, -3f); // 右下偏移
        shadowRT.offsetMax = new Vector2(3f, -3f);
        var shadowTMP = shadowGO.GetComponent<TextMeshProUGUI>();
        shadowTMP.text      = "选择角色";
        shadowTMP.fontSize  = 86f;
        shadowTMP.fontStyle = FontStyles.Bold;
        shadowTMP.color     = new Color(0f, 0f, 0f, 0.8f);
        shadowTMP.alignment = TextAlignmentOptions.Left;
        shadowTMP.raycastTarget = false;

        // TitleUnderline
        var lineGO = new GameObject("TitleUnderline", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(lineGO, "Create TitleUnderline");
        lineGO.transform.SetParent(headerRT, false);
        underline = lineGO.GetComponent<RectTransform>();
        underline.anchorMin = new Vector2(0f, 0.28f);
        underline.anchorMax = new Vector2(0f, 0.28f);
        underline.pivot     = new Vector2(0f, 0.5f);
        underline.sizeDelta = new Vector2(300f, 4f); // Editor可见，运行时从0展开
        var lineImg = lineGO.GetComponent<Image>();
        lineImg.color        = ToLinear(0.118f, 0.565f, 1f, 1f); // Hope蓝
        lineImg.raycastTarget = false;

        // SubTitle
        var subGO = new GameObject("SubTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(subGO, "Create SubTitle");
        subGO.transform.SetParent(headerRT, false);
        var subRT = subGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0f, 0f);
        subRT.anchorMax = new Vector2(1f, 0.28f);
        subRT.offsetMin = Vector2.zero;
        subRT.offsetMax = Vector2.zero;
        subTitle = subGO.GetComponent<TextMeshProUGUI>();
        subTitle.text      = "Make a wish · and the world answers";
        subTitle.fontSize  = 18f;
        subTitle.color     = ToLinear(0.118f, 0.565f, 1f, 0.27f); // Editor可见，运行时从0淡入
        subTitle.alignment = TextAlignmentOptions.Left;
    }

    // ─────────────────────────────────────────────────
    // 卡牌舞台
    // ─────────────────────────────────────────────────

    RectTransform CreateCardStage(RectTransform parent)
    {
        var go = new GameObject("CardStage", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create CardStage");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        // 占据画面中下区域，卡牌从这里的中心展开
        rt.anchorMin    = new Vector2(0f, 0.1f);
        rt.anchorMax    = new Vector2(1f, 0.85f);
        rt.offsetMin    = Vector2.zero;
        rt.offsetMax    = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    // ─────────────────────────────────────────────────
    // 底部提示
    // ─────────────────────────────────────────────────

    TextMeshProUGUI CreatePromptText(RectTransform parent)
    {
        var go = new GameObject("PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create PromptText");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 30f);
        rt.sizeDelta        = new Vector2(800f, 40f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text      = "点击卡牌出战  ·  Click to deploy";
        tmp.fontSize  = 20f;
        tmp.color     = new Color(1f, 1f, 1f, 0.13f); // Editor可见，运行时从0淡入
        tmp.alignment = TextAlignmentOptions.Center;

        // 挂载 GlitchText 用于 Hover 切换乱码效果
        go.AddComponent<GlitchText>();

        return tmp;
    }

    // ─────────────────────────────────────────────────
    // 过渡遮罩
    // ─────────────────────────────────────────────────

    void CreateTransitionOverlay(RectTransform parent,
                                  out CanvasGroup group,
                                  out TextMeshProUGUI text)
    {
        var go = new GameObject("TransitionOverlay",
                                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(go, "Create TransitionOverlay");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        SetFullStretch(rt);

        var img = go.GetComponent<Image>();
        img.color = ToLinear(0.040f, 0.040f, 0.040f, 1f);

        group                = go.GetComponent<CanvasGroup>();
        group.alpha          = 0f;
        group.blocksRaycasts = false;

        // 遮罩上的文字
        var txtGO = new GameObject("TransitionText",
                                    typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(txtGO, "Create TransitionText");
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.SetParent(rt, false);
        txtRT.anchorMin        = new Vector2(0.5f, 0.5f);
        txtRT.anchorMax        = new Vector2(0.5f, 0.5f);
        txtRT.anchoredPosition = Vector2.zero;
        txtRT.sizeDelta        = new Vector2(800f, 80f);

        text           = txtGO.GetComponent<TextMeshProUGUI>();
        text.text      = "ENTERING BATTLE";
        text.fontSize  = 40f;
        text.fontStyle = FontStyles.Bold;
        text.color     = ToLinear(0.118f, 0.565f, 1f, 0f);
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 10f;
    }

    // ─────────────────────────────────────────────────
    // 白色闪光
    // ─────────────────────────────────────────────────

    Image CreateFlashImage(RectTransform parent)
    {
        var go = new GameObject("FlashImage", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create FlashImage");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        SetFullStretch(rt);

        var img = go.GetComponent<Image>();
        img.color         = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = false;
        return img;
    }

    // ─────────────────────────────────────────────────
    // SceneEntry（转场入口，控制场景定位 + 淡入 + 黑底过渡）
    // ─────────────────────────────────────────────────

    void CreateSceneEntry(RectTransform canvasParent)
    {
        // 检查是否已存在
        var existing = Object.FindFirstObjectByType<CharacterSelectEntry>();
        if (existing != null)
        {
            Debug.Log("[CharSelectSetup] CharacterSelectEntry 已存在，跳过创建。");
            return;
        }

        var go = new GameObject("CharacterSelectEntry");
        Undo.RegisterCreatedObjectUndo(go, "Create CharacterSelectEntry");
        var entry = go.AddComponent<CharacterSelectEntry>();

        // ── TransitionBackground（UV 滚动黑底，转场时叠在 Title 画布上）──
        var bgGO = new GameObject("TransitionBackground", typeof(RectTransform), typeof(RawImage));
        Undo.RegisterCreatedObjectUndo(bgGO, "Create TransitionBackground");
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.SetParent(canvasParent, false);
        SetFullStretch(bgRT);
        var bgImg = bgGO.GetComponent<RawImage>();
        bgImg.color         = new Color(0f, 0f, 0f, 0f); // Edit模式透明；运行时Awake设为1
        bgImg.raycastTarget = false;

        // ── TransitionBlackBackground（纯黑板，转场初始遮挡）──
        var blackGO = new GameObject("TransitionBlackBackground", typeof(RectTransform), typeof(RawImage));
        Undo.RegisterCreatedObjectUndo(blackGO, "Create TransitionBlackBackground");
        var blackRT = blackGO.GetComponent<RectTransform>();
        blackRT.SetParent(canvasParent, false);
        SetFullStretch(blackRT);
        var blackImg = blackGO.GetComponent<RawImage>();
        blackImg.color         = new Color(0f, 0f, 0f, 0f); // Edit模式透明；运行时Awake设为1
        blackImg.raycastTarget = false;

        // 绑定引用
        var so = new SerializedObject(entry);
        so.FindProperty("transitionBackground").objectReferenceValue      = bgImg;
        so.FindProperty("transitionBlackBackground").objectReferenceValue = blackImg;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
        Debug.Log("[CharSelectSetup] CharacterSelectEntry 已创建并绑定转场黑底。");
    }

    // ─────────────────────────────────────────────────
    // Manager 对象
    // ─────────────────────────────────────────────────

    void CreateManager(Canvas canvas,
                        RectTransform cardStage,
                        RectTransform underline,
                        TextMeshProUGUI subTitle,
                        TextMeshProUGUI prompt,
                        CanvasGroup overlay,
                        TextMeshProUGUI overlayText,
                        Image flash,
                        RippleController ripple)
    {
        var go = new GameObject("CharacterSelectManager");
        Undo.RegisterCreatedObjectUndo(go, "Create CharacterSelectManager");

        var mgr = go.AddComponent<CharacterSelectManager>();

        // 用 SerializedObject 填写引用
        var so = new SerializedObject(mgr);
        so.FindProperty("cardStage").objectReferenceValue         = cardStage;
        so.FindProperty("titleUnderline").objectReferenceValue    = underline;
        so.FindProperty("subTitle").objectReferenceValue          = subTitle;
        so.FindProperty("promptText").objectReferenceValue        = prompt;
        so.FindProperty("transitionOverlay").objectReferenceValue = overlay;
        so.FindProperty("transitionText").objectReferenceValue    = overlayText;
        so.FindProperty("flashImage").objectReferenceValue        = flash;
        so.FindProperty("rippleController").objectReferenceValue  = ripple;
        so.FindProperty("battleSceneName").stringValue            = "Battle Scene";
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
        Debug.Log("[CharSelectSetup] Manager 已创建并完成引用绑定。");
    }

    // ─────────────────────────────────────────────────
    // 噪点层（模拟 HTML 4% 不透明度噪点 canvas）
    // ─────────────────────────────────────────────────

    void CreateNoiseLayer(RectTransform parent)
    {
        var go = new GameObject("NoiseLayer", typeof(RectTransform), typeof(RawImage));
        Undo.RegisterCreatedObjectUndo(go, "Create NoiseLayer");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.SetSiblingIndex(2); // 在 RippleLayer 之上，Header 之下
        SetFullStretch(rt);

        var img = go.GetComponent<RawImage>();
        img.texture = GenerateNoiseTexture();
        // 平铺噪点纹理以匹配 2560×1440 参考分辨率，每个噪点 ≈1px
        img.uvRect = new Rect(0f, 0f,
            _referenceWidth  / 256f,
            _referenceHeight / 256f);
        img.color        = new Color(1f, 1f, 1f, 0.014f); // Linear空间：等效sRGB ~0.08
        img.raycastTarget = false;
    }

    // ─────────────────────────────────────────────────
    // 程序化纹理生成
    // ─────────────────────────────────────────────────

    Texture2D GenerateNoiseTexture()
    {
        // 每次重新生成（不缓存），保证噪点纹理质量
        EnsureProceduralTexturesFolder();

        var tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                float v = Random.value;
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        tex.Apply();

        // 覆盖旧文件
        string path = NoiseTexPath;
        var old = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (old != null)
        {
            EditorUtility.CopySerialized(tex, old);
            Object.DestroyImmediate(tex);
            return old;
        }

        AssetDatabase.CreateAsset(tex, path);
        AssetDatabase.SaveAssets();
        return tex;
    }

    void EnsureProceduralTexturesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Textures"))
            AssetDatabase.CreateFolder("Assets", "Textures");
        if (!AssetDatabase.IsValidFolder("Assets/Textures/Procedural"))
            AssetDatabase.CreateFolder("Assets/Textures", "Procedural");
    }

    static readonly string NoiseTexPath = "Assets/Textures/Procedural/NoiseTexture.asset";

    // ─────────────────────────────────────────────────
    // 色彩空间工具（项目使用 Linear 空间，sRGB 值需转换）
    // ─────────────────────────────────────────────────

    static float ToLinear(float srgb)
    {
        if (srgb <= 0.04045f) return srgb / 12.92f;
        return Mathf.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }

    static Color ToLinear(float r, float g, float b, float a = 1f)
    {
        return new Color(ToLinear(r), ToLinear(g), ToLinear(b), a);
    }

    // ─────────────────────────────────────────────────
    // 工具方法
    // ─────────────────────────────────────────────────

    static void SetFullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
