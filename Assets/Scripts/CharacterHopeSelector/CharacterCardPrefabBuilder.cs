#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// 角色卡 Prefab 搭建工具。
///
/// 菜单：XiWang → Setup → Build CharacterCard Prefab
///
/// 生成的 Prefab 结构（400 × 600）：
///
///   CharacterCard（Root，400×600，挂 CharacterCardUI + CanvasGroup）
///     ├─ GlowBorder（发光边框 Image，inset -8，初始透明）
///     └─ TiltRoot（倾斜容器 RectTransform，Hover 3D 倾斜作用在这里）
///          ├─ CardBack（卡背根节点，翻面后隐藏）
///          │    ├─ BackBG（深色背景 Image）
///          │    ├─ BackGrid（网格线 Image，低透明度）
///          │    ├─ BackText_Main（"MAKE A WISH" TMP，倾斜，蓝色）
///          │    ├─ BackText_Sub（"Hope · Faction" TMP，小字）
///          │    └─ BackInkLine（蓝色墨水线装饰 Image）
///          └─ CardFace（卡面根节点）
///               ├─ PortraitArea（立绘区，高420）
///               │    ├─ PortraitBG（底色 Image）
///               │    ├─ PortraitImage（立绘 Image，替换 Sprite）
///               │    ├─ PortraitGradient（底部渐变遮罩 Image）
///               │    └─ HPBadge（左上角HP角标）
///               │         ├─ HPBadgeBG（Image）
///               │         └─ HPText（TMP）
///               └─ InfoArea（信息栏，高180）
///                    ├─ InfoBG（深色背景 Image）
///                    ├─ TopDivider（顶部蓝色分割线 Image）
///                    ├─ NameText（角色名 TMP，大字）
///                    ├─ RoleText（职位 TMP，小字）
///                    └─ HPBarRow（HP条行）
///                         ├─ HPLabel（"HP" TMP）
///                         ├─ HPBarBG（底条 Image）
///                         │    └─ HPBarFill（填充 Image）
///                         └─ HPValue（数值 TMP）
///
/// 保存路径：Assets/Prefabs/HopeCharacters/CharacterCard.prefab
/// </summary>
public class CharacterCardPrefabBuilder : EditorWindow
{
    private string _savePath = "Assets/Prefabs/HopeCharacters";
    private Color  _frameColor = new Color(0.247f, 0.247f, 0.247f, 1f); // #3F3F3F
    private TMP_FontAsset _nameFont;
    private TMP_FontAsset _roleFont;

    [MenuItem("XiWang/Setup/Build CharacterCard Prefab")]
    public static void ShowWindow()
    {
        var w = GetWindow<CharacterCardPrefabBuilder>("角色卡 Prefab 搭建");
        w.minSize = new Vector2(360, 200);
        w.AutoLoadFonts();
        w.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("角色卡 Prefab 搭建（400 × 600）", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        _savePath = EditorGUILayout.TextField("保存路径", _savePath);
        _frameColor = EditorGUILayout.ColorField("内边框颜色", _frameColor);
        _nameFont = (TMP_FontAsset)EditorGUILayout.ObjectField("角色名字体", _nameFont, typeof(TMP_FontAsset), false);
        _roleFont = (TMP_FontAsset)EditorGUILayout.ObjectField("职位/副标题字体", _roleFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "生成后需手动完成：\n" +
            "  · 替换 PortraitImage 的 Sprite 为角色立绘\n" +
            "  · 把生成的 Prefab 拖入 CharacterSelectManager → Card Prefab",
            MessageType.Info);

        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.4f, 0.75f, 1f);
        bool clicked = GUILayout.Button("▶  一键生成 CharacterCard Prefab", GUILayout.Height(42));
        GUI.backgroundColor = Color.white;

        if (clicked) Build();
    }

    void AutoLoadFonts()
    {
        if (_nameFont == null)
            _nameFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/PermanentMarker SDF.asset");
        if (_roleFont == null)
            _roleFont = _nameFont;
    }

    // ─────────────────────────────────────────────────
    void Build()
    {
        AutoLoadFonts();

        // 确保目录存在
        if (!AssetDatabase.IsValidFolder(_savePath))
        {
            // 递归创建文件夹
            string[] parts = _savePath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // ── 颜色常量（Hope蓝 + 暗色系）──
        Color hopeBlue      = ToLinear(0.118f, 0.565f, 1f,  1f);   // HTML #1e90ff
        Color hopeBlueAlpha = ToLinear(0.118f, 0.565f, 1f,  0f);   // 初始透明
        Color darkBg        = ToLinear(0.051f, 0.051f, 0.051f, 1f); // HTML #0d0d0d
        Color darkerBg      = ToLinear(0.040f, 0.040f, 0.040f, 1f); // ~#0a0a0a
        Color white0        = new Color(1f, 1f, 1f, 0f);
        Color white10       = new Color(1f, 1f, 1f, 0.1f);
        Color white20       = new Color(1f, 1f, 1f, 0.2f);
        Color white40       = new Color(1f, 1f, 1f, 0.4f);
        Color white80       = new Color(1f, 1f, 1f, 0.8f);

        // ── 根节点 ──
        var root = new GameObject("CharacterCard");
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(400f, 600f);
        root.AddComponent<CanvasGroup>();

        // 透明触控层（接收 hover/click 事件）
        var hitImg = root.AddComponent<Image>();
        hitImg.color         = new Color(0f, 0f, 0f, 0f);
        hitImg.raycastTarget = true;
        hitImg.alphaHitTestMinimumThreshold = 0f;
        EditorUtility.SetDirty(hitImg);

        var cardUI = root.AddComponent<CharacterCardUI>();

        // ── CardShadow（不随 TiltRoot 倾斜，offset 留足 margin）──
        var shadowGO = CreateChild(root, "CardShadow");
        shadowGO.transform.SetAsFirstSibling();
        var shadowRT = shadowGO.GetComponent<RectTransform>();
        shadowRT.anchorMin = Vector2.zero;
        shadowRT.anchorMax = Vector2.one;
        shadowRT.offsetMin = new Vector2(10f, 10f);  // 内缩 10px，左/下绝不露出
        shadowRT.offsetMax = new Vector2(10f, 10f);  // 外扩 10px，右/上阴影可见
        var shadowImg = shadowGO.AddComponent<Image>();
        shadowImg.color         = new Color(0f, 0f, 0f, 0.7f);
        shadowImg.raycastTarget = false;

        // ── TiltRoot ──
        var tiltGO = CreateChild(root, "TiltRoot");
        var tiltRT = tiltGO.GetComponent<RectTransform>();
        SetFullStretch(tiltRT);

        // ── GlowRoot（光晕，TiltRoot 首个子节点，跟随倾斜，CanvasGroup 控制）──
        var glowRootGO = CreateChild(tiltGO, "GlowRoot");
        glowRootGO.transform.SetAsFirstSibling();
        var glowRootRT = glowRootGO.GetComponent<RectTransform>();
        var glowRootCG = glowRootGO.AddComponent<CanvasGroup>();
        glowRootCG.alpha = 0f;
        glowRootRT.anchorMin = Vector2.zero;
        glowRootRT.anchorMax = Vector2.one;
        glowRootRT.offsetMin = new Vector2(-14f, -14f);
        glowRootRT.offsetMax = new Vector2( 14f,  14f);
        var glowRaw = glowRootGO.AddComponent<RawImage>();
        glowRaw.texture       = GenerateGlowTexture();
        glowRaw.raycastTarget = false;

        // ── 四角光束（尖头渐细渐变，沿边向内延伸）──
        float cX = 214f, cY = 314f;
        int beamLen = 52, beamBaseW = 6;
        string[] cNames = { "CornerTL", "CornerTR", "CornerBL", "CornerBR" };
        float[][] cPos = { new[]{ -cX, cY }, new[]{ cX, cY }, new[]{ -cX, -cY }, new[]{ cX, -cY } };

        for (int n = 0; n < 4; n++)
        {
            bool isRight  = n == 1 || n == 3;
            bool isBottom = n == 2 || n == 3;

            var container = CreateChild(glowRootGO, cNames[n]);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(cPos[n][0], cPos[n][1]);

            // 水平光束：向左或向右沿边
            float hDir = isRight ? -1f : 1f; // 右侧角向左，左侧角向右
            AddBeam(container, "HBeam", beamLen, beamBaseW, false, hDir > 0);

            // 垂直光束：向上或向下沿边
            float vDir = isBottom ? -1f : 1f; // 下侧角向上，上侧角向下
            AddBeam(container, "VBeam", beamLen, beamBaseW, true, vDir > 0);
        }

        // ── CardFrame（内边框 #1a1a1a）──
        var frameGO = CreateChild(tiltGO, "CardFrame");
        var frameImg = frameGO.AddComponent<Image>();
        frameImg.color         = _frameColor;
        frameImg.raycastTarget = false;
        SetFullStretch(frameGO.GetComponent<RectTransform>());

        // ── CardContent（内缩 2px，让 CardFrame 露出作为边框）──
        var contentGO = CreateChild(tiltGO, "CardContent");
        var contentRT = contentGO.GetComponent<RectTransform>();
        SetFullStretch(contentRT);
        contentRT.offsetMin = new Vector2(2f, 2f);
        contentRT.offsetMax = new Vector2(-2f, -2f);

        // ── CardBack ──
        var backGO = CreateChild(contentGO, "CardBack");
        var backRT = backGO.GetComponent<RectTransform>();
        SetFullStretch(backRT);

        // BackBG
        var backBgGO  = CreateChild(backGO, "BackBG");
        var backBgImg = backBgGO.AddComponent<Image>();
        backBgImg.raycastTarget = false;
        SetFullStretch(backBgGO.GetComponent<RectTransform>());
        // 加载指定卡背底图
        string backBgGuid = "6564918224b0adb478f91fcb6b5c01e8";
        string backBgPath = AssetDatabase.GUIDToAssetPath(backBgGuid);
        if (!string.IsNullOrEmpty(backBgPath))
        {
            var backBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(backBgPath);
            if (backBgSprite != null) backBgImg.sprite = backBgSprite;
        }
        // 如果 Sprite 存在则用 Sprite 本色，否则回退深色
        if (backBgImg.sprite == null)
            backBgImg.color = darkBg;

        // BackText_Main（卡背主文字，由 CharacterAsset.BackTextMain 驱动）
        var mainTxtGO  = CreateChild(backGO, "BackText_Main");
        var mainTxtRT  = mainTxtGO.GetComponent<RectTransform>();
        mainTxtRT.anchorMin        = new Vector2(0.5f, 0.5f);
        mainTxtRT.anchorMax        = new Vector2(0.5f, 0.5f);
        mainTxtRT.anchoredPosition = new Vector2(-10f, 20f);
        mainTxtRT.sizeDelta        = new Vector2(320f, 140f);
        mainTxtRT.localRotation    = Quaternion.Euler(0f, 0f, -7f);
        var mainTMP = mainTxtGO.AddComponent<TextMeshProUGUI>();
        mainTMP.text                = "MAKE\nA WISH";
        mainTMP.font                = _nameFont;
        mainTMP.fontSize            = 52f;
        mainTMP.fontStyle           = FontStyles.Bold;
        mainTMP.color               = new Color(hopeBlue.r, hopeBlue.g, hopeBlue.b, 0.55f);
        mainTMP.alignment           = TextAlignmentOptions.Center;
        mainTMP.raycastTarget       = false;
        mainTMP.enableWordWrapping  = false;

        // ── CardFace ──
        var faceGO = CreateChild(contentGO, "CardFace");
        var faceRT = faceGO.GetComponent<RectTransform>();
        SetFullStretch(faceRT);

        // PortraitImage（填满 CardFace，短边优先，不裁切）
        var portraitGO  = CreateChild(faceGO, "PortraitImage");
        portraitGO.transform.SetAsFirstSibling();
        var portraitRT  = portraitGO.GetComponent<RectTransform>();
        SetFullStretch(portraitRT);
        var portraitImg = portraitGO.AddComponent<Image>();
        portraitImg.color         = new Color(1f, 1f, 1f, 0f);
        portraitImg.raycastTarget = false;
        var fitter = portraitGO.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        // ── PortraitArea（高420，占70%）──
        var portraitAreaGO = CreateChild(faceGO, "PortraitArea");
        var portraitAreaRT = portraitAreaGO.GetComponent<RectTransform>();
        portraitAreaRT.anchorMin = new Vector2(0f, 180f / 600f);
        portraitAreaRT.anchorMax = Vector2.one;
        portraitAreaRT.offsetMin = Vector2.zero;
        portraitAreaRT.offsetMax = Vector2.zero;

        // PortraitBG
        var pBgGO  = CreateChild(portraitAreaGO, "PortraitBG");
        var pBgImg = pBgGO.AddComponent<Image>();
        pBgImg.color         = new Color(0f, 0f, 0f, 0f); // 透明，让立绘穿透显示
        pBgImg.raycastTarget = false;
        SetFullStretch(pBgGO.GetComponent<RectTransform>());

        // ── ArtGrid（斜线网底，模拟速写本纸张纹理）──
        var gridOverlayGO  = CreateChild(portraitAreaGO, "ArtGrid");
        var gridOverlayImg = gridOverlayGO.AddComponent<Image>();
        gridOverlayImg.color         = new Color(1f, 1f, 1f, 0.025f);
        gridOverlayImg.raycastTarget = false;
        SetFullStretch(gridOverlayGO.GetComponent<RectTransform>());

        // ── ArtWatermark（角色首字母水印，运行时由 CharacterCardUI 更新）──
        var wmGO  = CreateChild(portraitAreaGO, "ArtWatermark");
        var wmRT  = wmGO.GetComponent<RectTransform>();
        wmRT.anchorMin        = new Vector2(0.5f, 0.5f);
        wmRT.anchorMax        = new Vector2(0.5f, 0.5f);
        wmRT.sizeDelta        = new Vector2(200f, 80f);
        wmRT.anchoredPosition = Vector2.zero;
        var wmTMP = wmGO.AddComponent<TextMeshProUGUI>();
        wmTMP.text          = "EU";
        wmTMP.font          = _nameFont;
        wmTMP.fontSize      = 70f;
        wmTMP.fontStyle     = FontStyles.Bold;
        wmTMP.color         = new Color(1f, 1f, 1f, 0.055f);
        wmTMP.alignment     = TextAlignmentOptions.Center;
        wmTMP.raycastTarget = false;

        // PortraitGradient（底部渐变遮罩，下黑上透明）
        var gradGO  = CreateChild(portraitAreaGO, "PortraitGradient");
        var gradRT  = gradGO.GetComponent<RectTransform>();
        gradRT.anchorMin = Vector2.zero;
        gradRT.anchorMax = new Vector2(1f, 0.35f);
        gradRT.offsetMin = Vector2.zero;
        gradRT.offsetMax = Vector2.zero;
        var gradImg = gradGO.AddComponent<Image>();
        gradImg.sprite        = GenerateGradientSprite();
        gradImg.raycastTarget = false;

        // HPBadge（左上角）
        var badgeGO = CreateChild(portraitAreaGO, "HPBadge");
        var badgeRT = badgeGO.GetComponent<RectTransform>();
        badgeRT.anchorMin        = new Vector2(0f, 1f);
        badgeRT.anchorMax        = new Vector2(0f, 1f);
        badgeRT.pivot            = new Vector2(0f, 1f);
        badgeRT.anchoredPosition = new Vector2(10f, -10f);
        badgeRT.sizeDelta        = new Vector2(75f, 50f);

        // HPBadgeOutline（蓝色边框，外扩 2px 在黑色底下面）
        var badgeOutlineGO  = CreateChild(badgeGO, "HPBadgeOutline");
        badgeOutlineGO.transform.SetAsFirstSibling();
        var badgeOutlineRT  = badgeOutlineGO.GetComponent<RectTransform>();
        badgeOutlineRT.anchorMin = Vector2.zero;
        badgeOutlineRT.anchorMax = Vector2.one;
        badgeOutlineRT.offsetMin = new Vector2(-2f, -2f);
        badgeOutlineRT.offsetMax = new Vector2( 2f,  2f);
        var badgeOutlineImg = badgeOutlineGO.AddComponent<Image>();
        badgeOutlineImg.color         = new Color(hopeBlue.r, hopeBlue.g, hopeBlue.b, 0.27f); // HTML #1e90ff44
        badgeOutlineImg.raycastTarget = false;

        // HPBadgeBG（黑色底 80%）
        var badgeBgGO  = CreateChild(badgeGO, "HPBadgeBG");
        var badgeBgImg = badgeBgGO.AddComponent<Image>();
        badgeBgImg.color         = new Color(0f, 0f, 0f, 1f); // 纯黑
        badgeBgImg.raycastTarget = false;
        SetFullStretch(badgeBgGO.GetComponent<RectTransform>());
        // 备注：替换为空心矩形Sprite效果更好

        var hpBadgeTxtGO  = CreateChild(badgeGO, "HPText");
        var hpBadgeTxtRT  = hpBadgeTxtGO.GetComponent<RectTransform>();
        SetFullStretch(hpBadgeTxtRT);
        var hpBadgeTMP = hpBadgeTxtGO.AddComponent<TextMeshProUGUI>();
        hpBadgeTMP.text      = "150";
        hpBadgeTMP.font      = _nameFont;
        hpBadgeTMP.fontSize  = 32f;
        hpBadgeTMP.fontStyle = FontStyles.Bold;
        hpBadgeTMP.color     = hopeBlue;
        hpBadgeTMP.alignment = TextAlignmentOptions.Center;
        hpBadgeTMP.raycastTarget = false;

        // ── InfoArea（高180，占30%）──
        var infoGO = CreateChild(faceGO, "InfoArea");
        var infoRT = infoGO.GetComponent<RectTransform>();
        infoRT.anchorMin = Vector2.zero;
        infoRT.anchorMax = new Vector2(1f, 180f / 600f);
        infoRT.offsetMin = Vector2.zero;
        infoRT.offsetMax = Vector2.zero;

        // InfoBG
        var infoBgGO  = CreateChild(infoGO, "InfoBG");
        var infoBgImg = infoBgGO.AddComponent<Image>();
        infoBgImg.color         = darkerBg;
        infoBgImg.raycastTarget = false;
        SetFullStretch(infoBgGO.GetComponent<RectTransform>());

        // TopDivider（蓝色分割线）
        var divGO  = CreateChild(infoGO, "TopDivider");
        var divRT  = divGO.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0f, 1f);
        divRT.anchorMax = new Vector2(1f, 1f);
        divRT.offsetMin = new Vector2(0f, -2f);
        divRT.offsetMax = Vector2.zero;
        var divImg = divGO.AddComponent<Image>();
        divImg.color         = new Color(hopeBlue.r, hopeBlue.g, hopeBlue.b, 0.8f);
        divImg.raycastTarget = false;

        // NameShadow（模拟 text-shadow: 1px 1px 0 #000）
        var nameShadowGO  = CreateChild(infoGO, "NameShadow");
        var nameShadowRT  = nameShadowGO.GetComponent<RectTransform>();
        nameShadowRT.anchorMin        = new Vector2(0f, 0.55f);
        nameShadowRT.anchorMax        = new Vector2(1f, 1f);
        nameShadowRT.offsetMin        = new Vector2(18f, -6f);
        nameShadowRT.offsetMax        = new Vector2(-14f, -14f);
        var nameShadowTMP = nameShadowGO.AddComponent<TextMeshProUGUI>();
        nameShadowTMP.text             = "Eudora";
        nameShadowTMP.font             = _nameFont;
        nameShadowTMP.fontSize         = 40f;
        nameShadowTMP.fontStyle        = FontStyles.Bold;
        nameShadowTMP.color            = new Color(0f, 0f, 0f, 0.8f);
        nameShadowTMP.alignment        = TextAlignmentOptions.Left;
        nameShadowTMP.raycastTarget    = false;
        nameShadowTMP.enableWordWrapping = false;
        nameShadowTMP.overflowMode     = TextOverflowModes.Ellipsis;

        // NameText
        var nameGO  = CreateChild(infoGO, "NameText");
        var nameRT  = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin        = new Vector2(0f, 0.55f);
        nameRT.anchorMax        = new Vector2(1f, 1f);
        nameRT.offsetMin        = new Vector2(16f, -8f);
        nameRT.offsetMax        = new Vector2(-16f, -12f);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text             = "Eudora";
        nameTMP.font             = _nameFont;
        nameTMP.fontSize         = 40f;
        nameTMP.fontStyle        = FontStyles.Bold;
        nameTMP.color            = ToLinear(0.937f, 0.937f, 0.937f, 1f); // HTML #efefef
        nameTMP.alignment        = TextAlignmentOptions.Left;
        nameTMP.raycastTarget    = false;
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode     = TextOverflowModes.Ellipsis;

        // RoleText
        var roleGO  = CreateChild(infoGO, "RoleText");
        var roleRT  = roleGO.GetComponent<RectTransform>();
        roleRT.anchorMin        = new Vector2(0f, 0.38f);
        roleRT.anchorMax        = new Vector2(1f, 0.58f);
        roleRT.offsetMin        = new Vector2(16f, 0f);
        roleRT.offsetMax        = new Vector2(-16f, 0f);
        var roleTMP = roleGO.AddComponent<TextMeshProUGUI>();
        roleTMP.text           = "Hope · Leader";
        roleTMP.font           = _roleFont;
        roleTMP.fontSize       = 24f;
        roleTMP.color          = new Color(1f, 1f, 1f, 0.27f); // HTML #ffffff44
        roleTMP.alignment      = TextAlignmentOptions.Left;
        roleTMP.characterSpacing = 3f;
        roleTMP.raycastTarget  = false;

        // StatRow（部署格 + 牌库容量，替代 HPBarRow）
        var statRowGO = CreateChild(infoGO, "StatRow");
        var statRowRT = statRowGO.GetComponent<RectTransform>();
        statRowRT.anchorMin = new Vector2(0f, 0.05f);
        statRowRT.anchorMax = new Vector2(1f, 0.36f);
        statRowRT.offsetMin = new Vector2(16f, 0f);
        statRowRT.offsetMax = new Vector2(-16f, 0f);

        // DeploySlotContainer（平行四边形图标容器，运行时由 RefreshDisplay 填充）
        var slotContainerGO = CreateChild(statRowGO, "DeploySlotContainer");
        var slotContainerRT = slotContainerGO.GetComponent<RectTransform>();
        slotContainerRT.anchorMin = Vector2.zero;
        slotContainerRT.anchorMax = Vector2.one;
        slotContainerRT.offsetMin = Vector2.zero;
        slotContainerRT.offsetMax = new Vector2(-60f, 0f); // 右边留空给牌库容量
        var slotLayout = slotContainerGO.AddComponent<HorizontalLayoutGroup>();
        slotLayout.childAlignment = TextAnchor.MiddleLeft;
        slotLayout.spacing = 8f;
        slotLayout.childControlWidth = false;
        slotLayout.childControlHeight = false;
        slotLayout.childForceExpandWidth = false;
        slotLayout.childForceExpandHeight = false;

        // DeckCapacity（牌库上限数字）
        var deckCapGO = CreateChild(statRowGO, "DeckCapacity");
        var deckCapRT = deckCapGO.GetComponent<RectTransform>();
        deckCapRT.anchorMin = new Vector2(1f, 0f);
        deckCapRT.anchorMax = new Vector2(1f, 1f);
        deckCapRT.pivot     = new Vector2(1f, 0.5f);
        deckCapRT.anchoredPosition = Vector2.zero;
        deckCapRT.sizeDelta = new Vector2(60f, 0f);
        var deckCapTMP = deckCapGO.AddComponent<TextMeshProUGUI>();
        deckCapTMP.text          = "30";
        deckCapTMP.font          = _nameFont;
        deckCapTMP.fontSize      = 26f;
        deckCapTMP.color         = new Color(hopeBlue.r, hopeBlue.g, hopeBlue.b, 0.7f);
        deckCapTMP.alignment     = TextAlignmentOptions.Right;
        deckCapTMP.raycastTarget = false;

        // CardBack 置顶渲染，确保飞入时看到卡背
        backGO.transform.SetAsLastSibling();

        // ── 绑定 CharacterCardUI 的引用 ──
        var so = new SerializedObject(cardUI);
        so.FindProperty("nameText").objectReferenceValue    = nameTMP;
        so.FindProperty("roleText").objectReferenceValue    = roleTMP;
        so.FindProperty("hpText").objectReferenceValue      = hpBadgeTMP;
        so.FindProperty("portraitImage").objectReferenceValue = portraitImg;
        so.FindProperty("cardBackRoot").objectReferenceValue = backGO;
        so.FindProperty("backTextMain").objectReferenceValue = mainTMP;
        so.FindProperty("cardFaceRoot").objectReferenceValue = faceGO;
        so.FindProperty("glowGroup").objectReferenceValue   = glowRootCG;
        so.FindProperty("tiltRoot").objectReferenceValue    = tiltRT;
        so.FindProperty("artWatermark").objectReferenceValue = wmTMP;
        so.FindProperty("deploySlotContainer").objectReferenceValue = slotContainerRT;
        so.FindProperty("deckCapacityText").objectReferenceValue    = deckCapTMP;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(root);

        // ── 存为 Prefab ──
        string prefabPath = _savePath + "/CharacterCard.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();

        if (prefab != null)
        {
            EditorUtility.DisplayDialog("完成",
                $"CharacterCard Prefab 已保存至：\n{prefabPath}\n\n" +
                "接下来：\n" +
                "  1. 把此 Prefab 拖入 CharacterSelectManager → Card Prefab\n" +
                "  2. 游戏运行时会自动用 CharacterAsset 填充数据",
                "OK");

            // 在 Project 窗口高亮选中
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        else
        {
            EditorUtility.DisplayDialog("失败", $"Prefab 保存失败，路径：{prefabPath}", "OK");
        }
    }

    // ─────────────────────────────────────────────────
    // 色彩空间工具（项目使用 Linear 空间）
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
    // 程序化渐变纹理
    // ─────────────────────────────────────────────────

    Sprite GenerateGradientSprite()
    {
        string path = "Assets/Textures/Procedural/PortraitGradient.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        int w = 8, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.alphaIsTransparency = true;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            float a = 1f - (float)y / (h - 1);
            Color c = new Color(0f, 0f, 0f, a);
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        Object.DestroyImmediate(tex);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    void AddCornerGlow(GameObject parent, string name, float px, float py, float dx, float dy, float len, float w, Color c)
    {
        var corner = CreateChild(parent, name);
        var crt = corner.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(px, py);

        // 水平线：dx>0 向右延伸 (pivot 靠左)，dx<0 向左延伸 (pivot 靠右)
        var h = CreateChild(corner, $"{name}_H");
        var hrt = h.GetComponent<RectTransform>();
        hrt.pivot = new Vector2(dx > 0 ? 0f : 1f, 0.5f);
        hrt.sizeDelta = new Vector2(len, w);
        var himg = h.AddComponent<Image>();
        himg.color = c; himg.raycastTarget = false;

        // 垂直线：dy>0 向上延伸 (pivot 靠下)，dy<0 向下延伸 (pivot 靠上)
        var v = CreateChild(corner, $"{name}_V");
        var vrt = v.GetComponent<RectTransform>();
        vrt.pivot = new Vector2(0.5f, dy > 0 ? 0f : 1f);
        vrt.sizeDelta = new Vector2(w, len);
        var vimg = v.AddComponent<Image>();
        vimg.color = c; vimg.raycastTarget = false;
    }

    Texture2D GenerateGlowTexture()
    {
        string path = "Assets/Textures/Procedural/GlowBorder.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        int M = 14, B = 4, S = 8;
        int iW = 400, iH = 600;
        int tW = iW + M * 2, tH = iH + M * 2;

        var tex = new Texture2D(tW, tH, TextureFormat.RGBA32, false);
        tex.alphaIsTransparency = true;
        tex.filterMode = FilterMode.Bilinear;

        float r = 30f / 255f, g = 144f / 255f, b = 255f / 255f;

        for (int y = 0; y < tH; y++)
        {
            for (int x = 0; x < tW; x++)
            {
                int dx = (x < M) ? (M - x) : (x >= M + iW) ? (x - M - iW + 1) : 0;
                int dy = (y < M) ? (M - y) : (y >= M + iH) ? (y - M - iH + 1) : 0;
                int d = Mathf.Max(dx, dy);
                float a = 0f;

                if (d > 0 && d <= B + S)
                {
                    float t = (float)(d - 1) / (B + S);
                    a = 0.06f * (1f - t * t);
                    // 随机噪点颗粒感
                    a *= 0.5f + Random.value * 0.5f;
                }

                tex.SetPixel(x, y, new Color(r, g, b, a));
            }
        }

        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        Object.DestroyImmediate(tex);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ─────────────────────────────────────────────────
    void AddBeam(GameObject parent, string name, int length, int baseWidth, bool vertical, bool positiveDir)
    {
        var beam = CreateChild(parent, name);
        var brt = beam.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);

        int texW = vertical ? baseWidth : length;
        int texH = vertical ? length : baseWidth;
        // flipGradient: opaque at pivot end, transparent at tip
        //   vertical downward→false, vertical upward→true, horizontal right→true, horizontal left→false
        bool flipGradient = vertical ? !positiveDir : positiveDir;
        var tex = CreateBeamTexture($"{parent.name}_{name}", texW, texH, flipGradient);
        var raw = beam.AddComponent<RawImage>();
        raw.texture = tex; raw.raycastTarget = false;

        brt.sizeDelta = new Vector2(vertical ? baseWidth : length, vertical ? length : baseWidth);
        // pivot at corner end: downward→top(1), upward→bottom(0), rightward→left(0), leftward→right(1)
        brt.pivot = vertical
            ? new Vector2(0.5f, positiveDir ? 1f : 0f)
            : new Vector2(positiveDir ? 0f : 1f, 0.5f);
    }

    Texture2D CreateBeamTexture(string tag, int w, int h, bool flipGradient)
    {
        string path = $"Assets/Textures/Procedural/{tag}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        // w > h → horizontal beam (length along X, width along Y)
        // h > w → vertical beam   (length along Y, width along X)
        bool horizontal = w > h;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.alphaIsTransparency = true;
        tex.filterMode = FilterMode.Bilinear;
        Color cyan = new Color(0f, 0.827f, 1f, 1f);
        for (int py = 0; py < h; py++)
        {
            float yNorm = h > 1 ? (float)py / (h - 1) : 0f;
            for (int px = 0; px < w; px++)
            {
                float xNorm = w > 1 ? (float)px / (w - 1) : 0f;
                // length axis: along the beam's extension direction
                float lenNorm = horizontal ? xNorm : yNorm;
                // width axis: the thin cross-section
                float widthNorm = horizontal ? yNorm : xNorm;
                float lenAlpha = flipGradient ? (1f - lenNorm) : lenNorm;
                lenAlpha = lenAlpha * lenAlpha;
                float dist = Mathf.Abs(widthNorm - 0.5f) * 2f;
                float widthAlpha = 1f - dist * dist;
                float a = lenAlpha * widthAlpha;
                tex.SetPixel(px, py, new Color(cyan.r, cyan.g, cyan.b, a));
            }
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        Object.DestroyImmediate(tex);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 工具方法
    // ─────────────────────────────────────────────────

    static GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void SetFullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
