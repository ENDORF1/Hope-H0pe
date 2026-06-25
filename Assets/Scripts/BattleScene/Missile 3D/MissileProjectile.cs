using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 挂在导弹预制体上。
/// 由 CombatEngine.FireMissile 调用 Launch()，飞向目标后销毁自身。
/// </summary>
public class MissileProjectile : MonoBehaviour
{
    [Header("飞行参数")]
    public float flightDuration = 0.4f;   // 飞行总时长
    public float arcHeight      = 0.5f;   // 弧线高度（世界单位）

    [Header("朝向补偿")]
    [Tooltip("如弹体朝向还有偏差，在此微调。通常不需要修改。")]
    public Vector3 rotationOffset = new Vector3(0f, 0f, 0f);

    [Header("命中特效")]
    [Tooltip("命中时生成的粒子预制体（品红色爆炸），留空则跳过")]
    public GameObject hitVFXPrefab;
    public float hitVFXDuration = 1.0f;   // 特效存活时长

    // ─────────────────────────────────────────────────

    /// <summary>
    /// 从 startPos 飞向 targetPos，命中后执行 onHit 回调再销毁自身。
    /// arcDirection：弧线凸出方向（传入 missile.transform.forward，朝屏幕外）
    /// onHit：落地时执行的回调（伤害结算、死亡判断、溅射、伤害数字显示等）
    /// 由 CombatEngine 调用。
    /// </summary>
    public IEnumerator Launch(Vector3 startPos, Vector3 targetPos, Vector3 arcDirection,
                              Action<Vector3> onHit = null)
    {
        // 强制目标Z对齐起点Z，防止弹体穿入画布背面
        targetPos.z = startPos.z;

        transform.position = startPos;

        Quaternion offsetRot = Quaternion.Euler(rotationOffset);

        // 启动尾迹
        MissileTrail trail = GetComponent<MissileTrail>();
        if (trail != null) trail.StartTrail();

        float elapsed = 0f;
        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);

            // 线性插值 + 抛物线弧（朝屏幕外凸出）
            Vector3 linear = Vector3.Lerp(startPos, targetPos, t);
            float   arc    = arcHeight * 4f * t * (1f - t);
            transform.position = linear + arcDirection * arc;

            // 朝向飞行方向：用Y轴对齐飞行方向（模型头部是Y轴）
            if (elapsed + Time.deltaTime < flightDuration)
            {
                Vector3 nextLinear = Vector3.Lerp(startPos, targetPos, t + 0.05f);
                float   nextArc    = arcHeight * 4f * (t + 0.05f) * (1f - (t + 0.05f));
                Vector3 nextPos    = nextLinear + arcDirection * nextArc;
                Vector3 dir        = nextPos - transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.FromToRotation(Vector3.up, dir) * offsetRot;
            }

            yield return null;
        }

        // 命中
        if (trail != null) trail.StopTrail();
        transform.position = targetPos;

        // 执行命中回调（伤害结算、伤害数字等）
        onHit?.Invoke(targetPos);

        // 生成命中特效
        if (hitVFXPrefab != null)
        {
            GameObject vfx = Instantiate(hitVFXPrefab, targetPos, Quaternion.identity);
            Destroy(vfx, hitVFXDuration);
        }

        Destroy(gameObject);
    }
}