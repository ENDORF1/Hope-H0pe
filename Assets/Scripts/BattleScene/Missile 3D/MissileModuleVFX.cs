using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 挂在导弹模块卡牌根对象上。
///
/// 完整时序：
///   CallAppear()         — 进入导弹阶段：整体从下方升起，进入待机
///   CallOpeningAnimation()— 导弹即将发射：底座弹开→发射管升起→碎块飞散
///   GetTubeMuzzlePositions()— 每发 FireMissile 取管口位置
///   CallDisappear()      — 导弹阶段结束：碎块归位→发射管降下→底座合拢→整体下沉销毁
///
/// 子对象命名约定：
///   Tube001/002/003  — 发射管（初始藏在底座以下）
///   BaseLeft         — 左底座
///   BaseRight        — 右底座
///   Fragment001…     — 弹飞小碎块
/// </summary>
[RequireComponent(typeof(ModuleInstance))]
public class MissileModuleVFX : MonoBehaviour
{
    [Header("预制体")]
    [Tooltip("拖入 MissileLauncher 的 Prefab（3D导弹发射架模型）")]
    public GameObject launcherPrefab;

    [Header("NPR描边材质（可选）")]
    [Tooltip("拖入使用 Custom/NPROutline Shader 的材质，生成时自动替换底座材质")]
    public Material outlineMaterialBase;
    [Tooltip("拖入使用 Custom/NPROutline Shader 的材质，生成时自动替换发射管材质")]
    public Material outlineMaterialTube;
    [Tooltip("拖入使用 Custom/NPROutline Shader 的材质，生成时自动替换碎块材质")]
    public Material outlineMaterialFragment;

    [Header("位置偏移（相对卡牌）")]
    [Tooltip("发射架生成位置相对于卡牌中心的偏移。Z为负值表示向屏幕外凸出，让模型浮在卡牌表面前方。")]
    public Vector3 spawnOffset = new Vector3(0f, 0f, -0.5f);

    [Tooltip("管口位置沿Forward方向的额外偏移，用于微调弹体生成位置。正值向屏幕外，负值向内。")]
    public float muzzleOffset = 0.3f;

    // ── 浮现 ──────────────────────────────────────────
    [Header("① 浮现动画（导弹阶段开始时）")]
    [Tooltip("模型从卡牌下方多远的位置开始升起。负值越大，初始藏得越深，升起过程越长。单位：世界单位。")]
    public float appearStartDepth  = -0.8f;
    [Tooltip("模型从下方升起到位的总时长（秒）。值越大升起越慢。")]
    public float appearDuration    = 0.9f;

    // ── 待机 ──────────────────────────────────────────
    [Header("② 待机浮动（升起后持续播放）")]
    [Tooltip("碎块待机时上下浮动的幅度（世界单位）。值越大浮动越明显。")]
    public float idleFloatAmplitude  = 0.04f;
    [Tooltip("碎块待机时随机摇晃的最大旋转角度（度）。值越大晃动越夸张。")]
    public float idleRotateAmplitude = 3f;

    // ── 底座弹开 ──────────────────────────────────────
    [Header("③ 底座弹开（开幕动画第一步）")]
    [Tooltip("底座弹开前震动蓄力的幅度（世界单位）。值越大震动越剧烈。")]
    public float shakeAmplitude      = 0.04f;
    [Tooltip("底座震动蓄力持续的时长（秒）。")]
    public float shakeDuration       = 0.15f;
    [Tooltip("底座震动的频率（次/秒）。值越大震动越密集。")]
    public float shakeFrequency      = 25f;
    [Tooltip("底座弹出前向内后缩的距离（世界单位）。蓄力感来源，值越大弹出越有力。")]
    public float baseWindbackDist    = 0.06f;
    [Tooltip("底座后缩动作的持续时长（秒）。")]
    public float baseWindbackDur     = 0.05f;
    [Tooltip("底座弹开飞出的距离（世界单位）。决定两侧底座最终分开多远。")]
    public float baseLaunchDist      = 0.55f;
    [Tooltip("底座弹开飞出动作的持续时长（秒）。值越小弹出越快。")]
    public float baseLaunchDur       = 0.12f;
    [Tooltip("底座弹开后继续多飞出的过冲距离（世界单位）。过冲后会弹回，产生弹性感。")]
    public float baseOvershootDist   = 0.07f;
    [Tooltip("底座过冲动作的持续时长（秒）。")]
    public float baseOvershootDur    = 0.04f;
    [Tooltip("底座从过冲位置弹回到最终停止位置的时长（秒）。")]
    public float baseSettleDur       = 0.06f;

    // ── 发射管升起 ────────────────────────────────────
    [Header("④ 发射管升起（开幕动画第二步）")]
    [Tooltip("发射管初始隐藏时相对于其原位置向下偏移多少（世界单位，填负值）。值越小（越负）藏得越深。")]
    public float tubeHideOffset      = -0.35f;
    [Tooltip("三根发射管从底部升起到位的总时长（秒）。")]
    public float tubeRiseDuration    = 0.7f;
    [Tooltip("发射管升起过程中左右摇晃的幅度（世界单位）。模拟液压机构不稳定的感觉。")]
    public float tubeWobbleAmplitude = 0.025f;
    [Tooltip("发射管升起时左右摇晃的频率（次/秒）。")]
    public float tubeWobbleFrequency = 8f;
    [Tooltip("升起进度到达此比例后开始收敛摇晃（0~1）。例如0.75表示升起75%后逐渐稳定。")]
    public float tubeSettleStart     = 0.75f;

    // ── 碎块弹飞 ──────────────────────────────────────
    [Header("⑤ 碎块弹飞（开幕动画第三步）")]
    [Tooltip("碎块弹飞前向中心后缩的距离（世界单位）。蓄力感来源。")]
    public float fragWindbackDist    = 0.04f;
    [Tooltip("碎块后缩动作的持续时长（秒）。")]
    public float fragWindbackDur     = 0.05f;
    [Tooltip("碎块向外飞散的距离（世界单位）。值越大碎块飞得越远。")]
    public float fragLaunchDist      = 0.7f;
    [Tooltip("碎块飞出动作的持续时长（秒）。值越小飞出越快。")]
    public float fragLaunchDur       = 0.1f;
    [Tooltip("碎块飞出后继续多飞的过冲距离（世界单位）。")]
    public float fragOvershootDist   = 0.08f;
    [Tooltip("碎块过冲动作的持续时长（秒）。")]
    public float fragOvershootDur    = 0.04f;
    [Tooltip("碎块从过冲位置弹回到最终停止位置的时长（秒）。")]
    public float fragSettleDur       = 0.08f;
    [Tooltip("碎块飞出时随机旋转的最大角度（度）。增加飞散时的混乱感。")]
    public float fragRotateDeg       = 35f;

    // ── 归位 ──────────────────────────────────────────
    [Header("⑥ 归位动画（导弹阶段结束时）")]
    [Tooltip("碎块从飞散位置归回原位的总时长（秒）。")]
    public float fragReturnDur       = 0.5f;
    [Tooltip("发射管从升起位置降回隐藏位置的总时长（秒）。")]
    public float tubeDescendDur      = 0.4f;
    [Tooltip("底座合拢前震动蓄力的持续时长（秒）。")]
    public float baseReturnShakeDur  = 0.12f;
    [Tooltip("底座从展开位置合拢回原位的总时长（秒）。")]
    public float baseReturnDur       = 0.18f;
    [Tooltip("底座合拢撞击后冲击震动的持续时长（秒）。")]
    public float baseImpactShakeDur  = 0.15f;
    [Tooltip("底座合拢冲击震动的幅度（世界单位）。模拟两侧底座撞在一起的震感。")]
    public float baseImpactAmplitude = 0.03f;

    // ── 下沉消失 ──────────────────────────────────────
    [Header("⑦ 下沉消失（归位完成后）")]
    [Tooltip("整个发射架向下沉入卡牌并消失时下沉的距离（世界单位）。")]
    public float sinkDepth           = 1.0f;
    [Tooltip("下沉消失动画的总时长（秒）。值越小消失越快。")]
    public float sinkDuration        = 0.5f;

    // ─────────────────────────────────────────────────
    // 运行时
    // ─────────────────────────────────────────────────
    private ModuleInstance _module;
    private GameObject     _launcher;
    private bool           _idleRunning;

    // 子对象缓存
    private Transform[]  _tubes;
    private Transform    _baseLeft;
    private Transform    _baseRight;
    private Transform[]  _fragments;

    // 原始局部坐标（用于归位和动画基准）
    private Vector3[]    _tubeOrigLocalPos;
    private Vector3      _baseLeftOrigPos;
    private Vector3      _baseRightOrigPos;
    private Vector3[]    _fragOrigLocalPos;
    private Quaternion[] _fragOrigLocalRot;

    // 碎块弹飞终点（归位用）
    private Vector3[]    _fragFlyEndPos;
    private Quaternion[] _fragFlyEndRot;

    // 底座弹开终点（归位用）
    private Vector3      _baseLeftFlyEnd;
    private Vector3      _baseRightFlyEnd;

    // 碎块淡入用的 Renderer 缓存
    private Renderer[][] _fragRenderers;

    // 卡牌局部坐标轴缓存（生成时记录，所有动画用这些方向）
    private Vector3      _localUp;
    private Vector3      _localRight;
    private Vector3      _localForward; // 朝向玩家方向（Z轴负方向）

    // 待机参数
    private struct IdleParams
    {
        public float floatPeriod;
        public float rotatePeriod;
        public float phase;
        public Vector3 rotateAxis;
    }
    private IdleParams[] _idleParams;

    // ─────────────────────────────────────────────────
    void Awake()
    {
        _module = GetComponent<ModuleInstance>();
        // 默认禁用，等 OnModuleInitialized 确认是导弹模块后再启用
        enabled = false;
    }

    /// <summary>
    /// 由 ModuleRuntimeBridge.OnInitialized() 调用。
    /// 确认是导弹模块后才启用本脚本。
    /// </summary>
    public void OnModuleInitialized()
    {
        if (_module == null || _module.Asset == null) return;
        if (_module.Asset.ModuleType != ModuleType.Missile) return;
        enabled = true;
    }

    void OnEnable()
    {
        if (_module != null) _module.OnFaceStateChanged += OnFaceStateChanged;
    }

    void OnDisable()
    {
        if (_module != null) _module.OnFaceStateChanged -= OnFaceStateChanged;
    }

    private void OnFaceStateChanged(bool isFaceDown)
    {
        // 翻开/盖伏时不做任何3D表现，由 GameManager 显式调用 CallAppear
    }

    // ─────────────────────────────────────────────────
    // 生成 & 缓存
    // ─────────────────────────────────────────────────

    private void SpawnLauncher()
    {
        if (launcherPrefab == null || _launcher != null) return;

        Vector3 pos = transform.position + transform.TransformDirection(spawnOffset);
        // 修正：使用 Quaternion.identity 生成，避免继承卡牌世界旋转导致所有模型朝向画布中心
        _launcher = Instantiate(launcherPrefab, pos, Quaternion.identity);
        _launcher.transform.SetParent(transform, worldPositionStays: true);
        _launcher.transform.localRotation = Quaternion.identity;

        // 缓存卡牌局部坐标轴，所有动画方向统一用这些，避免世界轴与局部轴混用
        _localUp      = transform.up;
        _localRight   = transform.right;
        _localForward = -transform.forward; // 朝玩家方向（从卡牌内部向外冒出）

        // 设置所有子对象Renderer的Sorting Layer为For3DEffects，确保渲染在最高层
        foreach (var r in _launcher.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerName = "For3DEffects";
            r.sortingOrder     = 0;
        }

        CacheChildren();
        SetTubesHidden(true);
    }

    private void CacheChildren()
    {
        _tubes     = GetChildrenWith(_launcher.transform, "Tube");
        _baseLeft  = FindChild(_launcher.transform, "BaseLeft");
        _baseRight = FindChild(_launcher.transform, "BaseRight");
        _fragments = GetChildrenWith(_launcher.transform, "Fragment");

        // 弹体永久隐藏，发射时由脚本实例化新对象
        Transform projectile = FindChild(_launcher.transform, "MissileProjectile");
        if (projectile != null) projectile.gameObject.SetActive(false);

        // 替换描边材质（如果Inspector里填了的话）
        if (outlineMaterialBase != null)
        {
            ApplyMaterialTo(_baseLeft,  outlineMaterialBase);
            ApplyMaterialTo(_baseRight, outlineMaterialBase);
        }
        if (outlineMaterialTube != null)
            foreach (var tube in _tubes) ApplyMaterialTo(tube, outlineMaterialTube);
        if (outlineMaterialFragment != null)
            foreach (var frag in _fragments) ApplyMaterialTo(frag, outlineMaterialFragment);

        // 记录原始局部坐标
        _tubeOrigLocalPos = new Vector3[_tubes.Length];
        for (int i = 0; i < _tubes.Length; i++)
            _tubeOrigLocalPos[i] = _tubes[i].localPosition;

        if (_baseLeft  != null) _baseLeftOrigPos  = _baseLeft.localPosition;
        if (_baseRight != null) _baseRightOrigPos = _baseRight.localPosition;

        _fragOrigLocalPos = new Vector3[_fragments.Length];
        _fragOrigLocalRot = new Quaternion[_fragments.Length];
        for (int i = 0; i < _fragments.Length; i++)
        {
            _fragOrigLocalPos[i] = _fragments[i].localPosition;
            _fragOrigLocalRot[i] = _fragments[i].localRotation;
        }

        _fragFlyEndPos = new Vector3[_fragments.Length];
        _fragFlyEndRot = new Quaternion[_fragments.Length];

        // 缓存碎块所有Renderer，初始化为透明（淡入用）
        _fragRenderers = new Renderer[_fragments.Length][];
        for (int i = 0; i < _fragments.Length; i++)
        {
            _fragRenderers[i] = _fragments[i].GetComponentsInChildren<Renderer>(true);
            foreach (var r in _fragRenderers[i])
            {
                SetRendererFadeMode(r);  // 只切一次模式
                SetRendererAlpha(r, 0f); // 初始透明
            }
        }
    }

    // 发射管藏在底座以下
    private void SetTubesHidden(bool hidden)
    {
        if (_tubes == null) return;
        for (int i = 0; i < _tubes.Length; i++)
        {
            if (_tubes[i] == null) continue;
            _tubes[i].localPosition = hidden
                ? _tubeOrigLocalPos[i] + _localForward * tubeHideOffset
                : _tubeOrigLocalPos[i];
        }
    }

    // ─────────────────────────────────────────────────
    // 公开接口（由 GameManager / CombatEngine 调用）
    // ─────────────────────────────────────────────────

    /// <summary>进入导弹阶段时调用：生成模型 + 整体从下方升起 + 进入待机</summary>
    public IEnumerator CallAppear()
    {
        SpawnLauncher();
        if (_launcher == null) yield break;

        // 初始位置压到卡牌内部
        Vector3 origPos = _launcher.transform.localPosition;
        Vector3 startPos = origPos + _localForward * appearStartDepth;
        _launcher.transform.localPosition = startPos;

        float elapsed = 0f;
        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / appearDuration);
            _launcher.transform.localPosition = Vector3.Lerp(startPos, origPos, t);

            // 碎块随升起同步淡入
            if (_fragRenderers != null)
                foreach (var renderers in _fragRenderers)
                    foreach (var r in renderers)
                        SetRendererAlpha(r, t);

            yield return null;
        }
        _launcher.transform.localPosition = origPos;

        StartIdleAnimation();
    }

    /// <summary>导弹即将发射时调用：底座弹开→发射管升起→碎块飞散</summary>
    public IEnumerator CallOpeningAnimation()
    {
        if (_launcher == null) yield break;
        _idleRunning = false;

        // 1. 底座震动 + 弹开
        yield return StartCoroutine(AnimateBasesOpen());

        // 2. 发射管升起
        yield return StartCoroutine(AnimateTubesRise());

        // 3. 碎块飞散（不等待，让碎块飞出后自行进入待机）
        StartCoroutine(AnimateFragmentsFly());
        yield return new WaitForSeconds(fragLaunchDur + fragOvershootDur + fragSettleDur);
    }

    /// <summary>导弹阶段结束时调用：归位 + 下沉消失</summary>
    public IEnumerator CallDisappear()
    {
        if (_launcher == null) yield break;
        _idleRunning = false;

        // 1. 碎块归位
        yield return StartCoroutine(AnimateFragmentsReturn());

        // 2. 发射管降下
        yield return StartCoroutine(AnimateTubesDescend());

        // 3. 底座合拢
        yield return StartCoroutine(AnimateBasesClose());

        // 4. 整体下沉消失
        yield return StartCoroutine(AnimateSink());

        Destroy(_launcher);
        _launcher = null;
    }

    /// <summary>取所有发射管管口世界坐标（用Bounds取管子顶端）</summary>
    public Vector3[] GetTubeMuzzlePositions()
    {
        if (_tubes == null || _tubes.Length == 0)
            return new Vector3[] { transform.position };

        var positions = new Vector3[_tubes.Length];
        for (int i = 0; i < _tubes.Length; i++)
        {
            if (_tubes[i] == null)
            {
                positions[i] = transform.position;
                continue;
            }

            // 用Renderer的Bounds取管子在_localForward方向上的最远点（管口）
            Renderer r = _tubes[i].GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Bounds b = r.bounds;
                // 沿_localForward方向投影，取最远端
                Vector3 center  = b.center;
                Vector3 extents = b.extents;
                // 在localForward方向上加上extents的投影量
                float proj = Mathf.Abs(Vector3.Dot(_localForward, new Vector3(extents.x, extents.y, extents.z)));
                positions[i] = center + _localForward * proj;
            }
            else
            {
                positions[i] = _tubes[i].position + _localForward * muzzleOffset;
            }
        }
        return positions;
    }

    // ─────────────────────────────────────────────────
    // 待机动画
    // ─────────────────────────────────────────────────

    private void StartIdleAnimation()
    {
        if (_fragments == null || _fragments.Length == 0) return;

        float[] periods = { 0.3f, 0.5f, 1.0f };
        _idleParams = new IdleParams[_fragments.Length];
        for (int i = 0; i < _fragments.Length; i++)
        {
            _idleParams[i] = new IdleParams
            {
                floatPeriod  = periods[Random.Range(0, periods.Length)] + Random.Range(-0.05f, 0.05f),
                rotatePeriod = periods[Random.Range(0, periods.Length)] + Random.Range(-0.05f, 0.05f),
                phase        = Random.Range(0f, Mathf.PI * 2f),
                rotateAxis   = Random.onUnitSphere
            };
        }
        _idleRunning = true;
        StartCoroutine(IdleLoop());
    }

    private IEnumerator IdleLoop()
    {
        while (_idleRunning && _launcher != null)
        {
            float t = Time.time;
            for (int i = 0; i < _fragments.Length; i++)
            {
                if (_fragments[i] == null) continue;
                var p = _idleParams[i];
                float floatY   = Mathf.Sin(t / p.floatPeriod  * Mathf.PI * 2f + p.phase) * idleFloatAmplitude;
                float rotAngle = Mathf.Sin(t / p.rotatePeriod * Mathf.PI * 2f + p.phase) * idleRotateAmplitude;

                // 飞散后用 flyEnd 作为待机基准
                Vector3    basePos = _fragFlyEndPos[i] != Vector3.zero ? _fragFlyEndPos[i] : _fragOrigLocalPos[i];
                Quaternion baseRot = _fragFlyEndRot[i] != Quaternion.identity ? _fragFlyEndRot[i] : _fragOrigLocalRot[i];

                _fragments[i].localPosition = basePos + _localForward * floatY;
                _fragments[i].localRotation = baseRot * Quaternion.AngleAxis(rotAngle, p.rotateAxis);
            }
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────
    // 底座弹开
    // ─────────────────────────────────────────────────

    private IEnumerator AnimateBasesOpen()
    {
        // 计算弹开方向：从原点到各自中心的方向（局部坐标）
        Vector3 leftDir  = _baseLeft  != null ? (_baseLeftOrigPos  - Vector3.zero).normalized : Vector3.left;
        Vector3 rightDir = _baseRight != null ? (_baseRightOrigPos - Vector3.zero).normalized : Vector3.right;

        // 震动蓄力
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float shake = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * shakeAmplitude
                          * (1f - elapsed / shakeDuration);
            if (_baseLeft  != null) _baseLeft.localPosition  = _baseLeftOrigPos  + leftDir  * shake;
            if (_baseRight != null) _baseRight.localPosition = _baseRightOrigPos + rightDir * shake;
            yield return null;
        }

        // 后缩
        yield return StartCoroutine(MoveBase(
            _baseLeftOrigPos  - leftDir  * baseWindbackDist,
            _baseRightOrigPos - rightDir * baseWindbackDist,
            baseWindbackDur, AnimCurve.Linear));

        // 快速弹出
        Vector3 leftLaunch  = _baseLeftOrigPos  + leftDir  * baseLaunchDist;
        Vector3 rightLaunch = _baseRightOrigPos + rightDir * baseLaunchDist;
        yield return StartCoroutine(MoveBase(leftLaunch, rightLaunch, baseLaunchDur, AnimCurve.ExpoOut));

        // 过冲
        yield return StartCoroutine(MoveBase(
            leftLaunch  + leftDir  * baseOvershootDist,
            rightLaunch + rightDir * baseOvershootDist,
            baseOvershootDur, AnimCurve.Linear));

        // 弹回终点
        _baseLeftFlyEnd  = leftLaunch;
        _baseRightFlyEnd = rightLaunch;
        yield return StartCoroutine(MoveBase(leftLaunch, rightLaunch, baseSettleDur, AnimCurve.SmoothStep));
    }

    private IEnumerator MoveBase(Vector3 leftTarget, Vector3 rightTarget, float dur, AnimCurve curve)
    {
        Vector3 leftStart  = _baseLeft  != null ? _baseLeft.localPosition  : Vector3.zero;
        Vector3 rightStart = _baseRight != null ? _baseRight.localPosition : Vector3.zero;

        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Evaluate(curve, Mathf.Clamp01(elapsed / dur));
            if (_baseLeft  != null) _baseLeft.localPosition  = Vector3.Lerp(leftStart,  leftTarget,  t);
            if (_baseRight != null) _baseRight.localPosition = Vector3.Lerp(rightStart, rightTarget, t);
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────
    // 发射管升起（颤颤巍巍）
    // ─────────────────────────────────────────────────

    private IEnumerator AnimateTubesRise()
    {
        if (_tubes == null || _tubes.Length == 0) yield break;

        Vector3[] startPos = new Vector3[_tubes.Length];
        for (int i = 0; i < _tubes.Length; i++)
            startPos[i] = _tubes[i].localPosition; // 当前在 hideOffset 位置

        float elapsed = 0f;
        while (elapsed < tubeRiseDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / tubeRiseDuration);
            float t = Mathf.SmoothStep(0f, 1f, progress);

            // 摇晃幅度随进度收敛
            float wobbleDecay = progress < tubeSettleStart
                ? 1f
                : 1f - (progress - tubeSettleStart) / (1f - tubeSettleStart);
            float wobble = Mathf.Sin(elapsed * tubeWobbleFrequency * Mathf.PI * 2f)
                           * tubeWobbleAmplitude * wobbleDecay;

            for (int i = 0; i < _tubes.Length; i++)
            {
                if (_tubes[i] == null) continue;
                Vector3 lerpPos = Vector3.Lerp(startPos[i], _tubeOrigLocalPos[i], t);
                _tubes[i].localPosition = lerpPos + _tubes[i].right * wobble;
            }
            yield return null;
        }

        // 确保到位
        for (int i = 0; i < _tubes.Length; i++)
            if (_tubes[i] != null) _tubes[i].localPosition = _tubeOrigLocalPos[i];
    }

    // ─────────────────────────────────────────────────
    // 碎块弹飞
    // ─────────────────────────────────────────────────

    private IEnumerator AnimateFragmentsFly()
    {
        if (_fragments == null) yield break;

        Vector3[]    startPos = new Vector3[_fragments.Length];
        Quaternion[] startRot = new Quaternion[_fragments.Length];
        Vector3[]    windback = new Vector3[_fragments.Length];
        Vector3[]    launch   = new Vector3[_fragments.Length];
        Vector3[]    overshoot= new Vector3[_fragments.Length];

        for (int i = 0; i < _fragments.Length; i++)
        {
            startPos[i] = _fragments[i].localPosition;
            startRot[i] = _fragments[i].localRotation;

            // 方向：从原点指向碎块中心
            Vector3 dir = (startPos[i] - Vector3.zero).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Random.onUnitSphere;

            windback[i]  = startPos[i] - dir * fragWindbackDist;
            launch[i]    = startPos[i] + dir * fragLaunchDist;
            overshoot[i] = launch[i]   + dir * fragOvershootDist;

            _fragFlyEndPos[i] = launch[i];
            _fragFlyEndRot[i] = startRot[i] * Quaternion.AngleAxis(
                fragRotateDeg, Random.onUnitSphere);
        }

        // 后缩
        yield return StartCoroutine(LerpFragments(startPos, windback, startRot, startRot, fragWindbackDur, AnimCurve.Linear));
        // 快速冲出
        yield return StartCoroutine(LerpFragments(windback, overshoot, startRot, _fragFlyEndRot, fragLaunchDur, AnimCurve.ExpoOut));
        // 过冲弹回
        yield return StartCoroutine(LerpFragments(overshoot, launch, _fragFlyEndRot, _fragFlyEndRot, fragSettleDur, AnimCurve.SmoothStep));

        // 停在终点后恢复待机
        StartIdleAnimation();
    }

    private IEnumerator LerpFragments(
        Vector3[] fromPos, Vector3[] toPos,
        Quaternion[] fromRot, Quaternion[] toRot,
        float dur, AnimCurve curve)
    {
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Evaluate(curve, Mathf.Clamp01(elapsed / dur));
            for (int i = 0; i < _fragments.Length; i++)
            {
                if (_fragments[i] == null) continue;
                _fragments[i].localPosition = Vector3.Lerp(fromPos[i], toPos[i], t);
                _fragments[i].localRotation = Quaternion.Slerp(fromRot[i], toRot[i], t);
            }
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────
    // 归位：碎块
    // ─────────────────────────────────────────────────

    private IEnumerator AnimateFragmentsReturn()
    {
        if (_fragments == null) yield break;

        Vector3[]    fromPos = new Vector3[_fragments.Length];
        Quaternion[] fromRot = new Quaternion[_fragments.Length];
        for (int i = 0; i < _fragments.Length; i++)
        {
            fromPos[i] = _fragments[i].localPosition;
            fromRot[i] = _fragments[i].localRotation;
        }

        // 先快后慢：ExpoOut（冲出去）然后 SmoothStep 落回
        float elapsed = 0f;
        while (elapsed < fragReturnDur)
        {
            elapsed += Time.deltaTime;
            // 先快后慢用 1 - SmoothStep(1-t) 模拟
            float raw = Mathf.Clamp01(elapsed / fragReturnDur);
            float t   = 1f - Mathf.SmoothStep(0f, 1f, 1f - raw);
            for (int i = 0; i < _fragments.Length; i++)
            {
                if (_fragments[i] == null) continue;
                _fragments[i].localPosition = Vector3.Lerp(fromPos[i], _fragOrigLocalPos[i], t);
                _fragments[i].localRotation = Quaternion.Slerp(fromRot[i], _fragOrigLocalRot[i], t);
            }
            yield return null;
        }
        for (int i = 0; i < _fragments.Length; i++)
        {
            if (_fragments[i] == null) continue;
            _fragments[i].localPosition = _fragOrigLocalPos[i];
            _fragments[i].localRotation = _fragOrigLocalRot[i];
        }
    }

    // ─────────────────────────────────────────────────
    // 归位：发射管降下
    // ─────────────────────────────────────────────────

    private IEnumerator AnimateTubesDescend()
    {
        if (_tubes == null) yield break;

        Vector3[] startPos = new Vector3[_tubes.Length];
        Vector3[] hidePos  = new Vector3[_tubes.Length];
        for (int i = 0; i < _tubes.Length; i++)
        {
            startPos[i] = _tubes[i].localPosition;
            hidePos[i]  = _tubeOrigLocalPos[i] + _localForward * tubeHideOffset;
        }

        float elapsed = 0f;
        while (elapsed < tubeDescendDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / tubeDescendDur);
            for (int i = 0; i < _tubes.Length; i++)
            {
                if (_tubes[i] == null) continue;
                _tubes[i].localPosition = Vector3.Lerp(startPos[i], hidePos[i], t);
            }
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────
    // 归位：底座合拢（震动蓄力→撞合→冲击震动）
    // ─────────────────────────────────────────────────

    private IEnumerator AnimateBasesClose()
    {
        if (_baseLeft == null && _baseRight == null) yield break;

        Vector3 leftCur  = _baseLeft  != null ? _baseLeft.localPosition  : Vector3.zero;
        Vector3 rightCur = _baseRight != null ? _baseRight.localPosition : Vector3.zero;

        Vector3 leftDir  = (_baseLeftOrigPos  - leftCur).normalized;
        Vector3 rightDir = (_baseRightOrigPos - rightCur).normalized;

        // 震动蓄力
        float elapsed = 0f;
        while (elapsed < baseReturnShakeDur)
        {
            elapsed += Time.deltaTime;
            float shake = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f)
                          * shakeAmplitude * (1f - elapsed / baseReturnShakeDur);
            if (_baseLeft  != null) _baseLeft.localPosition  = leftCur  + leftDir  * shake;
            if (_baseRight != null) _baseRight.localPosition = rightCur + rightDir * shake;
            yield return null;
        }

        // 撞合
        yield return StartCoroutine(MoveBase(_baseLeftOrigPos, _baseRightOrigPos, baseReturnDur, AnimCurve.ExpoOut));

        // 合拢冲击震动
        elapsed = 0f;
        while (elapsed < baseImpactShakeDur)
        {
            elapsed += Time.deltaTime;
            float shake = Mathf.Sin(elapsed * shakeFrequency * 1.5f * Mathf.PI * 2f)
                          * baseImpactAmplitude * (1f - elapsed / baseImpactShakeDur);
            if (_baseLeft  != null) _baseLeft.localPosition  = _baseLeftOrigPos  + _localRight * shake;
            if (_baseRight != null) _baseRight.localPosition = _baseRightOrigPos - _localRight * shake;
            yield return null;
        }

        if (_baseLeft  != null) _baseLeft.localPosition  = _baseLeftOrigPos;
        if (_baseRight != null) _baseRight.localPosition = _baseRightOrigPos;
    }

    // ─────────────────────────────────────────────────
    // 整体下沉消失
    // ─────────────────────────────────────────────────

    private IEnumerator AnimateSink()
    {
        if (_launcher == null) yield break;

        Vector3 startPos = _launcher.transform.localPosition;
        Vector3 endPos   = startPos - _localForward * sinkDepth;

        float elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / sinkDuration);
            if (_launcher != null)
                _launcher.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────
    // 缓动曲线工具
    // ─────────────────────────────────────────────────

    private enum AnimCurve { Linear, SmoothStep, ExpoOut }

    private float Evaluate(AnimCurve curve, float t)
    {
        switch (curve)
        {
            case AnimCurve.SmoothStep: return Mathf.SmoothStep(0f, 1f, t);
            case AnimCurve.ExpoOut:    return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
            default:                   return t;
        }
    }

    // ─────────────────────────────────────────────────
    // 材质透明度工具
    // ─────────────────────────────────────────────────

    private void ApplyMaterialTo(Transform t, Material mat)
    {
        if (t == null || mat == null) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[r.materials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.materials = mats;
        }
    }

    // 一次性把材质切换为Fade模式（只在CacheChildren时调用一次）
    private void SetRendererFadeMode(Renderer r)
    {
        if (r == null) return;
        foreach (var mat in r.materials)
        {
            if (mat == null) continue;
            mat.SetFloat("_Mode", 2f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }

    // 只改alpha，不切换模式（每帧调用）
    private void SetRendererAlpha(Renderer r, float alpha)
    {
        if (r == null) return;
        foreach (var mat in r.materials)
        {
            if (mat == null) continue;
            // 兼容Standard(_Color)和NPROutline(_BaseColor)两种Shader
            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }
            else if (mat.HasProperty("_Color"))
            {
                Color c = mat.GetColor("_Color");
                c.a = alpha;
                mat.SetColor("_Color", c);
            }
        }
    }

    // ─────────────────────────────────────────────────
    // 子对象查找工具
    // ─────────────────────────────────────────────────

    private Transform[] GetChildrenWith(Transform parent, string keyword)
    {
        var result = new List<Transform>();
        FindChildrenRecursive(parent, keyword, result, exactMatch: false);
        return result.ToArray();
    }

    private Transform FindChild(Transform parent, string exactName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == exactName) return child;
            Transform found = FindChild(child, exactName);
            if (found != null) return found;
        }
        return null;
    }

    private void FindChildrenRecursive(Transform parent, string keyword, List<Transform> result, bool exactMatch)
    {
        foreach (Transform child in parent)
        {
            if (exactMatch ? child.name == keyword : child.name.Contains(keyword))
                result.Add(child);
            FindChildrenRecursive(child, keyword, result, exactMatch);
        }
    }
}