using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeywordTooltipPanel : MonoBehaviour
{
    [Header("数据库")]
    public KeywordTooltipDatabase database;

    [Header("布局")]
    public GameObject entryPrefab;
    public RectTransform entryContainer;

    [Header("每行字数（0 = 不限制）")]
    public int charsPerLine = 20;

    [Header("定位")]
    public Vector2 bottomRightOffset = new Vector2(20f, 20f);

    private RectTransform _rect;

    // 缓存当前显示的解释文本 TMP 及其原始模板，用于 Update 刷新占位符
    private readonly List<(TextMeshProUGUI tmp, string rawExplanation)> _activeExplanations
        = new List<(TextMeshProUGUI, string)>();
    private ModuleInstance _currentModule;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();

        _rect.anchorMin        = new Vector2(1, 0);
        _rect.anchorMax        = new Vector2(1, 0);
        _rect.pivot            = new Vector2(1, 0);
        _rect.anchoredPosition = new Vector2(-bottomRightOffset.x, bottomRightOffset.y);

        HoverPreview.OnAnyPreviewStarted += HandlePreviewStarted;
        HoverPreview.OnAnyPreviewStopped += HandlePreviewStopped;

        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        HoverPreview.OnAnyPreviewStarted -= HandlePreviewStarted;
        HoverPreview.OnAnyPreviewStopped -= HandlePreviewStopped;
    }

    void Update()
    {
        if (_activeExplanations.Count == 0 || _currentModule == null) return;

        foreach (var (tmp, raw) in _activeExplanations)
        {
            if (tmp == null) continue;
            tmp.text = WrapText(ReplacePlaceholders(raw, _currentModule), charsPerLine);
        }
    }

    private void HandlePreviewStarted(HoverPreview hp)
    {
        if (database == null || entryPrefab == null || entryContainer == null) return;

        string description = ResolveDescription(hp);
        if (string.IsNullOrEmpty(description)) { gameObject.SetActive(false); return; }

        List<KeywordTooltipEntry> matched = database.FindMatches(description);
        if (matched == null || matched.Count == 0) { gameObject.SetActive(false); return; }

        _currentModule = hp.GetComponent<ModuleInstance>();
        PopulateEntries(matched, _currentModule);
        _rect.anchoredPosition = new Vector2(-bottomRightOffset.x, bottomRightOffset.y);
        gameObject.SetActive(true);
    }

    private void HandlePreviewStopped()
    {
        gameObject.SetActive(false);
        ClearEntries();
        _currentModule = null;
    }

    /// <summary>
    /// 收集 hp 自身及 previewGameObject 下所有激活的 TMP 文本拼接起来，
    /// 用于关键词匹配。这样只要 UI 上显示了某个关键词，就能自然匹配到，
    /// 无需和具体模块类型绑定。
    /// </summary>
    private string ResolveDescription(HoverPreview hp)
    {
        var sb = new StringBuilder();

        GameObject[] roots = hp.previewGameObject != null && hp.previewGameObject != hp.gameObject
            ? new[] { hp.gameObject, hp.previewGameObject }
            : new[] { hp.gameObject };

        foreach (GameObject root in roots)
        {
            TextMeshProUGUI[] tmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                if (t.gameObject.activeInHierarchy && !string.IsNullOrEmpty(t.text))
                    sb.Append(t.text).Append(' ');
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private void PopulateEntries(List<KeywordTooltipEntry> entries, ModuleInstance module = null)
    {
        ClearEntries();

        // 生成一个 Entry 作为容器，背景图由它提供
        GameObject entryGo = Instantiate(entryPrefab, entryContainer);

        // 从 prefab 里取出两个 TMP 作为模板
        TextMeshProUGUI[] templateTmps = entryGo.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (templateTmps.Length < 2) return;

        TextMeshProUGUI keywordTemplate     = templateTmps[0];
        TextMeshProUGUI explanationTemplate = templateTmps[1];

        // 隐藏模板本身，只用它们的样式来克隆
        keywordTemplate.gameObject.SetActive(false);
        explanationTemplate.gameObject.SetActive(false);

        // 为每个关键词克隆一对 TMP，挂在 Entry 下
        foreach (var entry in entries)
        {
            GameObject kwGo  = Instantiate(keywordTemplate.gameObject,     entryGo.transform);
            GameObject expGo = Instantiate(explanationTemplate.gameObject, entryGo.transform);

            kwGo.SetActive(true);
            expGo.SetActive(true);

            kwGo.GetComponent<TextMeshProUGUI>().text = entry.Keyword;

            TextMeshProUGUI expTmp = expGo.GetComponent<TextMeshProUGUI>();
            expTmp.text = WrapText(ReplacePlaceholders(entry.Explanation, module), charsPerLine);

            // 缓存供 Update 刷新
            _activeExplanations.Add((expTmp, entry.Explanation));
        }
    }

    /// <summary>将解释文本中的占位符替换为模块运行时数值</summary>
    private string ReplacePlaceholders(string text, ModuleInstance module)
    {
        if (string.IsNullOrEmpty(text)) return text;

        int cooldown = (module != null) ? Mathf.Max(0, module.CooldownRemaining - 1) : 0;
        text = text.Replace("{cooldown}", cooldown.ToString());

        return text;
    }

    private string WrapText(string text, int maxChars)
    {
        if (maxChars <= 0 || string.IsNullOrEmpty(text)) return text;

        var sb = new StringBuilder();
        int count = 0;
        foreach (char c in text)
        {
            if (c == '\n') { sb.Append(c); count = 0; continue; }
            sb.Append(c);
            count++;
            if (count >= maxChars) { sb.AppendLine(); count = 0; }
        }
        return sb.ToString();
    }

    private void ClearEntries()
    {
        _activeExplanations.Clear();
        if (entryContainer == null) return;
        for (int i = entryContainer.childCount - 1; i >= 0; i--)
            Destroy(entryContainer.GetChild(i).gameObject);
    }
}