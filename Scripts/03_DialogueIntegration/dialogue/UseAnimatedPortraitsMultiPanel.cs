using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using PixelCrushers;

public class UseAnimatedPortraitsMultiPanel : MonoBehaviour
{
    private StandardDialogueUI dialogueUI = null;
    private Dictionary<Transform, AnimatedPortrait> animatedPortraits = new Dictionary<Transform, AnimatedPortrait>();
    private Dictionary<StandardUISubtitlePanel, Animator> panelAnimators = new Dictionary<StandardUISubtitlePanel, Animator>();
    private Transform lastSpeaker = null;

    void OnEnable()
    {
        Debug.Log("[UseAnimatedPortraitsMultiPanel] 组件已启用，正在注册事件");
    }

    public void OnConversationLine(Subtitle subtitle)
    {
        Debug.Log($"[UseAnimatedPortraitsMultiPanel] OnConversationLine 被调用 - Speaker: {subtitle.speakerInfo.Name}");

        if (!FindDialogueUI())
        {
            Debug.LogWarning("[UseAnimatedPortraitsMultiPanel] 无法找到 DialogueUI！");
            return;
        }

        StartCoroutine(SetAnimatorAtEndOfFrame(subtitle));
    }

    private IEnumerator SetAnimatorAtEndOfFrame(Subtitle subtitle)
    {
        yield return CoroutineUtility.endOfFrame;

        DialogueActor dialogueActor;
        var panel = dialogueUI.conversationUIElements.standardSubtitleControls.GetPanel(subtitle, out dialogueActor);

        Debug.Log($"[UseAnimatedPortraitsMultiPanel] Panel: {(panel != null ? panel.name : "null")}, Panel Number: {(panel != null ? panel.panelNumber.ToString() : "N/A")}");

        if (panel != null && panel.portraitImage != null)
        {
            SetAnimatorController(panel, subtitle.speakerInfo.transform);
        }
        else
        {
            Debug.LogWarning($"[UseAnimatedPortraitsMultiPanel] Panel 或 PortraitImage 为 null");
        }

        lastSpeaker = subtitle.speakerInfo.transform;
    }

    public void OnConversationResponseMenu(Response[] responses)
    {
        if (!FindDialogueUI()) return;

        var reminderPanel = dialogueUI.conversationUIElements.standardSubtitleControls.defaultNPCPanel;
        if (reminderPanel != null && reminderPanel.portraitImage != null)
        {
            SetAnimatorController(reminderPanel, lastSpeaker);
        }
    }

    public void OnConversationEnd(Transform actor)
    {
        Debug.Log("[UseAnimatedPortraitsMultiPanel] 对话结束，清理缓存");
        animatedPortraits.Clear();
        panelAnimators.Clear();
    }

    private bool FindDialogueUI()
    {
        if (dialogueUI == null)
        {
            if (DialogueManager.instance != null && DialogueManager.displaySettings.dialogueUI != null)
            {
                dialogueUI = DialogueManager.displaySettings.dialogueUI.GetComponent<StandardDialogueUI>();
                Debug.Log($"[UseAnimatedPortraitsMultiPanel] 找到 DialogueUI: {(dialogueUI != null ? dialogueUI.name : "null")}");
            }
        }
        return (dialogueUI != null);
    }

    private void SetAnimatorController(StandardUISubtitlePanel panel, Transform speaker)
    {
        try
        {
            Debug.Log($"[UseAnimatedPortraitsMultiPanel] SetAnimatorController 开始 - Speaker: {(speaker != null ? speaker.name : "null")}, Panel: {(panel != null ? panel.name : "null")}");

            if (speaker == null)
            {
                Debug.LogWarning("[UseAnimatedPortraitsMultiPanel] Speaker 是 null，跳过");
                return;
            }

            if (panel == null)
            {
                Debug.LogWarning("[UseAnimatedPortraitsMultiPanel] Panel 是 null，跳过");
                return;
            }

            Debug.Log($"[UseAnimatedPortraitsMultiPanel] 检查 portraitImage，panel.portraitImage: {panel.portraitImage}");

            if (panel.portraitImage == null)
            {
                Debug.LogWarning($"[UseAnimatedPortraitsMultiPanel] Panel {panel.name} 的 portraitImage 是 null！跳过");
                return;
            }

            Debug.Log($"[UseAnimatedPortraitsMultiPanel] portraitImage 不为 null，继续");

            if (!panelAnimators.ContainsKey(panel))
            {
                Debug.Log($"[UseAnimatedPortraitsMultiPanel] 开始获取 Animator 组件");
                var panelAnimator = panel.portraitImage.GetComponent<Animator>();
                Debug.Log($"[UseAnimatedPortraitsMultiPanel] GetComponent<Animator> 返回: {panelAnimator}");

                if (panelAnimator == null)
                {
                    Debug.Log($"[UseAnimatedPortraitsMultiPanel] Animator 不存在，添加新的");
                    panelAnimator = panel.portraitImage.gameObject.AddComponent<Animator>();
                }

                panelAnimators[panel] = panelAnimator;
                Debug.Log($"[UseAnimatedPortraitsMultiPanel] 缓存了 Panel {panel.name} 的 Animator");
            }

            var currentAnimator = panelAnimators[panel];
            Debug.Log($"[UseAnimatedPortraitsMultiPanel] 当前 Animator: {currentAnimator}");

            if (!animatedPortraits.ContainsKey(speaker))
            {
                var animatedPortrait = speaker.GetComponentInChildren<AnimatedPortrait>();
                animatedPortraits.Add(speaker, animatedPortrait);
                Debug.Log($"[UseAnimatedPortraitsMultiPanel] 找到 {speaker.name} 的 AnimatedPortrait: {(animatedPortrait != null ? "是" : "否")}");
            }

            if (animatedPortraits[speaker] != null)
            {
                var animatorController = animatedPortraits[speaker].animatorController;
                Debug.Log($"[UseAnimatedPortraitsMultiPanel] AnimatorController: {animatorController}");

                if (currentAnimator.runtimeAnimatorController != animatorController)
                {
                    currentAnimator.runtimeAnimatorController = animatorController;
                    Debug.Log($"[UseAnimatedPortraitsMultiPanel] 已设置 {speaker.name} 的动画控制器");
                }
                else
                {
                    Debug.Log($"[UseAnimatedPortraitsMultiPanel] AnimatorController 已经是正确的");
                }

                currentAnimator.enabled = true;
                currentAnimator.Rebind();
                currentAnimator.Update(0f);
                Debug.Log($"[UseAnimatedPortraitsMultiPanel] 已启用并重置 Animator");

            }
            else
            {
                Debug.LogWarning($"[UseAnimatedPortraitsMultiPanel] {speaker.name} 没有 AnimatedPortrait 组件！");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UseAnimatedPortraitsMultiPanel] SetAnimatorController 发生异常：{e.Message}\n{e.StackTrace}");
        }
    }

}
