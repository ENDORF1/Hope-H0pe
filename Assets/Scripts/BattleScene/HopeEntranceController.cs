using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// 战斗场景入场动画 —— Hope 阵营（飞升进入）。
/// </summary>
public class HopeEntranceController : MonoBehaviour
{
    [Header("位置标记（仅坐标参考）")]
    public Transform playerMarker;
    public Transform enemyMarker;

    [Header("卡牌预制体")]
    public GameObject characterCardPrefab;
    public CharacterAsset enemyCharacter;

    [Header("动画")]
    public float flyDuration = 1.4f;
    public Ease  flyEase = Ease.InOutCubic;
    public float holdDuration = 0.3f;

    [Header("地图缩放")]
    public float zoomStart = 0.85f;
    public float zoomDuration = 1.2f;
    public Camera battleCamera;

    [Header("拖尾")]
    public Color trailColor = new Color(0.29f, 0.62f, 1f, 0.5f);
    public int   trailCount = 20;
    public float trailSpacing = 0.025f;
    public float trailLife = 0.35f;
    public Vector2 trailSize = new Vector2(30f, 40f);

    [Header("战斗肖像预制体")]
    [Tooltip("玩家肖像默认预制体（若 CharacterAsset.BattlePortraitPrefab 为空则使用此预制体）")]
    public GameObject defaultPlayerPortraitPrefab;
    [Tooltip("敌方肖像默认预制体")]
    public GameObject defaultEnemyPortraitPrefab;
    [Tooltip("肖像实例化后的缩放，场景中正确尺寸为 (7,7,5)，预制体默认 (1,1,1) 需要 ×7")]
    public Vector3 portraitScale = new Vector3(7f, 7f, 5f);

    [Header("飞入卡片缩放")]
    [Tooltip("自动根据场景肖像尺寸计算，此值为额外微调倍率（1=不调整）")]
    public float flewInCardScale = 1f;

    [Header("黑屏衔接")]
    [Tooltip("入场开始前黑屏淡出时长（秒）")]
    public float blackFadeDuration = 0.5f;

    [Header("GameManager")]
    [Tooltip("拖入场景中的 GameManager，入场结束后自动启动游戏循环")]
    public GameManager gameManager;

    private Canvas        _entranceCanvas;
    private Image         _blackOverlay;
    private RectTransform _playerCardRT;
    private RectTransform _enemyCardRT;
    private List<TrailDot> _playerTrails = new List<TrailDot>();
    private List<TrailDot> _enemyTrails  = new List<TrailDot>();
    private Tweener       _zoomTween;

    private struct TrailDot
    {
        public GameObject go;
        public RectTransform rt;
        public Image img;
        public float life;
    }

    void Awake()
    {
        Debug.Log("[HopeEntrance] Awake");
        if (battleCamera == null) battleCamera = Camera.main;
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        if (playerMarker != null) HideCanvas(playerMarker);
        if (enemyMarker != null) HideCanvas(enemyMarker);

        var canvasGO = new GameObject("__EntranceCanvas__");
        canvasGO.transform.SetParent(transform);
        _entranceCanvas = canvasGO.AddComponent<Canvas>();
        _entranceCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _entranceCanvas.sortingOrder = 5;
        canvasGO.AddComponent<CanvasScaler>().referenceResolution = new Vector2(2560, 1440);
        canvasGO.AddComponent<GraphicRaycaster>();

        // 黑屏遮罩：场景加载后先全黑，衔接选人界面的黑屏，然后淡出
        var overlayGO = new GameObject("__BlackOverlay__", typeof(RectTransform), typeof(Image));
        overlayGO.transform.SetParent(_entranceCanvas.transform, false);
        var overlayRT = overlayGO.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero; overlayRT.anchorMax = Vector2.one;
        overlayRT.sizeDelta = Vector2.zero;
        _blackOverlay = overlayGO.GetComponent<Image>();
        _blackOverlay.color = Color.black;
        _blackOverlay.raycastTarget = false;

        Debug.Log("[HopeEntrance] Awake done");
    }

    void Start()
    {
        Debug.Log($"[HopeEntrance] Start. characterCardPrefab={characterCardPrefab}, selected={GameData.SelectedCharacter?.CharacterName}, enemy={enemyCharacter?.CharacterName}");
        if (characterCardPrefab == null) { Debug.LogError("[HopeEntrance] characterCardPrefab 未赋值！"); return; }

        // 用场景肖像的 BoxCollider 世界空间边界自动计算飞入卡片缩放，确保落地后无跳变
        float playerScale = CalcCardScaleFromMarker(playerMarker) * flewInCardScale;
        float enemyScale  = CalcCardScaleFromMarker(enemyMarker) * flewInCardScale;
        Debug.Log($"[HopeEntrance] Auto card scale: player={playerScale:F3}, enemy={enemyScale:F3}");

        _playerCardRT = CreateCard(GameData.SelectedCharacter, playerScale);
        Debug.Log($"[HopeEntrance] playerCardRT={_playerCardRT}");
        if (_playerCardRT == null) return;
        _enemyCardRT = CreateCard(enemyCharacter, enemyScale);
        Debug.Log($"[HopeEntrance] enemyCardRT={_enemyCardRT}");
        if (_enemyCardRT == null) return;

        Vector2 playerScrPos = WorldToCanvasPos(playerMarker);
        Vector2 enemyScrPos  = WorldToCanvasPos(enemyMarker);
        bool playerAscend = GameData.SelectedFaction == TitleScreenManager.Faction.Hope;

        float playerOffY = playerAscend ? -1200f : 1200f;
        float enemyOffY  = playerAscend ? 1200f : -1200f;
        _playerCardRT.anchoredPosition = new Vector2(playerScrPos.x, playerOffY);
        _enemyCardRT.anchoredPosition  = new Vector2(enemyScrPos.x,  enemyOffY);

        // 黑屏淡出 → 开始入场
        _blackOverlay.DOFade(0f, blackFadeDuration).SetEase(Ease.OutCubic);
        PlayEntrance(playerScrPos, enemyScrPos);
    }

    /// <summary>用 marker 上 BoxCollider 的世界空间边界，算出 CharacterCardUI 在 EntranceCanvas 中需要的 scale</summary>
    float CalcCardScaleFromMarker(Transform marker)
    {
        if (marker == null || battleCamera == null) return 1f;
        var col = marker.GetComponent<BoxCollider>();
        if (col == null) return 1f;

        Bounds b = col.bounds;
        Vector3 topC = new Vector3(b.center.x, b.max.y, b.center.z);
        Vector3 botC = new Vector3(b.center.x, b.min.y, b.center.z);
        Vector3 leftC = new Vector3(b.min.x, b.center.y, b.center.z);
        Vector3 rightC = new Vector3(b.max.x, b.center.y, b.center.z);

        Vector3 st = battleCamera.WorldToScreenPoint(topC);
        Vector3 sb = battleCamera.WorldToScreenPoint(botC);
        Vector3 sl = battleCamera.WorldToScreenPoint(leftC);
        Vector3 sr = battleCamera.WorldToScreenPoint(rightC);

        float screenH = Mathf.Abs(st.y - sb.y);
        float screenW = Mathf.Abs(sr.x - sl.x);

        // CharacterCardUI prefab 是 400×600（参考分辨率 2560×1440）
        const float cardW = 400f, cardH = 600f;
        float scaleH = screenH / cardH;
        float scaleW = screenW / cardW;
        return Mathf.Min(scaleH, scaleW); // 取较小值保证不超出
    }

    RectTransform CreateCard(CharacterAsset data, float scale)
    {
        if (data == null) { Debug.LogError("[HopeEntrance] 角色数据为空！"); return null; }
        var go = Instantiate(characterCardPrefab, _entranceCanvas.transform);
        go.name = $"Entrance_{data.CharacterName}";
        var cardUI = go.GetComponent<CharacterCardUI>();
        if (cardUI != null) { cardUI.Data = data; cardUI.RefreshDisplay(); cardUI.FlipToBackImmediate(); }
        var rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one * scale;
        return rt;
    }

    Vector2 WorldToCanvasPos(Transform marker)
    {
        if (marker == null || battleCamera == null) return Vector2.zero;
        Vector3 screenPos = battleCamera.WorldToScreenPoint(marker.position);
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _entranceCanvas.GetComponent<RectTransform>(), screenPos, null, out localPos);
        return localPos;
    }

    void PlayEntrance(Vector2 playerTarget, Vector2 enemyTarget)
    {
        Debug.Log($"[HopeEntrance] PlayEntrance. playerY={playerTarget.y}, enemyY={enemyTarget.y}, zoomStart={zoomStart}, battleCam={battleCamera}");

        // 地图缩放
        if (battleCamera != null && battleCamera.orthographic)
        {
            float targetOrtho = battleCamera.orthographicSize;
            float startOrtho  = targetOrtho / zoomStart;
            battleCamera.orthographicSize = startOrtho;
            _zoomTween = DOTween.To(() => battleCamera.orthographicSize,
                                    x  => battleCamera.orthographicSize = x,
                                    targetOrtho, zoomDuration)
                                .SetEase(Ease.OutCubic);
        }

        var seq = DOTween.Sequence();
        seq.Join(_playerCardRT.DOAnchorPosY(playerTarget.y, flyDuration).SetEase(flyEase));
        seq.Join(_enemyCardRT.DOAnchorPosY(enemyTarget.y, flyDuration).SetEase(flyEase));
        seq.AppendInterval(holdDuration);
        seq.OnComplete(() =>
        {
            // 销毁飞入的选人卡片，换成真正的战斗肖像预制体
            if (_playerCardRT != null) Destroy(_playerCardRT.gameObject);
            if (_enemyCardRT != null) Destroy(_enemyCardRT.gameObject);

            SetupBattlePortraits();

            // 启动游戏循环
            if (gameManager != null)
                gameManager.BeginGameLoop();

            // 等战斗卡翻面完成后再隐退入场 Canvas
            DOVirtual.DelayedCall(1.2f, () => _entranceCanvas.gameObject.SetActive(false));
        });

        StartCoroutine(TrailRoutine(_playerCardRT, _playerTrails));
        StartCoroutine(TrailRoutine(_enemyCardRT, _enemyTrails));
    }

    /// <summary>
    /// 入场动画结束后的回调：销毁飞入的选角卡片，实例化真正的战斗肖像预制体，
    /// 完成从选人界面到战斗界面的视觉过渡。
    /// </summary>
    /// <param name="playerTargetScreenH">飞入卡片在屏幕上的高度（像素），用于匹配战斗肖像大小</param>
    /// <param name="enemyTargetScreenH">敌方飞入卡片屏幕高度</param>
    void SetupBattlePortraits()
    {
        // ── 玩家肖像 ──────────────────────────────────
        GameObject playerPrefab = GameData.SelectedCharacter?.BattlePortraitPrefab ?? defaultPlayerPortraitPrefab;
        if (playerPrefab != null && playerMarker != null)
        {
            var go = Instantiate(playerPrefab, playerMarker.position, playerMarker.rotation, playerMarker.parent);
            go.transform.localScale = portraitScale;
            var ocm = go.GetComponent<OneCardManager>();
            var rot = go.GetComponent<BetterCardRotation>();
            ocm?.SetupPortraitFromCharacter(GameData.SelectedCharacter);
            rot?.ShowBack(); // 卡背朝上，等 FlipPortraitsOnGameStart 翻面
            gameManager?.SetPlayerPortrait(ocm, rot);
        }

        // ── 敌方肖像 ──────────────────────────────────
        GameObject enemyPrefab = enemyCharacter?.BattlePortraitPrefab ?? defaultEnemyPortraitPrefab;
        if (enemyPrefab != null && enemyMarker != null)
        {
            var go = Instantiate(enemyPrefab, enemyMarker.position, enemyMarker.rotation, enemyMarker.parent);
            go.transform.localScale = portraitScale;
            var ocm = go.GetComponent<OneCardManager>();
            var rot = go.GetComponent<BetterCardRotation>();
            ocm?.SetupPortraitFromCharacter(enemyCharacter);
            rot?.ShowBack();
            gameManager?.SetAiPortrait(ocm, rot);
        }
    }

    System.Collections.IEnumerator TrailRoutine(RectTransform cardRT, List<TrailDot> trails)
    {
        float timer = 0f;
        while (timer < flyDuration + 0.1f)
        {
            timer += Time.deltaTime;
            if (cardRT != null)
                SpawnTrail(trails, cardRT.anchoredPosition);
            yield return new WaitForSeconds(trailSpacing);
        }
    }

    void SpawnTrail(List<TrailDot> trails, Vector2 pos)
    {
        var go = new GameObject("Trail", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_entranceCanvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos + new Vector2(Random.Range(-15f, 15f), Random.Range(-10f, 10f));
        rt.sizeDelta = trailSize * Random.Range(0.5f, 1.2f);
        img.color = trailColor;
        img.raycastTarget = false;
        trails.Add(new TrailDot { go = go, rt = rt, img = img, life = trailLife });
    }

    void Update()
    {
        float dt = Time.deltaTime;
        UpdateTrails(_playerTrails, dt);
        UpdateTrails(_enemyTrails, dt);
    }

    void UpdateTrails(List<TrailDot> trails, float dt)
    {
        for (int i = trails.Count - 1; i >= 0; i--)
        {
            var t = trails[i];
            t.life -= dt;
            if (t.life <= 0f) { Destroy(t.go); trails.RemoveAt(i); }
            else
            {
                var c = t.img.color;
                t.img.color = new Color(c.r, c.g, c.b, t.life / trailLife * trailColor.a);
                trails[i] = t;
            }
        }
    }

    void OnDestroy()
    {
        _zoomTween?.Kill();
    }

    void HideCanvas(Transform t)
    {
        if (t == null) return;
        foreach (var c in t.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
    }
}
