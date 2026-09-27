using UnityEngine;
using PixelCrushers.DialogueSystem;

public class QuestBoxInteraction : MonoBehaviour
{
    [Header("Quest Settings")]
    [Tooltip("第一次互动的任务名称")]
    public string firstInteractionQuest = "找到箱子";

    [Tooltip("解谜的任务名称")]
    public string puzzleQuest = "解箱子謎題";

    [Header("Dialogue")]
    [Tooltip("第一次互动的对话名称")]
    public string firstDialogueConversation = "箱子";

    [Tooltip("对话的 NPC 名称")]
    public string conversationActor = "System";

    [Header("Puzzle Reference")]
    [Tooltip("谜题管理器引用")]
    public SymbolPuzzleManager puzzleManager;

    [Header("提示冷卻")]
    [Tooltip("「現在還不能打開」提示的冷卻時間（秒）")]
    public float alertCooldown = 2f;

    private bool hasFirstInteraction = false;
    private float lastAlertTime = -999f;

    /// <summary>箱子被互動時呼叫。</summary>
    public void OnBoxInteract()
    {
        // 若對話已在進行中，直接忽略（防止 ProximitySelector 和 OnBoxInteract 同框衝突）
        if (DialogueManager.isConversationActive)
        {
            Debug.LogWarning("[QuestBox] 對話系統仍在進行中，忽略互動");
            return;
        }


        QuestState findBoxState = QuestLog.GetQuestState(firstInteractionQuest);
        QuestState puzzleState = QuestLog.GetQuestState(puzzleQuest);

        Debug.Log($"[QuestBox] FindBox狀態: {findBoxState}, Puzzle狀態: {puzzleState}");

        if (findBoxState == QuestState.Active && !hasFirstInteraction)
        {
            Debug.Log("[QuestBox] 第一次互動，觸發對話");

            // 確保移動在對話開始前就解鎖（避免 DialoguePositionController 協程殘留鎖）
            if (PlayerMovement.Instance != null)
                PlayerMovement.Instance.SetMovementLocked(false, "QuestBoxInteraction-PreDialogue");

            DialogueManager.StartConversation(firstDialogueConversation, transform);
            hasFirstInteraction = true;
            return;
        }

        if (puzzleState == QuestState.Active)
        {
            Debug.Log("[QuestBox] 解謎任務激活，打開謎題");
            if (puzzleManager != null)
                puzzleManager.EnterPuzzleMode();
            else
                Debug.LogError("[QuestBox] Puzzle Manager 未設置！");
            return;
        }

        if (Time.unscaledTime - lastAlertTime < alertCooldown) return;
        lastAlertTime = Time.unscaledTime;
        DialogueManager.ShowAlert("現在還不能打開這箱子...");
    }

}
