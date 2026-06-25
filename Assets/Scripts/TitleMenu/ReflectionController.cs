using UnityEngine;
using DG.Tweening;

/// <summary>
/// 挂在场景任意常驻对象上。
/// 监听阵营切换，同步 ReflectionDisplay 的色调颜色。
/// </summary>
public class ReflectionController : MonoBehaviour
{
    public static ReflectionController Instance { get; private set; }

    [Header("ReflectionDisplay 引用")]
    [SerializeField] private ReflectionDisplay reflectionDisplay;

    [Header("阵营色调")]
    [SerializeField] private Color hopeTint = new Color(0.85f, 0.92f, 1.0f, 1f);
    [SerializeField] private Color voidTint  = new Color(1.0f,  0.85f, 0.85f, 1f);

    [Header("切换时长")]
    [SerializeField] private float switchDuration = 0.8f;

    private Color _currentTint;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        _currentTint = hopeTint;
        if (reflectionDisplay != null)
            reflectionDisplay.SetTintColor(_currentTint);
        TitleScreenManager.OnFactionChanged += OnFactionChanged;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        TitleScreenManager.OnFactionChanged -= OnFactionChanged;
    }

    private void OnFactionChanged(TitleScreenManager.Faction faction)
    {
        if (reflectionDisplay == null) return;
        Color target = faction == TitleScreenManager.Faction.Hope ? hopeTint : voidTint;
        DOTween.To(
            ()  => _currentTint,
            c   => { _currentTint = c; reflectionDisplay.SetTintColor(c); },
            target,
            switchDuration
        );
    }
}