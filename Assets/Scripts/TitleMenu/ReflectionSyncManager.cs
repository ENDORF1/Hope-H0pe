using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 挂在 ReflCanvas 上。
/// 每帧把 MainCanvas 里的 TMP 文字内容、颜色、激活状态同步到 ReflCanvas 的副本。
///
/// 同步内容：
///   - text 内容
///   - color（含 alpha）
///   - gameObject.activeSelf
///   - anchoredPosition（支持按钮动画等位移同步）
///
/// 不同步：
///   - 按钮交互（ReflCanvas 里的按钮组件已在 Setup 时移除）
///   - 独立的倒影覆盖文字（通过 ReflectionOverrideManager 管理）
/// </summary>
public class ReflectionSyncManager : MonoBehaviour
{
    [SerializeField] private List<RectTransform> _sources = new List<RectTransform>();
    [SerializeField] private List<RectTransform> _targets = new List<RectTransform>();

    // 运行时缓存
    private List<TextMeshProUGUI> _srcTmps = new List<TextMeshProUGUI>();
    private List<TextMeshProUGUI> _dstTmps = new List<TextMeshProUGUI>();
    private List<Image>           _srcImgs = new List<Image>();
    private List<Image>           _dstImgs = new List<Image>();

    void Start()
    {
        int count = Mathf.Min(_sources.Count, _targets.Count);
        for (int i = 0; i < count; i++)
        {
            _srcTmps.Add(_sources[i]?.GetComponent<TextMeshProUGUI>());
            _dstTmps.Add(_targets[i]?.GetComponent<TextMeshProUGUI>());
            _srcImgs.Add(_sources[i]?.GetComponent<Image>());
            _dstImgs.Add(_targets[i]?.GetComponent<Image>());
        }
    }

    void LateUpdate()
    {
        int count = Mathf.Min(_sources.Count, _targets.Count);
        for (int i = 0; i < count; i++)
        {
            var src = _sources[i];
            var dst = _targets[i];
            if (src == null || dst == null) continue;

            // 激活状态同步
            bool active = src.gameObject.activeInHierarchy;
            if (dst.gameObject.activeSelf != active)
                dst.gameObject.SetActive(active);

            if (!active) continue;

            // TMP 同步
            var srcTmp = _srcTmps[i];
            var dstTmp = _dstTmps[i];
            if (srcTmp != null && dstTmp != null)
            {
                if (dstTmp.text  != srcTmp.text)  dstTmp.text  = srcTmp.text;
                if (dstTmp.color != srcTmp.color)  dstTmp.color = srcTmp.color;
            }

            // Image 同步
            var srcImg = _srcImgs[i];
            var dstImg = _dstImgs[i];
            if (srcImg != null && dstImg != null)
            {
                if (dstImg.color != srcImg.color) dstImg.color = srcImg.color;
                if (dstImg.sprite != srcImg.sprite) dstImg.sprite = srcImg.sprite;
            }

            // 位置同步（支持动画）
            dst.anchoredPosition = src.anchoredPosition;
            dst.sizeDelta        = src.sizeDelta;
            dst.localScale       = src.localScale;
            dst.localEulerAngles = src.localEulerAngles;
        }
    }

    // ── 公开 API：供 ReflectionOverrideManager 覆盖倒影文字 ──

    /// <summary>按索引获取倒影 TMP（用于外部覆盖）</summary>
    public TextMeshProUGUI GetTargetTmp(int index)
    {
        if (index < 0 || index >= _dstTmps.Count) return null;
        return _dstTmps[index];
    }

    /// <summary>按索引暂停同步（覆盖模式）</summary>
    public void SetOverride(int index, string text)
    {
        var dst = GetTargetTmp(index);
        if (dst == null) return;
        dst.text = text;
        // 标记为覆盖：把源设为 null 让 LateUpdate 跳过
        if (index < _srcTmps.Count) _srcTmps[index] = null;
    }

    /// <summary>按索引恢复同步</summary>
    public void ClearOverride(int index)
    {
        if (index < 0 || index >= _sources.Count) return;
        _srcTmps[index] = _sources[index]?.GetComponent<TextMeshProUGUI>();
    }
}
