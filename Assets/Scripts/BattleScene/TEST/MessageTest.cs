using UnityEngine;
using TMPro;

public class MessageManagerTester : MonoBehaviour
{
    private MessageManager messageManager;
    
    [Header("测试设置")]
    public KeyCode testKey = KeyCode.Space;
    public TMP_Text messageText;  // 传入 TextPro 组件（仅用于读取文本内容）
    
    void Start()
    {
        messageManager = GetComponent<MessageManager>();
        
        if (messageManager == null)
        {
            Debug.LogError("找不到 MessageManager 组件！");
        }
        
        if (messageText == null)
        {
            Debug.LogError("没有传入 TextPro 组件！");
        }
    }
    
    void Update()
    {
        // 按下空格键触发消息
        if (Input.GetKeyDown(testKey) && messageManager != null && messageText != null)
        {
            // 只传递文本内容，不传递组件本身
            messageManager.ShowMessage(messageText.text);
            Debug.Log($"触发消息: {messageText.text}");
        }
    }
}