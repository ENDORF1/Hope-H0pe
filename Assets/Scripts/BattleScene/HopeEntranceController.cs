using UnityEngine;
using DG.Tweening;

/// <summary>
/// 战斗场景入场动画。
/// - 两个位置标记仅用于坐标参考，永远不可见
/// - 玩家卡从选角 Prefab 实例化，敌方卡从池中抽取
/// - 双方卡背同时从场外飞入（方向由玩家阵营决定）
/// - 入场完成后切换到战斗 Prefab
/// </summary>
public class HopeEntranceController : MonoBehaviour
{
    [Header("位置标记（仅坐标参考，脚本自动隐藏）")]
    public Transform playerMarker;
    public Transform enemyMarker;

    [Header("卡牌预制体")]
    [Tooltip("选角界面的 CharacterCard Prefab")]
    public GameObject characterCardPrefab;
    [Tooltip("敌方的 CharacterAsset（临时，后续接池子）")]
    public CharacterAsset enemyCharacter;

    [Header("动画参数")]
    public float flyDuration = 0.7f;
    public Ease  flyEase = Ease.OutBack;
    public float holdDuration = 0.5f;

    [Header("回调")]
    public MonoBehaviour onCompleteTarget;
    public string onCompleteMethod = "BeginGameLoop";

    private Canvas       _entranceCanvas;
    private RectTransform _playerCardRT;
    private RectTransform _enemyCardRT;
    private CharacterCardUI _playerCardUI;
    private Camera       _mainCam;

    void Awake()
    {
        _mainCam = Camera.main;

        // 标记不渲染，但保持 active（防止 GameManager 协程崩溃）
        HideCanvas(playerMarker);
        HideCanvas(enemyMarker);

        // 创建临时的 Screen Space Overlay Canvas
        var canvasGO = new GameObject("__EntranceCanvas__");
        canvasGO.transform.SetParent(transform);
        _entranceCanvas = canvasGO.AddComponent<Canvas>();
        _entranceCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _entranceCanvas.sortingOrder = 5;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>()
            .referenceResolution = new Vector2(2560, 1440);
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    void Start()
    {
        if (characterCardPrefab == null)
        {
            Debug.LogError("[HopeEntrance] characterCardPrefab 未赋值！");
            return;
        }

        // 实例化玩家卡
        _playerCardRT = CreateCard(GameData.SelectedCharacter);
        if (_playerCardRT == null) return;

        // 实例化敌方卡
        _enemyCardRT = CreateCard(enemyCharacter);
        if (_enemyCardRT == null) return;

        // 定位到屏幕坐标（从 3D 标记转换）
        Vector2 playerScrPos = WorldToCanvasPos(playerMarker);
        Vector2 enemyScrPos  = WorldToCanvasPos(enemyMarker);

        // 决定方向：Hope 玩家飞升 ↑ 敌人坠落 ↓，Void 反之
        bool playerAscend = GameData.SelectedFaction == TitleScreenManager.Faction.Hope;

        // 移动到屏幕外
        float playerOffY = playerAscend ? -900f : 900f;
        float enemyOffY  = playerAscend ? 900f : -900f;
        _playerCardRT.anchoredPosition = new Vector2(playerScrPos.x, playerOffY);
        _enemyCardRT.anchoredPosition  = new Vector2(enemyScrPos.x,  enemyOffY);

        // 播放动画
        PlayEntrance(playerAscend);
    }

    RectTransform CreateCard(CharacterAsset data)
    {
        if (data == null)
        {
            Debug.LogError("[HopeEntrance] 角色数据为空！");
            return null;
        }

        var go = Instantiate(characterCardPrefab, _entranceCanvas.transform);
        go.name = $"Entrance_{data.CharacterName}";

        var cardUI = go.GetComponent<CharacterCardUI>();
        if (cardUI != null)
        {
            cardUI.Data = data;
            cardUI.RefreshDisplay();
            // 强制卡背朝上
            cardUI.FlipToBackImmediate();
        }

        var rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        return rt;
    }

    Vector2 WorldToCanvasPos(Transform marker)
    {
        if (marker == null || _mainCam == null) return Vector2.zero;
        Vector3 screenPos = _mainCam.WorldToScreenPoint(marker.position);
        var canvasRT = _entranceCanvas.GetComponent<RectTransform>();
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT, screenPos, null, out localPos);
        return localPos;
    }

    void PlayEntrance(bool playerAscend)
    {
        var seq = DOTween.Sequence();

        // 双方同时飞入
        float playerTargetY = WorldToCanvasPos(playerMarker).y;
        float enemyTargetY  = WorldToCanvasPos(enemyMarker).y;

        seq.Join(_playerCardRT.DOAnchorPosY(playerTargetY, flyDuration).SetEase(flyEase));
        seq.Join(_enemyCardRT.DOAnchorPosY(enemyTargetY, flyDuration).SetEase(flyEase));

        // 停留
        seq.AppendInterval(holdDuration);

        // 交棒（入场 Canvas 暂留，后续实现切换动画后再销毁）
        seq.OnComplete(() =>
        {
            if (onCompleteTarget != null && !string.IsNullOrEmpty(onCompleteMethod))
                onCompleteTarget.Invoke(onCompleteMethod, 0f);
        });
    }

    void HideCanvas(Transform t)
    {
        if (t == null) return;
        foreach (var c in t.GetComponentsInChildren<Canvas>(true))
            c.enabled = false;
    }
}
