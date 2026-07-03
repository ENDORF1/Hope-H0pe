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
    public float zoomStart = 0.7f;
    public float zoomDuration = 1.2f;
    public Camera battleCamera;

    [Header("拖尾")]
    public Color trailColor = new Color(0.29f, 0.62f, 1f, 0.5f);
    public int   trailCount = 20;
    public float trailSpacing = 0.025f;
    public float trailLife = 0.35f;
    public Vector2 trailSize = new Vector2(30f, 40f);

    [Header("回调")]
    public MonoBehaviour onCompleteTarget;
    public string onCompleteMethod = "BeginGameLoop";

    private Canvas        _entranceCanvas;
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
        if (playerMarker != null) HideCanvas(playerMarker);
        if (enemyMarker != null) HideCanvas(enemyMarker);

        var canvasGO = new GameObject("__EntranceCanvas__");
        canvasGO.transform.SetParent(transform);
        _entranceCanvas = canvasGO.AddComponent<Canvas>();
        _entranceCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _entranceCanvas.sortingOrder = -1;
        canvasGO.AddComponent<CanvasScaler>().referenceResolution = new Vector2(2560, 1440);
        canvasGO.AddComponent<GraphicRaycaster>();
        Debug.Log("[HopeEntrance] Awake done");
    }

    void Start()
    {
        Debug.Log($"[HopeEntrance] Start. characterCardPrefab={characterCardPrefab}, selected={GameData.SelectedCharacter?.CharacterName}, enemy={enemyCharacter?.CharacterName}");
        if (characterCardPrefab == null) { Debug.LogError("[HopeEntrance] characterCardPrefab 未赋值！"); return; }

        _playerCardRT = CreateCard(GameData.SelectedCharacter);
        Debug.Log($"[HopeEntrance] playerCardRT={_playerCardRT}");
        if (_playerCardRT == null) return;
        _enemyCardRT = CreateCard(enemyCharacter);
        Debug.Log($"[HopeEntrance] enemyCardRT={_enemyCardRT}");
        if (_enemyCardRT == null) return;

        Vector2 playerScrPos = WorldToCanvasPos(playerMarker);
        Vector2 enemyScrPos  = WorldToCanvasPos(enemyMarker);
        bool playerAscend = GameData.SelectedFaction == TitleScreenManager.Faction.Hope;

        float playerOffY = playerAscend ? -1200f : 1200f;
        float enemyOffY  = playerAscend ? 1200f : -1200f;
        _playerCardRT.anchoredPosition = new Vector2(playerScrPos.x, playerOffY);
        _enemyCardRT.anchoredPosition  = new Vector2(enemyScrPos.x,  enemyOffY);

        PlayEntrance(playerScrPos, enemyScrPos);
    }

    RectTransform CreateCard(CharacterAsset data)
    {
        if (data == null) { Debug.LogError("[HopeEntrance] 角色数据为空！"); return null; }
        var go = Instantiate(characterCardPrefab, _entranceCanvas.transform);
        go.name = $"Entrance_{data.CharacterName}";
        var cardUI = go.GetComponent<CharacterCardUI>();
        if (cardUI != null) { cardUI.Data = data; cardUI.RefreshDisplay(); cardUI.FlipToBackImmediate(); }
        var rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
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
            if (onCompleteTarget != null && !string.IsNullOrEmpty(onCompleteMethod))
                onCompleteTarget.Invoke(onCompleteMethod, 0f);
            // 等战斗卡翻面完成后再隐退入场 Canvas
            DOVirtual.DelayedCall(1.2f, () => _entranceCanvas.gameObject.SetActive(false));
        });

        StartCoroutine(TrailRoutine(_playerCardRT, _playerTrails));
        StartCoroutine(TrailRoutine(_enemyCardRT, _enemyTrails));
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
