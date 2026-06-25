using UnityEngine;
using TMPro;
using TMPEffects.Components;

public class SubTitleWave : MonoBehaviour
{
    [SerializeField] private string waveTag = "<wave amp=2 upperiod=2 downperiod=2 uni=1>";

    private TMP_Text  _tmp;
    private TMPWriter _writer;
    private string    _plainText;

    void Awake()
    {
        _tmp    = GetComponent<TMP_Text>();
        _writer = GetComponent<TMPWriter>();
        if (_writer != null) _writer.enabled = false;
    }

    void Start()
    {
        // 保存纯文本，去掉 Editor 里可能存在的标签
        _plainText = System.Text.RegularExpressions.Regex.Replace(_tmp.text, @"<[^>]+>", "").Trim();
    }

    public void StartStandby()
    {
        // 先注入 wave 标签 → TMPAnimator 侦测文字变化，开始波动
        _tmp.text = $"{waveTag}{_plainText}</wave>";

        if (_writer != null)
        {
            _writer.enabled = true;
            _writer.ResetWriter();
            _writer.StartWriter();
        }
    }
}
