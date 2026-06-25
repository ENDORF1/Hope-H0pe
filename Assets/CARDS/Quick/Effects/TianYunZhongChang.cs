using UnityEngine;
using System.Collections;

/// <summary>
/// 天允终偿 · 妹妹·希望
/// 不挂在任何 Prefab 上，由 QuickPlayExecutor 通过反射动态挂载并调用。
/// 协程挂在 SceneRefs 上执行，避免新挂载组件 StartCoroutine 失败的问题。
/// </summary>
public class TianYunZhongChang : MonoBehaviour
{
    public void Execute(PlayerState caster, PlayerState enemy,
                        HandManager casterHand, DeckManager casterDeck)
    {
        Debug.Log("[TianYunZhongChang] Execute 被调用");

        var refs = SceneRefs.Instance;
        if (refs == null)
        {
            Debug.LogError("[TianYunZhongChang] 找不到 SceneRefs");
            return;
        }

        bool isPlayer = (caster == refs.PlayerState);
        refs.StartCoroutine(ExecuteRoutine(casterHand, casterDeck, isPlayer));
    }

    private IEnumerator ExecuteRoutine(HandManager hand, DeckManager deck, bool isPlayer)
    {
        Debug.Log("[TianYunZhongChang] ExecuteRoutine 开始");

        var refs   = SceneRefs.Instance;
        var fx     = TianYunEffects.Instance;
        var glowFx = EdgeGlowEffect.Instance;

        if (refs?.GameManager == null)
        {
            Debug.LogError("[TianYunZhongChang] SceneRefs.GameManager 未赋值");
            yield break;
        }
        if (deck == null)
        {
            Debug.LogError("[TianYunZhongChang] casterDeck 为空");
            yield break;
        }

        var gameManager = refs.GameManager;

        // ── 1. 边框内发光开始呼吸 ────────────────────
        Debug.Log("[TianYunZhongChang] 步骤1：边框发光");
        if (glowFx != null)
            yield return refs.StartCoroutine(glowFx.StartBreathing());

        // ── 2. 牌库彩虹循环开始（和抽牌并行）─────────
        Debug.Log("[TianYunZhongChang] 步骤2：彩虹+抽牌并行");
        if (fx != null)
            refs.StartCoroutine(fx.StartDeckRainbow(isPlayer)); // 不 yield，并行运行

        // ── 3. 抽取施法方所有剩余卡牌 ────────────────
        Debug.Log("[TianYunZhongChang] 步骤3：抽牌");
        hand?.SetIgnoreHandLimit(true);

        int remaining = deck.CardCount;
        if (remaining > 0)
        {
            deck.DrawAndDistribute(remaining);
            yield return null;
            while (deck.IsDrawing)
                yield return null;
        }

        Debug.Log($"[TianYunZhongChang] 抽取剩余 {remaining} 张牌");

        // 抽牌完成后重新激活手牌交互（速攻窗口可能仍开着，抽来的新牌需要能拖动）
        gameManager.ReactivateHandIfWindowOpen();

        // ── 4. 抽牌完成：停止彩虹，边框发光淡出 ───────
        Debug.Log("[TianYunZhongChang] 步骤4：停止彩虹+发光淡出");
        fx?.StopDeckRainbow();
        if (glowFx != null)
            yield return refs.StartCoroutine(glowFx.StopBreathing());

        // ── 5. 注册额外战斗阶段 ───────────────────────
        Debug.Log("[TianYunZhongChang] 步骤5：注册额外战斗");
        gameManager.RequestExtraBattlePhase(
            onComplete: () =>
            {
                hand?.SetIgnoreHandLimit(false);
                hand?.ClearAllCards();
                Debug.Log("[TianYunZhongChang] 额外战斗阶段结束，清空手牌");
            },
            onStart: glowFx != null
                ? () => glowFx.PlayGoldPulse()
                : (System.Func<System.Collections.IEnumerator>)null
        );

        Debug.Log("[TianYunZhongChang] 完成");
    }
}