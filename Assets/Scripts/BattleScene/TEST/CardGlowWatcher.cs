using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在速攻牌 Prefab 上，监听 CardGlow 的 enabled 和 alpha 变化，
/// 一旦变化立刻打印调用栈，帮助定位是谁点亮了它。
/// 调试完成后删除此脚本。
/// </summary>
public class CardGlowWatcher : MonoBehaviour
{
    [Tooltip("要监听的 CardGlow Image，不填则自动在子对象里找名字含 Glow 的")]
    public Image[] watchTargets;

    private bool[]  _lastEnabled;
    private float[] _lastAlpha;

    void Awake()
    {
        if (watchTargets == null || watchTargets.Length == 0)
        {
            var all = GetComponentsInChildren<Image>(true);
            var list = new System.Collections.Generic.List<Image>();
            foreach (var img in all)
                if (img.gameObject.name.Contains("Glow")) list.Add(img);
            watchTargets = list.ToArray();
        }

        _lastEnabled = new bool[watchTargets.Length];
        _lastAlpha   = new float[watchTargets.Length];

        for (int i = 0; i < watchTargets.Length; i++)
        {
            if (watchTargets[i] == null) continue;
            _lastEnabled[i] = watchTargets[i].enabled;
            _lastAlpha[i]   = watchTargets[i].color.a;
            // Awake 时就检查一次初始状态
            if (_lastEnabled[i] || _lastAlpha[i] > 0.01f)
                Debug.LogWarning($"[CardGlowWatcher] Awake时已亮: {gameObject.name}/{watchTargets[i].gameObject.name} enabled={_lastEnabled[i]} alpha={_lastAlpha[i]:F2}", gameObject);
        }
    }

    void OnEnable()
    {
        if (watchTargets == null) return;
        for (int i = 0; i < watchTargets.Length; i++)
        {
            if (watchTargets[i] == null) continue;
            bool  curEnabled = watchTargets[i].enabled;
            float curAlpha   = watchTargets[i].color.a;
            if (curEnabled || curAlpha > 0.01f)
                Debug.LogWarning($"[CardGlowWatcher] OnEnable时已亮: {gameObject.name}/{watchTargets[i].gameObject.name} enabled={curEnabled} alpha={curAlpha:F2}\n{System.Environment.StackTrace}", gameObject);
        }
    }

    void Update()
    {
        for (int i = 0; i < watchTargets.Length; i++)
        {
            var img = watchTargets[i];
            if (img == null) continue;

            bool  curEnabled = img.enabled;
            float curAlpha   = img.color.a;

            if (curEnabled != _lastEnabled[i] || Mathf.Abs(curAlpha - _lastAlpha[i]) > 0.01f)
            {
                Debug.LogWarning(
                    $"[CardGlowWatcher] {gameObject.name} / {img.gameObject.name} 状态变化！\n" +
                    $"enabled: {_lastEnabled[i]} → {curEnabled}\n" +
                    $"alpha:   {_lastAlpha[i]:F2} → {curAlpha:F2}\n" +
                    $"调用栈:\n{System.Environment.StackTrace}",
                    gameObject);

                _lastEnabled[i] = curEnabled;
                _lastAlpha[i]   = curAlpha;
            }
        }
    }
}