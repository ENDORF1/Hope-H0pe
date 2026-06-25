using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 进入游戏立即触发对话测试：AI 先说，玩家后说
/// 挂在场景任意对象上，拖入 DialogueManager
/// </summary>
public class DialogueTest : MonoBehaviour
{
    [SerializeField] private DialogueManager dialogueManager;

    void Start()
    {
        StartCoroutine(RunTest());
    }

    private IEnumerator RunTest()
    {
        // 构造测试用 CardAsset
        CardAsset testCard = ScriptableObject.CreateInstance<CardAsset>();

        // AI 先说
        testCard.OnPlayDialogues = new List<CardDialogue>
        {
            new CardDialogue
            {
                Speaker = DialogueSpeaker.AI,
                Line    = "<wave><palette>道德方面有问题..."
            }
        };
        dialogueManager.PlayCardDialogue(testCard);

        // 等 AI 说完一半再让玩家接话（稍微错开，不要完全重叠）
        yield return new WaitForSeconds(1.2f);

        testCard.OnPlayDialogues = new List<CardDialogue>
        {
            new CardDialogue
            {
                Speaker = DialogueSpeaker.Player,
                Line    = "<shake><color=red>忠于欲望吧..."
            }
        };
        dialogueManager.PlayCardDialogue(testCard);
    }
}
