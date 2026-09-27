using UnityEngine;
using UnityEngine.SceneManagement;
using PixelCrushers.DialogueSystem;
using System.Collections;
using System.Collections.Generic;

public class DialoguePositionController : MonoBehaviour
{
    [System.Serializable]
    public class TargetPosition
    {
        [Tooltip("目标对象的标签 (Player 或 Companion)")]
        public string targetTag = "Player";

        [Tooltip("目标站立位置")]
        public Transform position;

        [Tooltip("朝向设置")]
        public FacingDirection facingDirection = FacingDirection.Auto;

        [Tooltip("需要的任务条件（留空则总是移动）")]
        public string requiredQuestName = "";

        [Tooltip("对话结束后恢复原始朝向")]
        public bool restoreFacingAfterDialogue = true;

        [HideInInspector]
        public GameObject targetObject;
        [HideInInspector]
        public Animator targetAnimator;
        [HideInInspector]
        public CharacterController targetController;
        [HideInInspector]
        public Vector3 originalPosition;
        [HideInInspector]
        public Vector3 originalScale;
        [HideInInspector]
        public bool hasSavedState = false;
    }

    [Header("对话位置设置")]
    [Tooltip("是否在对话开始时移动角色")]
    public bool moveCharactersOnDialogue = true;

    [Tooltip("所有需要移动的目标（玩家、小羊等）")]
    public List<TargetPosition> targetPositions = new List<TargetPosition>();

    [Header("移动动画设置")]
    [Tooltip("移动到目标位置的速度 (m/s) - 留空则自动使用对象的速度")]
    public float moveSpeed = 0f;

    [Tooltip("到达目标位置的容差距离")]
    public float arrivalThreshold = 0.15f;

    [Header("2D 章节设定")]
    [Tooltip("需要启用 2D 模式的场景关键字")]
    public string[] chapter2DKeywords = { "Chapter1", "persistent" };

    [Header("对话延迟控制")]
    [Tooltip("在移动完成后等待多久再显示对话框")]
    public float delayAfterMove = 0.3f;

    [Header("高级设置")]
    [Tooltip("对话结束后是否恢复角色原始位置")]
    public bool restorePositionAfterDialogue = false;

    [Header("调试")]
    public bool showDebugLogs = true;
    public bool showGizmos = true;

    private DialogueSystemTrigger dialogueTrigger;
    private Usable usable;
    private bool isMovingCharacters = false;
    private bool is2D = false;
    private bool hasMovedOnce = false;
    private bool isInConversation = false;
    private int activeMovingCount = 0;

    public enum FacingDirection
    {
        Auto,
        Left,
        Right
    }

    void Start()
    {
        DetectSceneMode();

        dialogueTrigger = GetComponent<DialogueSystemTrigger>();
        usable = GetComponent<Usable>();

        InitializeTargets();

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted += OnConversationStart;
            DialogueManager.instance.conversationEnded += OnConversationEnd;
            Log("✅ 已註冊對話事件 (Start + End)");
        }

        if (moveCharactersOnDialogue && dialogueTrigger != null)
        {
            Log("🔒 禁用 DialogueSystemTrigger，等待角色移动完成");
            dialogueTrigger.enabled = false;
        }

        if (usable != null)
        {
            Log("✅ 已检测到 Usable 组件");
        }
    }

    void OnDestroy()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted -= OnConversationStart;
            DialogueManager.instance.conversationEnded -= OnConversationEnd;
        }

        ForceRestoreAllTargets();
    }

    void InitializeTargets()
    {
        foreach (var target in targetPositions)
        {
            if (target.position == null)
            {
                Log($"⚠️ 警告：目标 {target.targetTag} 未设置位置！");
                continue;
            }

            GameObject obj = GameObject.FindGameObjectWithTag(target.targetTag);
            if (obj == null)
            {
                Log($"⚠️ 未找到标签为 {target.targetTag} 的对象");
                continue;
            }

            target.targetObject = obj;
            target.targetAnimator = obj.GetComponent<Animator>();
            target.targetController = obj.GetComponent<CharacterController>();

            Log($"✅ 初始化目标: {target.targetTag} ({obj.name})");
        }
    }

    void DetectSceneMode()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string activeSceneName = activeScene.name;

        if (activeSceneName == "persistent")
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name != "persistent")
                {
                    activeSceneName = scene.name;
                    break;
                }
            }
        }

        is2D = false;
        foreach (string keyword in chapter2DKeywords)
        {
            if (keyword == "persistent") continue;

            if (activeSceneName.Contains(keyword))
            {
                is2D = true;
                break;
            }
        }

        Log($"🎮 场景模式: {(is2D ? "2D (只能左右移动)" : "3D (前后左右移动)")}");
    }

    void OnUse(Transform user)
    {
        Log($"🎬 OnUse 被调用！User: {(user != null ? user.name : "null")}");

        if (!moveCharactersOnDialogue || hasMovedOnce)
        {
            Log($"⏭️ 跳过移动 - moveCharactersOnDialogue={moveCharactersOnDialogue}, hasMovedOnce={hasMovedOnce}");
            return;
        }

        Log("🎬 玩家点击 NPC，开始移动角色");
        StartCoroutine(MoveCharactersThenEnableDialogue());
    }

    void OnUse()
    {
        OnUse(null);
    }

    void OnConversationStart(Transform actor)
    {
        Log($"🎬 对话开始 - Actor: {(actor != null ? actor.name : "Unknown")}");

        bool isMyConversation = false;
        foreach (var target in targetPositions)
        {
            if (target.hasSavedState)
            {
                isMyConversation = true;
                break;
            }
        }

        if (isMyConversation)
        {
            isInConversation = true;
            Log("✅ 这是我的对话，标记为活跃状态");
        }
    }

    void OnConversationEnd(Transform actor)
    {
        Log($"🎬 对话结束事件触发 - Actor: {(actor != null ? actor.name : "Unknown")}");

        bool hasAnySavedState = false;
        foreach (var target in targetPositions)
        {
            if (target.hasSavedState)
            {
                hasAnySavedState = true;
                break;
            }
        }

        if (!hasAnySavedState)
        {
            Log("⏭️ 此 NPC 未触发移动流程，跳过恢复操作");
            return;
        }

        Log($"🔄 开始恢复流程 (isInConversation={isInConversation})");

        isInConversation = false;
        hasMovedOnce = false;

        if (moveCharactersOnDialogue && dialogueTrigger != null)
        {
            Log("🔒 对话结束，重新禁用 DialogueSystemTrigger");
            dialogueTrigger.enabled = false;
        }

        RestoreAllTargets();

        if (!restorePositionAfterDialogue) return;

        Log("🏁 對話結束，恢復角色位置");
        StartCoroutine(RestoreCharactersPosition());
    }

    void RestoreAllTargets()
    {
        Log("🔄 强制恢复所有目标状态");

        foreach (var target in targetPositions)
        {
            if (!target.hasSavedState)
            {
                Log($"⏭️ {target.targetTag} 没有保存状态，跳过");
                continue;
            }

            Log($"🔄 恢复 {target.targetTag} 状态");

            if (target.targetObject != null)
            {
                if (target.targetTag == "Player")
                {
                    var playerMovement = target.targetObject.GetComponent<PlayerMovement>();
                    if (playerMovement != null)
                    {
                        Log($"🔓 解锁 {target.targetTag} 控制");
                        playerMovement.SetMovementLocked(false, "DialoguePositionController");
                    }
                }

                if (target.restoreFacingAfterDialogue && target.originalScale != Vector3.zero)
                {
                    Log($"🔄 恢复 {target.targetTag} 原始朝向 (scale.x = {target.originalScale.x})");
                    Log($"   当前 scale.x = {target.targetObject.transform.localScale.x}");

                    target.targetObject.transform.localScale = target.originalScale;

                    Log($"   恢复后 scale.x = {target.targetObject.transform.localScale.x}");
                }
                else
                {
                    if (target.originalScale == Vector3.zero)
                    {
                        Log($"⚠️ {target.targetTag} originalScale 为零，无法恢复！");
                    }
                }
            }

            target.hasSavedState = false;
        }

        Log("✅ 所有目标恢复完成");
    }

    void ForceRestoreAllTargets()
    {
        Log("🚨 强制紧急恢复所有目标（组件销毁时）");
        RestoreAllTargets();
    }

    IEnumerator MoveCharactersThenEnableDialogue()
    {
        isMovingCharacters = true;
        hasMovedOnce = true;

        List<TargetPosition> validTargets = new List<TargetPosition>();

        foreach (var target in targetPositions)
        {
            if (target.position == null || target.targetObject == null || target.targetController == null)
            {
                Log($"⚠️ {target.targetTag} 配置不完整，跳过");
                continue;
            }

            if (!string.IsNullOrEmpty(target.requiredQuestName))
            {
                QuestState questState = QuestLog.GetQuestState(target.requiredQuestName);
                if (questState != QuestState.Active)
                {
                    Log($"⏭️ {target.targetTag} 任务条件未满足 (需要: {target.requiredQuestName}, 当前: {questState})，跳过移动");
                    continue;
                }
                else
                {
                    Log($"✅ {target.targetTag} 任务条件满足: {target.requiredQuestName}");
                }
            }

            if (target.targetTag == "Companion" && !target.targetObject.activeInHierarchy)
            {
                Log($"⏭️ {target.targetTag} 未激活，跳过移动");
                continue;
            }

            validTargets.Add(target);

            target.originalPosition = target.targetObject.transform.position;
            target.originalScale = target.targetObject.transform.localScale;
            target.hasSavedState = true;

            Log($"📍 保存 {target.targetTag} 原始位置: {target.originalPosition}");
            Log($"📐 保存 {target.targetTag} 原始朝向: scale = {target.originalScale} (x={target.originalScale.x})");

            if (target.targetTag == "Player")
            {
                var playerMovement = target.targetObject.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    Log($"🔒 锁定 {target.targetTag} 输入控制");
                    playerMovement.SetMovementLocked(true, "DialoguePositionController");
                }
            }
        }

        if (validTargets.Count == 0)
        {
            Log("⚠️ 没有有效的移动目标，直接启用对话");
            EnableDialogueTrigger();
            yield break;
        }

        activeMovingCount = validTargets.Count;

        foreach (var target in validTargets)
        {
            StartCoroutine(MoveTargetToPosition(target));
        }

        while (activeMovingCount > 0)
        {
            yield return null;
        }

        Log("✅ 所有角色已到达目标位置");

        yield return new WaitForSeconds(0.15f);

        foreach (var target in validTargets)
        {
            SetTargetFacingToNPC(target);
        }

        if (delayAfterMove > 0)
        {
            Log($"⏳ 等待 {delayAfterMove} 秒");
            yield return new WaitForSeconds(delayAfterMove);
        }

        isMovingCharacters = false;

        Log("🔓 启用 DialogueSystemTrigger 并触发对话");
        EnableDialogueTrigger();
        TriggerDialogue();
    }

    IEnumerator MoveTargetToPosition(TargetPosition target)
    {
        Vector3 targetPos = target.position.position;

        if (is2D)
        {
            targetPos.z = target.originalPosition.z;
            Log($"🎮 {target.targetTag} 2D 模式：鎖定 Z 軸 = {targetPos.z:F2}");
        }

        float actualSpeed = moveSpeed;
        if (actualSpeed <= 0.01f)
        {
            if (target.targetTag == "Player")
            {
                var playerMovement = target.targetObject.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    actualSpeed = playerMovement.walkSpeed;
                }
            }
            else if (target.targetTag == "Companion")
            {
                var sheepCompanion = target.targetObject.GetComponent<SheepCompanion>();
                if (sheepCompanion != null)
                {
                    actualSpeed = sheepCompanion.moveSpeed * 10f;
                }
            }

            if (actualSpeed <= 0.01f) actualSpeed = 4f;
        }

        float totalDistance = Vector3.Distance(target.targetObject.transform.position, targetPos);
        Log($"🚶 {target.targetTag} 開始移動 (距離: {totalDistance:F2}m, 速度: {actualSpeed}m/s)");

        int frameCount = 0;
        while (Vector3.Distance(target.targetObject.transform.position, targetPos) > arrivalThreshold)
        {
            Vector3 currentPos = target.targetObject.transform.position;
            Vector3 direction = (targetPos - currentPos).normalized;

            direction.y = 0;

            if (is2D)
            {
                direction.z = 0;
            }

            float step = actualSpeed * Time.deltaTime;
            Vector3 moveVector = direction * step;

            target.targetController.Move(moveVector);

            if (target.targetAnimator != null)
            {
                float horizontalSpeed = Mathf.Abs(direction.x);
                float verticalSpeed = is2D ? 0f : Mathf.Abs(direction.z);

                target.targetAnimator.SetFloat("speed", horizontalSpeed);

                if (target.targetTag == "Player")
                {
                    target.targetAnimator.SetFloat("speed2", verticalSpeed);

                    int walkDir = 3;
                    if (!is2D && verticalSpeed > horizontalSpeed)
                    {
                        walkDir = direction.z > 0 ? 1 : 2;
                    }
                    target.targetAnimator.SetInteger("walkDirection", walkDir);
                }
            }

            SetTargetFacing(target, direction);

            frameCount++;
            if (frameCount % 30 == 0)
            {
                float currentDist = Vector3.Distance(target.targetObject.transform.position, targetPos);
                Log($"🚶 {target.targetTag} 移动中... 剩余距离: {currentDist:F2}m");
            }

            yield return null;
        }

        Log($"✅ {target.targetTag} 已到達目標位置");

        if (target.targetAnimator != null)
        {
            Log($"🛑 {target.targetTag} 停止移动动画");
            target.targetAnimator.SetFloat("speed", 0f);

            if (target.targetTag == "Player")
            {
                target.targetAnimator.SetFloat("speed2", 0f);
                target.targetAnimator.SetInteger("walkDirection", 0);
            }
        }

        activeMovingCount--;
    }

    void SetTargetFacing(TargetPosition target, Vector3 moveDirection)
    {
        if (target.targetObject == null) return;

        if (Mathf.Abs(moveDirection.x) < 0.01f) return;

        Vector3 scale = target.targetObject.transform.localScale;
        bool shouldFaceRight = moveDirection.x > 0;
        bool currentlyFacingRight = scale.x > 0;

        if (currentlyFacingRight != shouldFaceRight)
        {
            scale.x *= -1;
            target.targetObject.transform.localScale = scale;
        }
    }

    void SetTargetFacingToNPC(TargetPosition target)
    {
        if (target.targetObject == null) return;

        Vector3 scale = target.targetObject.transform.localScale;
        bool shouldFaceRight = false;

        switch (target.facingDirection)
        {
            case FacingDirection.Auto:
                Vector3 directionToNPC = transform.position - target.targetObject.transform.position;
                shouldFaceRight = directionToNPC.x > 0;
                Log($"🔄 {target.targetTag} 自動朝向 NPC: {(shouldFaceRight ? "右" : "左")}");
                break;

            case FacingDirection.Left:
                shouldFaceRight = false;
                Log($"🔄 {target.targetTag} 強制朝左");
                break;

            case FacingDirection.Right:
                shouldFaceRight = true;
                Log($"🔄 {target.targetTag} 強制朝右");
                break;
        }

        bool currentlyFacingRight = scale.x > 0;
        if (currentlyFacingRight != shouldFaceRight)
        {
            scale.x *= -1;
            target.targetObject.transform.localScale = scale;
            Log($"✅ {target.targetTag} 翻轉朝向: {(shouldFaceRight ? "右" : "左")} (scale.x = {scale.x})");
        }
    }

    IEnumerator RestoreCharactersPosition()
    {
        List<TargetPosition> validTargets = new List<TargetPosition>();

        foreach (var target in targetPositions)
        {
            if (target.hasSavedState && target.targetObject != null && target.targetController != null)
            {
                validTargets.Add(target);

                if (target.targetTag == "Player")
                {
                    var playerMovement = target.targetObject.GetComponent<PlayerMovement>();
                    if (playerMovement != null)
                    {
                        playerMovement.SetMovementLocked(true, "DialoguePositionController");
                    }
                }
            }
        }

        if (validTargets.Count == 0) yield break;

        Log("🔙 恢復角色原始位置");

        activeMovingCount = validTargets.Count;

        foreach (var target in validTargets)
        {
            StartCoroutine(RestoreTargetPosition(target));
        }

        while (activeMovingCount > 0)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.15f);

        foreach (var target in validTargets)
        {
            if (target.restoreFacingAfterDialogue && target.originalScale != Vector3.zero)
            {
                Log($"🔄 恢复 {target.targetTag} 原始朝向 (位置恢复后)");
                target.targetObject.transform.localScale = target.originalScale;
            }

            if (target.targetTag == "Player")
            {
                var playerMovement = target.targetObject.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerMovement.SetMovementLocked(false, "DialoguePositionController");
                }
            }

            target.hasSavedState = false;
        }

        Log("✅ 所有角色已恢復原始位置");
    }

    IEnumerator RestoreTargetPosition(TargetPosition target)
    {
        Vector3 targetPos = target.originalPosition;

        float actualSpeed = moveSpeed;
        if (actualSpeed <= 0.01f)
        {
            if (target.targetTag == "Player")
            {
                var playerMovement = target.targetObject.GetComponent<PlayerMovement>();
                if (playerMovement != null) actualSpeed = playerMovement.walkSpeed;
            }
            else if (target.targetTag == "Companion")
            {
                var sheepCompanion = target.targetObject.GetComponent<SheepCompanion>();
                if (sheepCompanion != null) actualSpeed = sheepCompanion.moveSpeed * 10f;
            }

            if (actualSpeed <= 0.01f) actualSpeed = 4f;
        }

        while (Vector3.Distance(target.targetObject.transform.position, targetPos) > arrivalThreshold)
        {
            Vector3 currentPos = target.targetObject.transform.position;
            Vector3 direction = (targetPos - currentPos).normalized;

            direction.y = 0;

            if (is2D)
            {
                direction.z = 0;
            }

            float step = actualSpeed * Time.deltaTime;
            Vector3 moveVector = direction * step;

            target.targetController.Move(moveVector);

            if (target.targetAnimator != null)
            {
                float horizontalSpeed = Mathf.Abs(direction.x);
                float verticalSpeed = is2D ? 0f : Mathf.Abs(direction.z);

                target.targetAnimator.SetFloat("speed", horizontalSpeed);

                if (target.targetTag == "Player")
                {
                    target.targetAnimator.SetFloat("speed2", verticalSpeed);

                    int walkDir = 3;
                    if (!is2D && verticalSpeed > horizontalSpeed)
                    {
                        walkDir = direction.z > 0 ? 1 : 2;
                    }
                    target.targetAnimator.SetInteger("walkDirection", walkDir);
                }
            }

            SetTargetFacing(target, direction);

            yield return null;
        }

        if (target.targetAnimator != null)
        {
            target.targetAnimator.SetFloat("speed", 0f);

            if (target.targetTag == "Player")
            {
                target.targetAnimator.SetFloat("speed2", 0f);
                target.targetAnimator.SetInteger("walkDirection", 0);
            }
        }

        activeMovingCount--;
    }

    void EnableDialogueTrigger()
    {
        if (dialogueTrigger != null)
        {
            dialogueTrigger.enabled = true;
            Log("✅ DialogueSystemTrigger 已启用");
        }
    }

    void TriggerDialogue()
    {
        if (dialogueTrigger != null)
        {
            Log("💬 手动触发对话");
            dialogueTrigger.OnUse();
        }
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        foreach (var target in targetPositions)
        {
            if (target.position == null) continue;

            Gizmos.color = target.targetTag == "Player" ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(target.position.position, 0.3f);

            Gizmos.color = Color.yellow;
            Vector3 directionIndicator = target.position.position;

            if (target.facingDirection == FacingDirection.Left)
            {
                Gizmos.DrawLine(directionIndicator, directionIndicator + Vector3.left * 0.5f);
            }
            else if (target.facingDirection == FacingDirection.Right)
            {
                Gizmos.DrawLine(directionIndicator, directionIndicator + Vector3.right * 0.5f);
            }

            if (Application.isPlaying && target.targetObject != null)
            {
                Gizmos.color = isMovingCharacters ? Color.red : (target.targetTag == "Player" ? Color.green : Color.cyan);
                Gizmos.DrawLine(target.targetObject.transform.position, target.position.position);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (targetPositions.Count == 0) return;

        foreach (var target in targetPositions)
        {
            if (target.position == null) continue;

            string statusText = $"{target.targetTag}";
            if (!string.IsNullOrEmpty(target.requiredQuestName))
            {
                statusText += $"\n需要任务: {target.requiredQuestName}";
            }
            statusText += $"\n{(target.facingDirection == FacingDirection.Auto ? "Auto Facing" : target.facingDirection.ToString())}";

            if (Application.isPlaying && target.hasSavedState)
            {
                statusText += "\n✅ 已保存状态";
            }

            UnityEditor.Handles.color = target.targetTag == "Player" ? Color.green : Color.cyan;
            UnityEditor.Handles.Label(
                target.position.position + Vector3.up * 0.5f,
                statusText
            );
        }
    }
#endif

    void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"<color=cyan>[DialoguePositionController - {gameObject.name}]</color> {message}");
        }
    }

    [ContextMenu("測試：移動所有角色到目標位置")]
    public void TestMoveCharacters()
    {
        if (Application.isPlaying)
        {
            StartCoroutine(MoveCharactersThenEnableDialogue());
        }
        else
        {
            Debug.LogWarning("此功能只能在 Play Mode 下測試");
        }
    }

    [ContextMenu("強制恢復所有角色狀態")]
    public void ForceRestoreAll()
    {
        if (Application.isPlaying)
        {
            ForceRestoreAllTargets();
        }
        else
        {
            Debug.LogWarning("此功能只能在 Play Mode 下測試");
        }
    }
}
