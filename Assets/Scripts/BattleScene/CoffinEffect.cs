using UnityEngine;
using DG.Tweening;
using System.Collections;
using Action = System.Action;

/// <summary>
/// 万物归墟棺椁特效主控制器。
/// 挂在 Main Camera 上。
/// Glitch 故障风格：RGB错位 + 像素块随机错位，随机爆发式节奏直到黑屏。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CoffinEffect : MonoBehaviour
{
    public static CoffinEffect Instance { get; private set; }

    [Header("材质")]
    [Tooltip("拖入用 Custom/CoffinGlow Shader 创建的 Material")]
    [SerializeField] private Material coffinMaterial;

    [Header("时长（秒）")]
    [Tooltip("整个序列的总时长（含所有爆发）")]
    [SerializeField] private float totalDuration = 4.0f;
    [Tooltip("黑屏保持时长")]
    [SerializeField] private float blackoutHold  = 0.6f;

    [Header("爆发参数")]
    [Tooltip("爆发次数（不含最后一次致命爆发）")]
    [SerializeField] private int   burstCount       = 4;
    [Tooltip("单次爆发持续时长范围")]
    [SerializeField] private float burstDurMin      = 0.15f;
    [SerializeField] private float burstDurMax      = 0.45f;
    [Tooltip("爆发时故障强度范围（随序列进行而整体提高）")]
    [SerializeField] private float burstIntensMin   = 0.5f;
    [SerializeField] private float burstIntensMax   = 0.95f;
    [Tooltip("平静期基线强度（轻微故障，表示系统不稳定）")]
    [SerializeField] private float idleIntensity    = 0.06f;

    [Header("音效（可选）")]
    [SerializeField] private AudioClip glitchBurstClip;
    [SerializeField] private AudioClip powerDownClip;

    private float       _intensity  = 0f;
    private AudioSource _sfxSource;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (coffinMaterial == null || _intensity <= 0.001f)
        {
            Graphics.Blit(src, dst);
            return;
        }
        coffinMaterial.SetFloat("_Intensity", _intensity);
        coffinMaterial.SetFloat("_Seed",      Random.Range(0f, 100f));
        Graphics.Blit(src, dst, coffinMaterial);
    }

    // ─────────────────────────────────────────────────
    // 主入口
    //
    // onShowMessage : 序列最开始调用（弹出翻牌消息）
    // onBlackout    : 黑屏切入瞬间调用（销毁模块/手牌）
    // ─────────────────────────────────────────────────
    public IEnumerator PlaySequence(Action onShowMessage = null, Action onBlackout = null)
    {
        _intensity = 0f;
        EnsureAudioSources();

        // 序列开始：弹出消息
        onShowMessage?.Invoke();

        // 把总时长分配给 burstCount 次爆发 + 间隔
        // 最后保留 0.3s 给致命爆发
        float availableTime = totalDuration - 0.3f;
        float timePerSlot   = availableTime / burstCount;

        for (int i = 0; i < burstCount; i++)
        {
            // 随序列进行，爆发强度基线整体提高
            float progress      = (float)i / burstCount;
            float intensityFloor = Mathf.Lerp(burstIntensMin, burstIntensMax * 0.7f, progress);
            float burstIntens   = Random.Range(intensityFloor, burstIntensMax * Mathf.Lerp(0.75f, 1.0f, progress));

            // 爆发持续时长随机
            float burstDur = Random.Range(burstDurMin, burstDurMax);

            // 爆发前的平静等待（随机，但总时长受控）
            float waitBefore = Random.Range(timePerSlot * 0.2f, timePerSlot - burstDur);
            waitBefore = Mathf.Max(waitBefore, 0.05f);

            // ── 平静期：轻微基线故障 ─────────────────
            yield return StartCoroutine(IdlePeriod(waitBefore));

            // ── 爆发期 ───────────────────────────────
            PlayBurstSound();
            yield return StartCoroutine(BurstPeriod(burstIntens, burstDur));
        }

        // ── 致命爆发：强度冲到最大，不再平静 ────────
        PlayBurstSound();
        yield return StartCoroutine(BurstPeriod(1.0f, 0.3f));

        // ── 黑屏切入 ─────────────────────────────────
        StopAllAudio();
        PlayPowerDown();
        _intensity = 0f;

        onBlackout?.Invoke();

        var overlay = CoffinOverlay.Instance;
        if (overlay != null)
            yield return StartCoroutine(overlay.PlayBlackout(blackoutHold));
        else
            yield return new WaitForSeconds(blackoutHold);

        _intensity = 0f;
    }

    // ─────────────────────────────────────────────────
    // 平静期：维持 idleIntensity 的轻微故障
    // ─────────────────────────────────────────────────
    private IEnumerator IdlePeriod(float duration)
    {
        float elapsed = 0f;
        // 快速降到 idle 水平
        yield return DOTween.To(() => _intensity, v => _intensity = v, idleIntensity, 0.08f)
                            .WaitForCompletion();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // idle 期间轻微抖动，增加不稳定感
            _intensity = idleIntensity + Mathf.Sin(Time.time * 23.0f) * 0.02f
                       + Random.Range(-0.01f, 0.01f);
            _intensity = Mathf.Max(_intensity, 0f);
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────
    // 爆发期：强度瞬间冲高，随机抖动，然后快速衰减
    // ─────────────────────────────────────────────────
    private IEnumerator BurstPeriod(float peakIntensity, float duration)
    {
        // 瞬间冲到峰值
        _intensity = peakIntensity;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 峰值附近随机抖动，制造不规则感
            float jitter = Random.Range(-0.12f, 0.12f) * (1.0f - t * 0.5f);
            _intensity = Mathf.Clamp01(peakIntensity + jitter);

            // 后半段开始衰减
            if (t > 0.5f)
                _intensity *= Mathf.Lerp(1.0f, 0.3f, (t - 0.5f) * 2.0f);

            yield return null;
        }
    }

    // ─────────────────────────────────────────────────
    // 音效
    // ─────────────────────────────────────────────────
    private void EnsureAudioSources()
    {
        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake  = false;
            _sfxSource.spatialBlend = 0f;
        }
    }

    private void PlayBurstSound()
    {
        if (glitchBurstClip == null || _sfxSource == null) return;
        _sfxSource.clip   = glitchBurstClip;
        _sfxSource.volume = Random.Range(0.5f, 0.9f);
        _sfxSource.Play();
    }

    private void PlayPowerDown()
    {
        if (powerDownClip == null || _sfxSource == null) return;
        _sfxSource.clip   = powerDownClip;
        _sfxSource.volume = 0.9f;
        _sfxSource.Play();
    }

    private void StopAllAudio()
    {
        if (_sfxSource != null) _sfxSource.Stop();
    }
}