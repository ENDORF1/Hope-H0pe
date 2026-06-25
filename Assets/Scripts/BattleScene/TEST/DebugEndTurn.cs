using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 调试用：按下指定键等同于点击回合结束按钮
/// 挂在任意常驻对象上，把 End Window Button 拖进来
/// </summary>
public class DebugEndTurn : MonoBehaviour
{
    [Tooltip("触发回合结束的按键")]
    public KeyCode endTurnKey = KeyCode.Space;

    [Tooltip("拖入场景中的回合结束按钮")]
    public Button endWindowButton;

    void Update()
    {
        if (Input.GetKeyDown(endTurnKey) && endWindowButton != null && endWindowButton.gameObject.activeInHierarchy)
            endWindowButton.onClick.Invoke();
    }
}
