using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 希望阵营专属转场控制器。
///
/// 时序：
///   0s                 — 粒子正常四散爆开
///   0 ~ slideDuration  — MainCamera 向左平移，背景 UV 横向流动，
///                        粒子每帧跟随镜头位移
///   slideDuration      — 新场景激活（已在后台 Additive 加载完毕），旧场景暂不卸载
///   激活后             — 粒子继续飞 particleFadeOutDuration 秒后淡出
///   淡出结束           — 卸载旧场景
///
/// Inspector 配置：
///   mainCamera              — 主相机
///   titleBackground         — TitleBackground 脚本
///   slideDuration           — 镜头平移总时长（秒）
///   cameraSlideX            — 镜头向左移动的世界单位距离
///   bgScrollAmount          — 背景 UV 横向偏移总量（0~1）
///   particleFollowScale     — 粒子跟随镜头的比例（根据 Canvas/Camera 比例调整）
///   particleFadeOutDuration — 进入新场景后粒子淡出时长（秒，可自由调节）
/// </summary>
public class HopeTransition : MonoBehaviour
{
    public static HopeTransition Instance { get; private set; }

    [Header("场景引用")]
    [Tooltip("主相机（MainCamera）")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("TitleBackground 脚本所在对象")]
    [SerializeField] private TitleBackground titleBackground;

    [Tooltip("主界面 Canvas 的 WorldSpaceCanvasScaler，转场时锁定位置防止 Canvas 跟随相机")]
    [SerializeField] private WorldSpaceCanvasScaler mainCanvasScaler;

    [Header("UI 淡出")]
    [Tooltip("需要淡出的 UI 容器列表（按钮容器、标题等），各自会被加上 CanvasGroup 统一淡出")]
    [SerializeField] private List<RectTransform> uiFadeTargets = new List<RectTransform>();

    [Tooltip("UI 淡出时长（秒）")]
    [SerializeField] private float uiFadeDuration = 0.5f;

    [Header("画面暗淡")]
    [Tooltip("暗淡时长（秒）")]
    [SerializeField] private float darkenDuration = 0.55f;

    [Tooltip("暗淡目标值（0=不暗，1=全黑）")]
    [Range(0f, 1f)]
    [SerializeField] private float darkenAmount = 0.93f;

    [Header("视差等待")]
    [Tooltip("背景视差滚动速度（UV单位/秒），等待加载期间背景向左流动造成光点飞行错觉")]
    [SerializeField] private float parallaxScrollSpeed = 0.08f;

    [Header("镜头平移")]
    [Tooltip("点击后等待多久才开始暗淡（秒），让粒子先自由爆开")]
    [SerializeField] private float initialDelay = 0.15f;

    [Tooltip("镜头平移总时长（秒）")]
    [SerializeField] private float slideDuration = 0.7f;

    [Tooltip("镜头向左平移的世界单位距离")]
    [SerializeField] private float cameraSlideX = 30f;

    [Tooltip("镜头平移期间背景 UV 额外横向偏移总量（叠加在视差之上）")]
    [SerializeField] private float bgScrollAmount = 0.3f;

    [Header("粒子跟随")]
    [Tooltip("粒子跟随镜头的比例，根据 Canvas 和相机比例关系调整")]
    [SerializeField] private float particleFollowScale = 100f;

    [Header("粒子淡出")]
    [Tooltip("进入新场景后粒子淡出时长（秒）")]
    [SerializeField] private float particleFadeOutDuration = 0.4f;

    // ─────────────────────────────────────────────────

    private bool _running = false;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void BeginTransition()
    {
        if (_running) return;
        _running = true;
        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        // ── 1. 收集所有 MenuButtonFX
        var allFX = new List<MenuButtonFX>(
            FindObjectsByType<MenuButtonFX>(FindObjectsSortMode.None));

        // ── 2. 从 CharacterSelectEntry 读 Canvas 世界X，算出推镜距离
        float camStartX    = mainCamera != null ? mainCamera.transform.position.x : 0f;
        float canvasWorldX = CharacterSelectEntry.CanvasWorldX;
        // CanvasWorldX 在场景刚加载时可能还是默认值 0，直接从 Canvas 取
        if (canvasWorldX == 0f && CharacterSelectEntry.Instance != null)
        {
            var entryCanvas = CharacterSelectEntry.Instance.GetComponentInChildren<Canvas>(true);
            if (entryCanvas != null)
                canvasWorldX = entryCanvas.transform.position.x;
        }
        float actualSlideX = camStartX - canvasWorldX;
        if (actualSlideX <= 0f)
        {
            actualSlideX = cameraSlideX;
            Debug.LogWarning("[HopeTransition] CanvasWorldX 未就绪，使用 Inspector 值");
        }
        Debug.Log($"[HopeTransition] canvasWorldX={canvasWorldX}, actualSlideX={actualSlideX}");

        // ── 3. 锁定主界面 Canvas 位置，防止它随相机移动
        if (mainCanvasScaler != null)
            mainCanvasScaler.lockPosition = true;

        // ── 4. 准备 UI CanvasGroup（为淡出目标挂 CanvasGroup）
        var uiGroups = new List<CanvasGroup>();
        foreach (var rt in uiFadeTargets)
        {
            if (rt == null) continue;
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            uiGroups.Add(cg);
        }

        // ── 4. 等待初始延迟，让粒子先自由爆开
        float t = 0f;
        while (t < initialDelay) { t += Time.deltaTime; yield return null; }

        // ── 5. 画面暗淡 + UI淡出（同步进行）
        t = 0f;
        float bgScrollAccum = 0f;
        while (t < darkenDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / darkenDuration);
            float ease     = 1f - Mathf.Pow(1f - progress, 3f);

            if (titleBackground != null)
                titleBackground.SetDarkness(ease * darkenAmount);

            float uiProgress = Mathf.Clamp01(t / uiFadeDuration);
            float uiEase     = 1f - Mathf.Pow(1f - uiProgress, 2f);
            foreach (var cg in uiGroups)
                cg.alpha = 1f - uiEase;

            yield return null;
        }

        if (titleBackground != null) titleBackground.SetDarkness(darkenAmount);
        foreach (var cg in uiGroups) cg.alpha = 0f;

        // ── 6. 视差跑一小段，让动画不突兀
        float pt = 0f;
        while (pt < 0.15f)
        {
            pt += Time.deltaTime;
            bgScrollAccum += parallaxScrollSpeed * Time.deltaTime;
            if (titleBackground != null)
                titleBackground.SetScrollX(bgScrollAccum);
            yield return null;
        }

        // ── 7. 镜头推进：背景继续滚 + 粒子跟随镜头
        Vector3 camStart = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        Vector3 camEnd   = camStart + Vector3.left * actualSlideX;
        Vector3 camPrev  = camStart;

        t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / slideDuration);
            float ease     = 1f - Mathf.Pow(1f - progress, 3f);

            Vector3 camCurrent = Vector3.LerpUnclamped(camStart, camEnd, ease);
            if (mainCamera != null)
                mainCamera.transform.position = camCurrent;

            // 背景：视差累计 + 镜头额外偏移
            bgScrollAccum += parallaxScrollSpeed * Time.deltaTime;
            if (titleBackground != null)
                titleBackground.SetScrollX(bgScrollAccum + ease * bgScrollAmount);

            // 粒子跟随镜头
            Vector3 worldDelta    = camCurrent - camPrev;
            camPrev               = camCurrent;
            Vector2 particleDelta = new Vector2(worldDelta.x * particleFollowScale,
                                                worldDelta.y * particleFollowScale);
            foreach (var fx in allFX)
                fx.AddParticleOffset(particleDelta);

            yield return null;
        }

        // ── 8. 精确到位
        if (mainCamera != null) mainCamera.transform.position = camEnd;

        // ── 9. 镜头到位，淡入新场景，同时粒子淡出
//       等两件事都完成，再卸载旧场景

bool revealDone = false;
if (CharacterSelectEntry.Instance != null)
    CharacterSelectEntry.Instance.Reveal(() => revealDone = true);

foreach (var fx in allFX)
    fx.StartForceFadeOut(particleFadeOutDuration);

// ★ 同时等 Reveal 完成 AND 粒子淡出完成，取较长的那个
float waited = 0f;
while (!revealDone || waited < particleFadeOutDuration)
{
    waited += Time.deltaTime;
    yield return null;
}

// ★ 不调用 SetActiveScene，避免闪烁

// ── 10. 两件事都完成，再卸载旧场景
for (int i = 0; i < SceneManager.sceneCount; i++)
{
    var scene = SceneManager.GetSceneAt(i);
    if (scene.name != "Hope_CharacterSelect" && scene.IsValid())
    {
        yield return SceneManager.UnloadSceneAsync(scene);
        break;
    }
}

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.name != "Hope_CharacterSelect" && scene.IsValid())
            {
                yield return SceneManager.UnloadSceneAsync(scene);
                break;
            }
        }
    } // end TransitionRoutine
} // end HopeTransition