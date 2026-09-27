using UnityEngine;
using System.Collections;
using PixelCrushers.DialogueSystem;

public class DelayedDialogueTrigger : MonoBehaviour
{
    [Header("延遲設定")]
    [Tooltip("等待場景完全加載後的額外延遲（秒）")]
    public float additionalDelay = 0.5f;

    [Tooltip("等待 GlobalSceneTransition 完成")]
    public bool waitForSceneTransition = true;

    [Header("對話設定")]
    [Tooltip("對話名稱")]
    public string conversationTitle = "Start";

    [Tooltip("對話者（Actor）- 留空則為 Player")]
    public Transform conversationActor;

    [Tooltip("對話對象（Conversant）- 留空則為自己")]
    public Transform conversationConversant;

    [Header("觸發條件")]
    [Tooltip("用於檢查是否已觸發的 Variable 名稱（留空則不檢查）")]
    public string checkVariableName = "";

    [Tooltip("觸發後設置的 Variable 名稱（留空則不設置）")]
    public string setVariableName = "";

    [Header("淡出設定")]
    [Tooltip("對話開始後是否從遮蔽色淡回透明")]
    public bool fadeFromBlackOnStart = false;

    [Tooltip("淡回所需秒數")]
    public float fadeFromBlackDuration = 3f;

    [Header("調試")]
    public bool enableDebugLog = true;

    void Start()
    {
        StartCoroutine(WaitAndTriggerDialogue());
    }

    IEnumerator WaitAndTriggerDialogue()
    {
        yield return null;

        if (!string.IsNullOrEmpty(checkVariableName))
        {
            bool alreadyTriggered = DialogueLua.GetVariable(checkVariableName).asBool;
            if (alreadyTriggered)
            {
                Log($"⏭️ Variable['{checkVariableName}'] = true，對話已觸發過，跳過");
                yield break;
            }
        }

        if (waitForSceneTransition)
        {
            Log("⏳ 等待 GlobalSceneTransition 完成...");
            while (GlobalSceneTransition.IsBusy)
                yield return null;
            Log("✅ GlobalSceneTransition 已完成");
        }

        if (additionalDelay > 0)
        {
            Log($"⏱️ 額外等待 {additionalDelay} 秒...");
            yield return new WaitForSeconds(additionalDelay);
        }

        Log($"💬 啟動對話：{conversationTitle}");

        Transform actor = conversationActor;
        if (actor == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                actor = playerObj.transform;
                Log($"✅ 找到 Player: {actor.name}");
            }
        }

        Transform conversant = conversationConversant != null ? conversationConversant : transform;

        DialogueManager.StartConversation(conversationTitle, actor, conversant);

        if (!string.IsNullOrEmpty(setVariableName))
        {
            DialogueLua.SetVariable(setVariableName, true);
            Log($"✅ 已設置 Variable['{setVariableName}'] = true");
        }

        if (fadeFromBlackOnStart)
        {
            Log($"🌅 從遮蔽色淡回，時長 {fadeFromBlackDuration} 秒");
            GlobalSceneTransition.FadeFromColor(fadeFromBlackDuration);
        }
    }

    void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log($"<color=orange>[DelayedDialogueTrigger - {gameObject.name}]</color> {message}");
    }
}
