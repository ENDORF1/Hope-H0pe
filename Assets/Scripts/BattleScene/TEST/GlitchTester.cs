using UnityEngine;

/// <summary>
/// 测试用。按键手动触发各动作。
///
/// ── 熄忘阵营 (Void) ──────────────────────────────
/// 1 = 块撕裂 (blockTear)
/// 2 = RGB分离 (rgbSplit)
/// 3 = 错误弹窗 (errorBars)
/// 4 = 白色噪点 (noise)
/// 5 = 闪红噪点 (flashRed)
/// 6 = 边缘侵入 (edgeBleed)
/// 7 = 画面失步 (screenRoll)
/// 8 = 熄忘幽灵 (voidGhost)
/// 9 = 熄忘裂缝 (voidCrack)
///
/// ── 希望阵营 (Hope) ──────────────────────────────
/// 小键盘1 = 水滴落下+水波 (hopeDrops)
/// 小键盘2 = 水珠破裂 (hopeBurst)
/// 小键盘3 = 水滴落下 (hopeDrops)
/// 小键盘4 = 波浪线入侵 (hopeWaves)
/// 小键盘5 = 小点爆发 (hopeParticles)
/// 小键盘6 = 负片闪现 (hopeNegative)
/// 小键盘7 = 希望幽灵 (hopeGhost)
/// 小键盘7 = 希望幽灵 (hopeGhost)
/// 小键盘8 = 希望弹窗 (hopePopup)
/// 小键盘0 = 希望扫掠 (hopeSweep)
/// 小键盘Enter = LED点阵 (hopeLED)
/// 小键盘- = 闪蓝噪点 (flashBlue)
///
/// ── 全部 ─────────────────────────────────────────
/// G = 触发当前阵营全部特效
/// </summary>
public class GlitchTester : MonoBehaviour
{
    [SerializeField] private ScreenGlitchUI glitchUI;

    void Update()
    {
        // ── 熄忘阵营 ──────────────────────────────────
        if (Input.GetKeyDown(KeyCode.Alpha1)) glitchUI?.TriggerAction("blockTear");
        if (Input.GetKeyDown(KeyCode.Alpha2)) glitchUI?.TriggerAction("rgbSplit");
        if (Input.GetKeyDown(KeyCode.Alpha3)) glitchUI?.TriggerAction("errorBars");
        if (Input.GetKeyDown(KeyCode.Alpha4)) glitchUI?.TriggerAction("noise");
        if (Input.GetKeyDown(KeyCode.Alpha5)) glitchUI?.TriggerAction("flashRed");
        if (Input.GetKeyDown(KeyCode.Alpha6)) glitchUI?.TriggerAction("edgeBleed");
        if (Input.GetKeyDown(KeyCode.Alpha7)) glitchUI?.TriggerAction("screenRoll");
        if (Input.GetKeyDown(KeyCode.Alpha8)) glitchUI?.TriggerAction("voidGhost");
        if (Input.GetKeyDown(KeyCode.Alpha9)) glitchUI?.TriggerAction("voidCrack");

        // ── 希望阵营 ──────────────────────────────────
        if (Input.GetKeyDown(KeyCode.Keypad1)) glitchUI?.TriggerAction("hopeDrops");
        if (Input.GetKeyDown(KeyCode.Keypad2)) glitchUI?.TriggerAction("hopeBurst");
        if (Input.GetKeyDown(KeyCode.Keypad3)) glitchUI?.TriggerAction("hopeDrops");
        if (Input.GetKeyDown(KeyCode.Keypad4)) glitchUI?.TriggerAction("hopeSin");
        if (Input.GetKeyDown(KeyCode.Keypad5)) glitchUI?.TriggerAction("hopeParticles");
        if (Input.GetKeyDown(KeyCode.Keypad6)) glitchUI?.TriggerAction("hopeNegative");
        if (Input.GetKeyDown(KeyCode.Keypad7)) glitchUI?.TriggerAction("hopeGhost");
        if (Input.GetKeyDown(KeyCode.Keypad7)) glitchUI?.TriggerAction("hopeGhost");
        if (Input.GetKeyDown(KeyCode.Keypad8)) glitchUI?.TriggerAction("hopePopup");
        if (Input.GetKeyDown(KeyCode.Keypad9)) glitchUI?.TriggerAction("hopeBubble");
        if (Input.GetKeyDown(KeyCode.Keypad0))     glitchUI?.TriggerAction("hopeSweep");
        if (Input.GetKeyDown(KeyCode.KeypadEnter)) glitchUI?.TriggerAction("hopeLED");
        if (Input.GetKeyDown(KeyCode.KeypadMinus)) glitchUI?.TriggerAction("flashBlue");

        // ── 全部 ──────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.G)) glitchUI?.TriggerAll();
    }
}