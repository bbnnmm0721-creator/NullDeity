using UnityEngine;
using PixelCrushers.DialogueSystem;

public class DialogueFastForward : MonoBehaviour
{
    [Header("Fast Forward Settings")]
    [Tooltip("快进按键")]
    public KeyCode fastForwardKey = KeyCode.Space;
    
    [Tooltip("是否也支持鼠标左键")]
    public bool allowMouseClick = false;
    
    [Tooltip("需要按住多久才触发快进（秒）")]
    public float holdTimeToTrigger = 0.3f;
    
    [Tooltip("立即跳过打字效果（推荐开启）")]
    public bool skipTypewriterImmediately = true;
    
    [Tooltip("自动继续的延迟时间（秒）- 打字停止后等待多久再继续")]
    public float autoContinueDelay = 0.15f;
    
    [Tooltip("检测选项菜单")]
    public bool detectResponseMenu = true;

    [Header("Debug")]
    [Tooltip("显示详细调试信息")]
    public bool debugMode = false;

    private AbstractTypewriterEffect typewriterEffect;
    private StandardUIMenuPanel menuPanel;
    private bool isFastForwarding = false;
    private float lastTypingStoppedTime = -999f;
    private bool continueScheduled = false;
    
    private float keyPressStartTime = -999f;
    private bool isKeyPressed = false;
    private bool fastForwardActivated = false;

    void OnEnable()
    {
        if (DialogueManager.instance != null && DialogueManager.instance.GetComponent<DialogueSystemEvents>() != null)
        {
            var events = DialogueManager.instance.GetComponent<DialogueSystemEvents>();
            events.conversationEvents.onConversationStart.AddListener(OnConversationStart);
            events.conversationEvents.onConversationLine.AddListener(OnConversationLine);
        }
    }

    void OnDisable()
    {
        if (DialogueManager.instance != null && DialogueManager.instance.GetComponent<DialogueSystemEvents>() != null)
        {
            var events = DialogueManager.instance.GetComponent<DialogueSystemEvents>();
            events.conversationEvents.onConversationStart.RemoveListener(OnConversationStart);
            events.conversationEvents.onConversationLine.RemoveListener(OnConversationLine);
        }
    }

    void OnConversationStart(Transform actor)
    {
        if (debugMode) Debug.Log("[DialogueFastForward] 对话启动");
        Invoke(nameof(FindTypewriter), 0.1f);
    }

    void OnConversationLine(Subtitle subtitle)
    {
        if (debugMode) Debug.Log($"[DialogueFastForward] 新对话行: {subtitle.speakerInfo.Name}");
        
        lastTypingStoppedTime = -999f;
        continueScheduled = false;
        CancelInvoke(nameof(AutoContinue));
    }

    void FindTypewriter()
    {
        typewriterEffect = null;
        menuPanel = null;

        if (detectResponseMenu)
        {
            menuPanel = FindObjectOfType<StandardUIMenuPanel>(true);
            if (menuPanel != null && debugMode)
            {
                Debug.Log($"[DialogueFastForward] 找到 Menu Panel");
            }
        }

        var allTypewriters = FindObjectsOfType<AbstractTypewriterEffect>(true);
        if (debugMode) Debug.Log($"[DialogueFastForward] 场景中找到 {allTypewriters.Length} 个 Typewriter 组件");
        
        foreach (var tw in allTypewriters)
        {
            if (tw.gameObject.activeInHierarchy)
            {
                typewriterEffect = tw;
                break;
            }
        }

        if (typewriterEffect == null && allTypewriters.Length > 0)
        {
            typewriterEffect = allTypewriters[0];
        }

        if (typewriterEffect != null)
        {
            Debug.Log($"[DialogueFastForward] ✅ 成功找到 Typewriter！");
        }
        else
        {
            Debug.LogWarning("[DialogueFastForward] ❌ 找不到任何 AbstractTypewriterEffect 组件！");
        }
    }

    void Update()
    {
        // 如果 backlog 開啟，不處理快進
        if (BacklogUI.IsBacklogOpen)
        {
            if (isFastForwarding)
            {
                StopFastForward();
            }
            ResetKeyState();
            return;
        }

        if (typewriterEffect == null) return;

        if (!DialogueManager.isConversationActive)
        {
            if (isFastForwarding)
            {
                StopFastForward();
            }
            ResetKeyState();
            return;
        }

        // 检查按键状态
        bool fastForwardPressed = Input.GetKey(fastForwardKey) ||
                                   (allowMouseClick && Input.GetMouseButton(0));

        // 按键刚按下
        if (fastForwardPressed && !isKeyPressed)
        {
            isKeyPressed = true;
            keyPressStartTime = Time.time;
            fastForwardActivated = false;

            if (debugMode) Debug.Log($"[DialogueFastForward] 按键按下，开始计时");
        }
        // 按键持续按住
        else if (fastForwardPressed && isKeyPressed)
        {
            float holdDuration = Time.time - keyPressStartTime;

            // 达到长按时间阈值，激活快进
            if (!fastForwardActivated && holdDuration >= holdTimeToTrigger)
            {
                fastForwardActivated = true;
                StartFastForward();

                if (debugMode) Debug.Log($"[DialogueFastForward] ⏱️ 长按 {holdDuration:F2}s，触发快进");
            }

            // 如果已经激活快进，继续执行快进逻辑
            if (fastForwardActivated)
            {
                HandleContinuousFastForward();
            }
        }
        // 按键松开
        else if (!fastForwardPressed && isKeyPressed)
        {
            float holdDuration = Time.time - keyPressStartTime;

            if (debugMode)
            {
                if (fastForwardActivated)
                {
                    Debug.Log($"[DialogueFastForward] 松开按键，停止快进（按住了 {holdDuration:F2}s）");
                }
                else
                {
                    Debug.Log($"[DialogueFastForward] 松开按键，未达到长按时间（仅按住 {holdDuration:F2}s < {holdTimeToTrigger}s）");
                }
            }

            if (fastForwardActivated)
            {
                StopFastForward();
            }

            ResetKeyState();
        }
    }


    private void ResetKeyState()
    {
        isKeyPressed = false;
        keyPressStartTime = -999f;
        fastForwardActivated = false;
    }

    private void HandleContinuousFastForward()
    {
        if (detectResponseMenu && IsResponseMenuActive())
        {
            if (debugMode) Debug.Log("[DialogueFastForward] 检测到选项菜单，暂停自动继续");
            CancelInvoke(nameof(AutoContinue));
            continueScheduled = false;
            lastTypingStoppedTime = -999f;
            return;
        }

        bool isCurrentlyTyping = typewriterEffect != null && typewriterEffect.isPlaying;

        if (isCurrentlyTyping)
        {
            if (skipTypewriterImmediately)
            {
                typewriterEffect.Stop();
                if (debugMode) Debug.Log("[DialogueFastForward] ⏭️ 立即跳过打字");
            }
            
            if (continueScheduled)
            {
                CancelInvoke(nameof(AutoContinue));
                continueScheduled = false;
            }
            
            lastTypingStoppedTime = -999f;
        }
        else
        {
            float currentTime = Time.time;
            
            if (lastTypingStoppedTime < 0)
            {
                lastTypingStoppedTime = currentTime;
                if (debugMode) Debug.Log($"[DialogueFastForward] 打字停止，记录时间: {lastTypingStoppedTime}");
            }
            
            if (!continueScheduled)
            {
                float timeSinceStopped = currentTime - lastTypingStoppedTime;
                
                if (timeSinceStopped >= autoContinueDelay)
                {
                    if (debugMode) Debug.Log($"[DialogueFastForward] 安排自动继续（已等待 {timeSinceStopped:F2}s）");
                    continueScheduled = true;
                    Invoke(nameof(AutoContinue), 0.01f);
                }
            }
        }
    }

    private bool IsResponseMenuActive()
    {
        if (menuPanel == null) return false;

        bool isActive = menuPanel.isActiveAndEnabled && 
                        menuPanel.gameObject.activeInHierarchy;

        if (isActive && menuPanel.buttons != null)
        {
            foreach (var button in menuPanel.buttons)
            {
                if (button != null && button.isActiveAndEnabled)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void AutoContinue()
    {
        continueScheduled = false;
        
        if (!isFastForwarding)
        {
            if (debugMode) Debug.Log("[DialogueFastForward] 已停止快进，取消自动继续");
            return;
        }

        if (!DialogueManager.isConversationActive)
        {
            if (debugMode) Debug.Log("[DialogueFastForward] 对话已结束，取消自动继续");
            return;
        }

        if (detectResponseMenu && IsResponseMenuActive())
        {
            if (debugMode) Debug.Log("[DialogueFastForward] 有选项菜单，取消自动继续");
            return;
        }

        if (typewriterEffect != null && typewriterEffect.isPlaying)
        {
            if (debugMode) Debug.Log("[DialogueFastForward] 还在打字，取消自动继续");
            return;
        }

        if (DialogueManager.standardDialogueUI != null)
        {
            if (debugMode) Debug.Log("[DialogueFastForward] 🚀 执行自动继续");
            DialogueManager.standardDialogueUI.OnContinue();
        }
    }

    private void StartFastForward()
    {
        isFastForwarding = true;
        lastTypingStoppedTime = -999f;
        continueScheduled = false;
        
        if (debugMode) Debug.Log("[DialogueFastForward] ⚡ 开始连续快进");
    }

    private void StopFastForward()
    {
        isFastForwarding = false;
        lastTypingStoppedTime = -999f;
        continueScheduled = false;
        CancelInvoke(nameof(AutoContinue));
        
        if (debugMode) Debug.Log("[DialogueFastForward] ⏸️ 停止连续快进");
    }

    void OnDestroy()
    {
        CancelInvoke();
    }

    [ContextMenu("手动查找 Typewriter")]
    public void ManualFindTypewriter()
    {
        FindTypewriter();
    }

    [ContextMenu("显示当前状态")]
    public void ShowStatus()
    {
        Debug.Log("=== DialogueFastForward 状态 ===");
        Debug.Log($"Typewriter 找到: {typewriterEffect != null}");
        if (typewriterEffect != null)
        {
            Debug.Log($"是否在播放: {typewriterEffect.isPlaying}");
        }
        Debug.Log($"Menu Panel 找到: {menuPanel != null}");
        Debug.Log($"选项菜单激活: {IsResponseMenuActive()}");
        Debug.Log($"对话进行中: {DialogueManager.isConversationActive}");
        Debug.Log($"按键按下: {isKeyPressed}");
        if (isKeyPressed)
        {
            Debug.Log($"按键持续时间: {(Time.time - keyPressStartTime):F2}s");
        }
        Debug.Log($"快进已激活: {fastForwardActivated}");
        Debug.Log($"正在快进: {isFastForwarding}");
        Debug.Log($"继续已安排: {continueScheduled}");
        Debug.Log("==============================");
    }
}
