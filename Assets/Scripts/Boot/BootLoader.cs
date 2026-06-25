using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Boot 场景唯一脚本。
///
/// 职责：
///   1. 游戏启动时立刻开始后台预加载所有需要的场景
///   2. 把 AsyncOperation 存入静态字典，供转场脚本直接取用
///   3. 预加载启动后立刻跳转到 TitleScene，不等加载完成
///
/// 其他脚本通过 BootLoader.GetPreloadOp("场景名") 获取对应的 AsyncOperation。
/// 如果场景还没加载完，op.progress < 0.9f，调用方自行等待。
/// </summary>
public class BootLoader : MonoBehaviour
{
    // 静态字典：场景名 → AsyncOperation
    // 供 HopeTransition 等转场脚本直接取用
    private static readonly Dictionary<string, AsyncOperation> _preloadOps
        = new Dictionary<string, AsyncOperation>();

    [Header("需要预加载的场景名列表")]
    [SerializeField] private string[] scenesToPreload = new string[]
    {
        "Hope_CharacterSelect",
        // 之后加 Void_CharacterSelect 等场景直接在这里添加
    };

    [Header("跳转目标")]
    [SerializeField] private string titleSceneName = "Title Scene";

    // ─────────────────────────────────────────────────

    void Start()
    {
        StartCoroutine(BootRoutine());
    }

    private IEnumerator BootRoutine()
    {
        // 1. 先完整加载 TitleScene
        var titleOp = SceneManager.LoadSceneAsync(titleSceneName, LoadSceneMode.Additive);
        yield return titleOp;

        // 2. TitleScene 就绪，设为 Active，卸载 Boot
        Scene titleScene = SceneManager.GetSceneByName(titleSceneName);
        if (titleScene.IsValid())
            SceneManager.SetActiveScene(titleScene);

        SceneManager.UnloadSceneAsync(gameObject.scene);

        // 3. TitleScene 完成后再启动预加载，此时不会阻塞任何东西
        //    allowSceneActivation = false：卡在90%，防止场景激活后抢占画面
        //    HopeTransition 取用时手动 allowSceneActivation = true
        foreach (var sceneName in scenesToPreload)
        {
            if (!_preloadOps.ContainsKey(sceneName))
            {
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                // allowSceneActivation 保持默认 true：场景激活后 Canvas 靠 positionOffset 定位在左边，alpha=0 不可见
                _preloadOps[sceneName] = op;
                Debug.Log($"[Boot] 开始预加载：{sceneName}");
            }
        }
    }

    // ─────────────────────────────────────────────────
    // 公共接口：供转场脚本取用预加载的 AsyncOperation
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 获取指定场景的预加载 AsyncOperation。
    /// 如果返回 null，说明 Boot 还没有预加载该场景（不应该发生）。
    /// 调用方通过 op.progress >= 0.9f 判断是否加载完毕。
    /// </summary>
    public static AsyncOperation GetPreloadOp(string sceneName)
    {
        _preloadOps.TryGetValue(sceneName, out var op);
        return op;
    }

    /// <summary>
    /// 场景是否已预加载完毕并激活（在内存中存在）。
    /// </summary>
    public static bool IsPreloaded(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }
}