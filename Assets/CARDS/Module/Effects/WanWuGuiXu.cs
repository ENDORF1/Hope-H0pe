using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 万物归墟 · 姐姐·熄忘 · 绝望
/// 模块牌自定义效果脚本，通过 CardAsset.ModuleScriptName = "WanWuGuiXu" 在运行时动态挂载。
/// 不挂在 Prefab 上，DeckManager 生成卡牌实例时自动 AddComponent。
/// 场景引用通过 SceneRefs 单例自动获取。
/// </summary>
public class WanWuGuiXu : MonoBehaviour
{
    public bool BlocksHealing => true;
    public bool BlocksDrones  => true;

    private ModuleInstance _module;
    private bool _effectFired            = false;
    private bool _sideEffectRegistered   = false;
    private bool _refsResolved           = false;

    // 缓存场景引用
    private GameManager  _gameManager;
    private DeckManager  _ownerDeckManager;
    private HandManager  _enemyHandManager;

    void Awake()
    {
        _module = GetComponent<ModuleInstance>();
    }

    void Start()
    {
        if (_module == null) return;
        _module.OnFaceStateChanged += OnFaceStateChanged;
        _module.OnDestroyed        += OnDestroyed;
    }

    void OnDestroy()
    {
        if (_module != null)
        {
            _module.OnFaceStateChanged -= OnFaceStateChanged;
            _module.OnDestroyed        -= OnDestroyed;
        }
        if (_sideEffectRegistered && _gameManager != null)
            _gameManager.OnTurnEnd -= OnTurnEnd;
    }

    /// <summary>
    /// 在翻面时才解析场景引用，确保此时 ModuleInstance.Initialize 已执行完毕，Owner 已赋值。
    /// </summary>
    private void ResolveRefs()
    {
        if (_refsResolved) return;

        var refs = SceneRefs.Instance;
        if (refs == null)
        {
            Debug.LogError("[WanWuGuiXu] SceneRefs 未找到！");
            return;
        }

        _gameManager = refs.GameManager;

        bool isPlayer = (_module.Owner == refs.PlayerState);
        Debug.Log($"[WanWuGuiXu] ResolveRefs: isPlayer={isPlayer}, Owner={_module.Owner?.name}, PlayerState={refs.PlayerState?.name}");

        _ownerDeckManager = isPlayer ? refs.PlayerDeckManager : refs.AiDeckManager;
        _enemyHandManager = isPlayer ? refs.AiHandManager     : refs.PlayerHandManager;

        _refsResolved = true;
    }

    private void OnFaceStateChanged(bool isFaceDown)
    {
        if (isFaceDown || _effectFired) return;
        _effectFired = true;
        ResolveRefs();
        StartCoroutine(OnFlipEffect());
    }

    private IEnumerator OnFlipEffect()
    {
        // 等待翻面动画结束
        var rot = GetComponentInChildren<BetterCardRotation>(true);
        if (rot != null)
            yield return new WaitUntil(() => !rot.IsFlipping);
        else
            yield return new WaitForEndOfFrame();

        // 缓存 OCM，供黑屏回调内刷新 UI 使用
        var ocm = _module.GetComponent<OneCardManager>()
               ?? _module.GetComponentInChildren<OneCardManager>(true);

        // 提前收集数据，避免黑屏回调时对象已被销毁
        DeployZone enemyZone = _gameManager?.GetEnemyDeployZone(_module.Owner);
        List<ModuleInstance> enemyModules = enemyZone != null
            ? CollectAliveModules(enemyZone)
            : new List<ModuleInstance>();
        int handCount = _enemyHandManager?.GetHandCount() ?? 0;

        // ── 棺椁特效序列 ─────────────────────────────
        // onShowMessage : 翻开后立即弹出台词
        // onBlackout    : 黑屏切入瞬间摧毁模块和手牌
        var coffinFx = CoffinEffect.Instance;
        if (coffinFx != null)
        {
            yield return StartCoroutine(coffinFx.PlaySequence(

                onShowMessage: () =>
                {
                    if (_gameManager?.MessageManager != null)
                        StartCoroutine(_gameManager.MessageManager.ShowAndWait(
                            "<shake><color=red>予汝熄忘...</color></shake>"));
                },

                onBlackout: () =>
                {
                    // ── 效果1：摧毁敌方所有模块 ──────────────────
                    foreach (var m in enemyModules)
                    {
                        m.SilentDestroy();
                        if (_module.IsAlive)
                        {
                            _module.SetMaxHealth(_module.MaxHealth + 3, adjustCurrent: false);
                            _module.AddCurrentHealth(3);
                        }
                    }
                    ocm?.ChangeStats(_module.Attack, _module.CurrentHealth);
                    Debug.Log($"[WanWuGuiXu] 摧毁敌方模块 {enemyModules.Count} 个，血量上限 +{enemyModules.Count * 3}");

                    // ── 效果2：清空敌方手牌 ───────────────────────
                    if (_enemyHandManager != null)
                    {
                        _enemyHandManager.ClearAllCards();
                        if (_module.IsAlive)
                            _module.SetAttack(_module.Attack + handCount * 2);
                        ocm?.ChangeStats(_module.Attack, _module.CurrentHealth);
                        Debug.Log($"[WanWuGuiXu] 清空敌方手牌 {handCount} 张，攻击力 +{handCount * 2}");
                    }
                }
            ));
        }
        else
        {
            // 无特效时退化：直接弹消息再结算
            if (_gameManager?.MessageManager != null)
                yield return StartCoroutine(_gameManager.MessageManager.ShowAndWait(
                    "<shake><color=red>予汝熄忘...</color></shake>"));

            foreach (var m in enemyModules)
            {
                m.SilentDestroy();
                if (_module.IsAlive)
                {
                    _module.SetMaxHealth(_module.MaxHealth + 3, adjustCurrent: false);
                    _module.AddCurrentHealth(3);
                }
            }
            ocm?.ChangeStats(_module.Attack, _module.CurrentHealth);

            if (_enemyHandManager != null)
            {
                _enemyHandManager.ClearAllCards();
                if (_module.IsAlive)
                    _module.SetAttack(_module.Attack + handCount * 2);
                ocm?.ChangeStats(_module.Attack, _module.CurrentHealth);
            }
        }

        // ── 注册副作用（特效播完后） ──────────────────
        if (!_sideEffectRegistered && _gameManager != null)
        {
            _gameManager.OnTurnEnd += OnTurnEnd;
            _sideEffectRegistered   = true;
        }
    }

    private void OnTurnEnd()
    {
        if (!_module.IsAlive) return;
        _ownerDeckManager?.DestroyTopCards(10);
        Debug.Log("[WanWuGuiXu] 副作用：摧毁牌库顶10张");
    }

    private void OnDestroyed(ModuleInstance m)
    {
        Debug.Log("[WanWuGuiXu] 模块被摧毁，触发特殊败北");
        _gameManager?.TriggerSpecialDefeat("熄忘不再...");
    }

    private List<ModuleInstance> CollectAliveModules(DeployZone zone)
    {
        var list = new List<ModuleInstance>();
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            var slotObj = zone.GetSlot(i);
            if (slotObj == null) continue;
            var slot = slotObj.GetComponent<DeploySlot>();
            var mi   = slot?.OccupyingModuleInstance;
            if (mi != null && mi.IsAlive) list.Add(mi);
        }
        return list;
    }
}