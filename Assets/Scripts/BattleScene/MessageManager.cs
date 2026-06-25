using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

public class MessageManager : MonoBehaviour
{
    [Header("游戏对象")]
    public Image leftTriangle;      // 左三角形 Image
    public Image rightTriangle;     // 右三角形 Image
    public TMP_Text messageText;    // TextMeshPro 文本
    
    [Header("动画设置")]
    public float flyInDuration = 0.5f;        // 飞入时间
    public float displayDuration = 1.5f;      // 显示时间
    public float flyOutDuration = 0.5f;       // 飞出时间
    
    [Header("三角形拼合位置")]
    public Vector3 leftTargetPosition = new Vector3(-2f, 0f, 0f);   // 左三角形目标位置
    public Vector3 rightTargetPosition = new Vector3(2f, 0f, 0f);   // 右三角形目标位置
    
    [Header("三角形大小")]
    public float triangleScale = 2f;           // 三角形缩放倍数
    
    [Header("飞行距离")]
    public float flyDistanceMultiplier = 10f;   // 飞行距离倍数
    
    private Sequence currentSequence;
    private Vector3 leftStartPos;
    private Vector3 rightStartPos;
    private Vector3 leftExitPos;
    private Vector3 rightExitPos;
    
    void Awake()
    {
        CalculateScreenPositions();
        SetupTriangleScale();
        
        // 确保文本初始是隐藏的
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
        
        // 确保三角形初始是隐藏的
        if (leftTriangle != null)
            leftTriangle.gameObject.SetActive(false);
        
        if (rightTriangle != null)
            rightTriangle.gameObject.SetActive(false);
    }
    
    void CalculateScreenPositions()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        
        Vector3 bottomLeft = cam.ScreenToWorldPoint(new Vector3(0, 0, 10));
        Vector3 topLeft = cam.ScreenToWorldPoint(new Vector3(0, Screen.height, 10));
        Vector3 bottomRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, 0, 10));
        Vector3 topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 10));
        
        float offset = 20f * flyDistanceMultiplier;
        
        leftStartPos = new Vector3(bottomLeft.x - offset, bottomLeft.y - offset, 0);
        leftExitPos = new Vector3(topLeft.x - offset, topLeft.y + offset, 0);
        
        rightStartPos = new Vector3(topRight.x + offset, topRight.y + offset, 0);
        rightExitPos = new Vector3(bottomRight.x + offset, bottomRight.y - offset, 0);
    }
    
    void SetupTriangleScale()
    {
        if (leftTriangle != null)
            leftTriangle.transform.localScale = Vector3.one * triangleScale;
        
        if (rightTriangle != null)
            rightTriangle.transform.localScale = Vector3.one * triangleScale;
    }
    
    public void ShowMessage(string message)
    {
        if (currentSequence != null && currentSequence.IsActive())
            currentSequence.Kill();
        
        // 设置文本内容，但保持隐藏
        if (messageText != null)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(false);  // 再次确保隐藏
        }
        
        currentSequence = DOTween.Sequence();
        
        // 准备三角形
        if (leftTriangle != null)
        {
            leftTriangle.gameObject.SetActive(true);
            leftTriangle.transform.position = leftStartPos;
        }
        
        if (rightTriangle != null)
        {
            rightTriangle.gameObject.SetActive(true);
            rightTriangle.transform.position = rightStartPos;
        }
        
        // 三角形飞入
        if (leftTriangle != null)
            currentSequence.Join(leftTriangle.transform.DOMove(leftTargetPosition, flyInDuration).SetEase(Ease.OutBack));
        
        if (rightTriangle != null)
            currentSequence.Join(rightTriangle.transform.DOMove(rightTargetPosition, flyInDuration).SetEase(Ease.OutBack));
        
        // 三角形飞入完成后，显示文本对象
        currentSequence.AppendCallback(() => {
            if (messageText != null)
            {
                messageText.gameObject.SetActive(true);
                messageText.transform.position = (leftTargetPosition + rightTargetPosition) / 2f;
            }
        });
        
        // 等待显示时间
        currentSequence.AppendInterval(displayDuration);
        
        // 隐藏文本对象
        if (messageText != null)
            currentSequence.AppendCallback(() => messageText.gameObject.SetActive(false));
        
        // 三角形飞出
        if (leftTriangle != null)
            currentSequence.Join(leftTriangle.transform.DOMove(leftExitPos, flyOutDuration).SetEase(Ease.InBack));
        
        if (rightTriangle != null)
            currentSequence.Join(rightTriangle.transform.DOMove(rightExitPos, flyOutDuration).SetEase(Ease.InBack));
        
        // 隐藏三角形
        currentSequence.AppendCallback(HideAll);
        
        currentSequence.Play();
    }
    
    void HideAll()
    {
        if (leftTriangle != null) leftTriangle.gameObject.SetActive(false);
        if (rightTriangle != null) rightTriangle.gameObject.SetActive(false);
        // 文本已经在前面隐藏了，这里不需要重复隐藏
    }
    
    /// <summary>播放消息动画并等待完成</summary>
    public IEnumerator ShowAndWait(string message)
    {
        ShowMessage(message);
        yield return new WaitUntil(() => currentSequence == null || !currentSequence.IsActive());
    }

    /// <summary>天允终偿专用：「赐你希望！」金色 palette + 放大弹出效果</summary>
    public IEnumerator ShowHopeAndWait()
    {
        // 文字放大弹出：先缩小到0，弹出到1.3，回弹到1
        yield return ShowAndWait("<palette><wave amplitude=4 frequency=0.8>赐你希望！</wave></palette>");
    }

    public IEnumerator ShowGameStartAndWait()            => ShowAndWait("<wave><palette>Game Start</palette></wave>");
    public IEnumerator ShowTurnStartAndWait(int turnNum) => ShowAndWait($"<wave><palette>Turn {turnNum}</palette></wave>");
    public IEnumerator ShowBattlePhaseAndWait()          => ShowAndWait("<wave><palette>Battle Phase</palette></wave>");
    public IEnumerator ShowMissilePhaseAndWait()         => ShowAndWait("<wave><palette>Missile Phase</palette></wave>");
    public IEnumerator ShowTurnEndAndWait()              => ShowAndWait("<wave><palette>Turn End</palette></wave>");
    public IEnumerator ShowVictoryAndWait()              => ShowAndWait("<wave><palette>Victory</palette></wave>");
    public IEnumerator ShowDefeatAndWait()               => ShowAndWait("<wave><palette>Defeat</palette></wave>");
    public IEnumerator ShowDrawAndWait()                 => ShowAndWait("<wave><palette>Draw</palette></wave>");
    public void ShowOpponentTurn() => ShowMessage("<wave><palette>Opponent Turn</palette></wave>");
    public void ShowVictory()      => ShowMessage("<wave><palette>Victory</palette></wave>");
    public void ShowDefeat()       => ShowMessage("<wave><palette>Defeat</palette></wave>");
    public void ShowDraw()         => ShowMessage("<wave><palette>Draw</palette></wave>");

    // ── GameManager 阶段方法 ──────────────────────────
    public void ShowGameStart()              => ShowMessage("<wave><palette>Game Start</palette></wave>");
    public void ShowTurnStart(int turnNum)   => ShowMessage($"<wave><palette>Turn {turnNum}</palette></wave>");
    public void ShowBattlePhase()            => ShowMessage("<wave><palette>Battle Phase</palette></wave>");
    public void ShowMissilePhase()           => ShowMessage("<wave><palette>Missile Phase</palette></wave>");
    public void ShowTurnEnd()                => ShowMessage("<wave><palette>Turn End</palette></wave>");
}