using UnityEngine;

/// <summary>
/// 专门负责注册 DamageEffect Prefab 的常驻脚本。
/// 挂在场景任意常驻对象上，把 Prefab 拖进来。
/// DamageEffect Prefab 本身【不挂】本脚本，也【不挂】DamageEffect 脚本作为注册器。
/// </summary>
public class DamageEffectSpawner : MonoBehaviour
{
    [SerializeField] private GameObject damageEffectPrefab;

    // 所有人通过这里取 Prefab
    public static GameObject Prefab { get; private set; }

    void Awake()
    {
        if (damageEffectPrefab != null)
            Prefab = damageEffectPrefab;
        else
            Debug.LogWarning("[DamageEffectSpawner] damageEffectPrefab 未赋值！");
    }
}