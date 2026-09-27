using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using PixelCrushers.DialogueSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement Instance { get; private set; }

    [Header("移動參數")]
    [Tooltip("行走速度 (m/s)")] public float walkSpeed = 4f;
    [Tooltip("跑步速度 (m/s)")] public float runSpeed = 7f;
    [Tooltip("蹲下速度 (m/s)")] public float crouchSpeed = 2f;
    [Tooltip("重力 (m/s²)")] public float gravity = 16f;
    [Tooltip("跳躍初速度 (m/s)")] public float jumpSpeed = 7f;

    [Header("跳躍設定")]
    [Tooltip("落地檢測延遲（避免誤判）")] public float groundedDelay = 0.1f;
    [Tooltip("跳躍冷卻時間")] public float jumpCooldown = 0.2f;
    [Tooltip("強制重置Jump Trigger的時間")] public float jumpTriggerResetTime = 0.1f;

    [Header("生成點設定")]
    [Tooltip("場景載入後等待幾秒再開始移動邏輯")] public float spawnWaitTime = 0.5f;

    [Header("Camera State Control")]
    public bool disableOnNonMainCamera = true;

    [Header("Animator Params")]
    public string pIsGrounded = "grounded";
    public string pYVel = "yVel";
    public string pJumpTrig = "jump";
    public string pFace = "face";
    public string pFacingBack = "facingBack";
    public string pIsCrouching = "isCrouching";

    [Header("背面狀態控制")]
    public bool enableBackFacing = true;
    public float turnToBackDuration = 0.5f;
    public float turnToFrontDuration = 0.5f;

    [Header("移動限制")]
    public bool isMovementLocked = false;

    [Header("蹲下設定")]
    [Tooltip("啟用蹲下功能的任務名稱（留空=始終可用）")]
    public string crouchQuestName = "";
    [Tooltip("是否需要任務處於 Active 狀態才能蹲下")]
    public bool requireQuestActive = true;

    [Header("2D 章節設定")]
    [Tooltip("需要啟用2D模式的場景關鍵字")]
    public string[] chapter2DKeywords = { "Chapter1", "persistent" };

    [Header("對話系統整合")]
    [Tooltip("對話開始時是否強制回到 Standby 動畫")]
    public bool resetAnimationOnDialogueStart = true;
    [Tooltip("對話開始時是否鎖定移動（建議保持開啟）")]
    public bool lockMovementOnDialogue = true;
    [Tooltip("是否顯示對話系統調試日誌")]
    public bool showDialogueDebugLogs = true;

    [Header("方向判定")]
    [Tooltip("搖桿死區補償：低於此幅度視為靜止（只影響動畫方向判定，不影響實際移動）")]
    [Range(0.05f, 0.4f)] public float stickMagnitudeThreshold = 0.2f;
    [Tooltip("純前方扇形半角（°）：值越大 dir=2 越好觸發，建議 30~45")]
    [Range(15f, 55f)] public float forwardSectorAngle = 35f;
    [Tooltip("純後方扇形半角（°）")]
    [Range(15f, 55f)] public float backSectorAngle = 30f;
    [Tooltip("純側移扇形半角（°）：值越小越難觸發純側移")]
    [Range(5f, 30f)] public float sideSectorAngle = 15f;

    [Header("待機動畫設定")]
    [Tooltip("幾秒沒動後觸發睡著動畫")] public float sleepTriggerDelay = 30f;

    private float idleTimer = 0f;

    private PlayerInputActions inputActions;
    private CharacterController cc;
    private Animator anim;
    private Vector3 velocity;
    private bool is2D;
    private bool facingRight = true;
    private bool firstInitialized = false;

    private bool isGrounded;
    private bool wasGrounded;
    private float lastGroundedTime;
    private float lastJumpTime;
    private bool jumpRequested;
    private int lastFaceDirection;
    private float jumpTriggerTime;
    private bool jumpTriggerSet;

    private float sceneLoadTime;
    private bool allowMovement;
    private Vector3 initialZ;
    private bool isFacingBack = false;
    private bool isTransitioning = false;
    private float crouchValue = 0f;
    private bool isCrouching = false;

    private bool originalEnabledState;
    private bool isControlledByCameraState;

    private bool isInDialogue = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();

        inputActions = new PlayerInputActions();

        ResetMovementState();
        ApplySceneMode(SceneManager.GetActiveScene().name);
        firstInitialized = true;

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (disableOnNonMainCamera && CameraStateManager.Instance != null)
        {
            originalEnabledState = enabled;
            CameraStateManager.Instance.onCameraStateChanged.AddListener(OnCameraStateChanged);
            Debug.Log("[PlayerMovement] 已註冊相機狀態監聽");
        }

        RegisterDialogueSystemEvents();
    }

    void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Enable();
            inputActions.Player.Jump.performed += OnJumpPerformed;
            inputActions.Player.Jump.canceled += OnJumpCanceled;
        }
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Jump.performed -= OnJumpPerformed;
            inputActions.Player.Jump.canceled -= OnJumpCanceled;
            inputActions.Player.Disable();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            if (inputActions != null)
            {
                inputActions.Dispose();
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        if (CameraStateManager.Instance != null)
        {
            CameraStateManager.Instance.onCameraStateChanged.RemoveListener(OnCameraStateChanged);
        }

        UnregisterDialogueSystemEvents();
    }

    #region Dialogue System Integration

    void RegisterDialogueSystemEvents()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted += OnConversationStart;
            DialogueManager.instance.conversationEnded += OnConversationEnd;
            LogDialogue("已註冊 Dialogue System 事件監聽");
        }
        else
        {
            Invoke(nameof(DelayedRegisterDialogueEvents), 0.5f);
        }
    }

    void DelayedRegisterDialogueEvents()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted += OnConversationStart;
            DialogueManager.instance.conversationEnded += OnConversationEnd;
            LogDialogue("延遲註冊 Dialogue System 事件成功");
        }
    }

    void UnregisterDialogueSystemEvents()
    {
        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted -= OnConversationStart;
            DialogueManager.instance.conversationEnded -= OnConversationEnd;
            LogDialogue("已取消註冊 Dialogue System 事件");
        }
    }

    void OnConversationStart(Transform actor)
    {
        isInDialogue = true;
        LogDialogue($"對話開始 - Actor: {(actor != null ? actor.name : "Unknown")}");

        if (resetAnimationOnDialogueStart)
        {
            ForceResetToStandby();
            LogDialogue("✅ 已強制重置動畫至 Standby");
        }

        if (lockMovementOnDialogue)
        {
            SetMovementLocked(true, "對話系統");
            LogDialogue("🔒 已鎖定角色移動");
        }
    }

    void OnConversationEnd(Transform actor)
    {
        isInDialogue = false;
        LogDialogue($"對話結束 - Actor: {(actor != null ? actor.name : "Unknown")}");

        if (lockMovementOnDialogue)
        {
            SetMovementLocked(false, "對話系統");
            LogDialogue("🔓 已解鎖角色移動");
        }
    }

    [ContextMenu("強制回到 Standby（對話用）")]
    public void ForceResetToStandby()
    {
        if (anim)
        {
            anim.SetFloat("speed", 0f);
            anim.SetFloat("speed2", 0f);
            anim.SetInteger("walkDirection", 0);
            anim.SetBool(pIsGrounded, true);
            anim.SetFloat(pYVel, 0f);
            anim.SetFloat(pIsCrouching, 0f);
            anim.ResetTrigger(pJumpTrig);

            jumpTriggerSet = false;
            jumpRequested = false;
            velocity.y = 0f;
            crouchValue = 0f;
            isCrouching = false;

            LogDialogue("動畫已重置至 Standby 狀態");
        }
    }

    void LogDialogue(string message)
    {
        if (showDialogueDebugLogs)
            Debug.Log($"[PlayerMovement-Dialogue] {message}");
    }

    /// <summary>查詢角色是否正在對話中。</summary>
    public bool IsInDialogue() => isInDialogue;

    /// <summary>當前水平速度（X 軸，單位 m/s）。</summary>
    public float HorizontalVelocity => velocity.x;

    #endregion

    private void OnJumpPerformed(InputAction.CallbackContext context) => jumpRequested = true;
    private void OnJumpCanceled(InputAction.CallbackContext context) => jumpRequested = false;

    void OnCameraStateChanged(bool isMainCameraActive)
    {
        if (!disableOnNonMainCamera) return;

        if (isMainCameraActive)
        {
            if (isControlledByCameraState)
            {
                enabled = originalEnabledState;
                isControlledByCameraState = false;
            }
        }
        else
        {
            if (!isControlledByCameraState)
            {
                originalEnabledState = enabled;
                enabled = false;
                isControlledByCameraState = true;
            }
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetMovementState();
        allowMovement = false;
        Invoke(nameof(EnableMovement), spawnWaitTime);
        ApplySceneMode(scene.name);
    }

    void EnableMovement()
    {
        allowMovement = true;
        initialZ = transform.position;
    }

    void ResetMovementState()
    {
        lastGroundedTime = Time.time;
        lastJumpTime = -jumpCooldown;
        lastFaceDirection = facingRight ? 1 : -1;
        jumpTriggerTime = -1f;
        jumpTriggerSet = false;
        sceneLoadTime = Time.time;
        allowMovement = true;
    }

    void ApplySceneMode(string sceneName)
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

        bool newIs2D = false;
        foreach (string keyword in chapter2DKeywords)
        {
            if (keyword == "persistent") continue;
            if (activeSceneName.Contains(keyword))
            {
                newIs2D = true;
                break;
            }
        }

        if (is2D != newIs2D)
        {
            is2D = newIs2D;
            Debug.Log($"[场景模式切换] {activeSceneName} → {(is2D ? "2D模式(只能左右)" : "3D模式(前后左右)")}");
        }
    }

    void Update()
    {
        if (cc == null || !allowMovement || isMovementLocked || isTransitioning) return;

        if (Input.GetKeyDown(KeyCode.K))
            ForceResetAnimation();

        Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        bool runInput = inputActions.Player.Run.IsPressed();
        bool crouchInput = inputActions.Player.Crouch.IsPressed();

        float h = moveInput.x;
        float v = is2D ? 0 : moveInput.y;
        Vector3 input = new Vector3(h, 0, v).normalized;

        if (isFacingBack) h = 0;

        bool canCrouch = CanCrouch();
        float speed;

        if (canCrouch && crouchInput && isGrounded)
        {
            isCrouching = true;
            crouchValue = 1f;
            speed = (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f) ? crouchSpeed : 0f;
        }
        else
        {
            isCrouching = false;
            crouchValue = 0f;
            speed = runInput ? runSpeed : walkSpeed;
        }

        Vector3 planar = input * speed;

        // 睡著狀態期間強制移動速度為零，但仍讀取輸入以便觸發 SleepEnd
        if (IsInSleepState())
            planar = Vector3.zero;

        UpdateGroundedState();
        HandleJumpTriggerReset();

        if (isGrounded && !wasGrounded)
            velocity.y = -1f;

        if (jumpRequested && CanJump())
        {
            PerformJump(h);
            jumpRequested = false;
        }

        if (!isGrounded)
            velocity.y -= gravity * Time.deltaTime;

        velocity.x = planar.x;
        velocity.z = planar.z;
        if (is2D) velocity.z = 0;

        if (velocity.magnitude > 0.01f)
            cc.Move(velocity * Time.deltaTime);

        UpdateAnimationParameters(h, v);

        if (is2D)
            HandleFlipping(h);
        else
            Handle3DFlipping(h, v);

        wasGrounded = isGrounded;
    }

    void Handle3DFlipping(float h, float v)
    {
        const float DeadZone = 0.01f;
        int dir = GetDir(h, v);

        // 正前方、斜前有獨立左右動畫，不依賴 Scale 翻轉
        if (dir == 2 || dir == 4 || dir == 5)
        {
            ResetFlip();
            return;
        }

        // 側移（dir=3）與斜後（dir=6）靠 Scale Flip 區分朝向
        if (Mathf.Abs(h) > DeadZone)
        {
            if (h > DeadZone && !facingRight) Flip();
            else if (h < -DeadZone && facingRight) Flip();
        }
    }

    void ResetFlip()
    {
        facingRight = true;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x);
        transform.localScale = s;
    }

    bool CanCrouch()
    {
        if (string.IsNullOrEmpty(crouchQuestName)) return true;
        QuestState questState = QuestLog.GetQuestState(crouchQuestName);
        return requireQuestActive ? questState == QuestState.Active : questState != QuestState.Unassigned;
    }

    void HandleFlipping(float h)
    {
        if (h > 0 && !facingRight) Flip();
        if (h < 0 && facingRight) Flip();

        if (is2D)
        {
            velocity.z = 0;
            if (initialZ != Vector3.zero)
            {
                Vector3 pos = transform.position;
                pos.z = initialZ.z;
                transform.position = pos;
            }
        }
    }

    void UpdateGroundedState()
    {
        if (cc.isGrounded)
        {
            lastGroundedTime = Time.time;
            isGrounded = true;
        }
        else
        {
            isGrounded = (Time.time - lastGroundedTime) < groundedDelay;
        }
    }

    void HandleJumpTriggerReset()
    {
        if (!jumpTriggerSet) return;

        if ((Time.time - jumpTriggerTime) >= jumpTriggerResetTime ||
            (isGrounded && velocity.y <= 0.5f) ||
            (velocity.y < -2f && cc.isGrounded))
        {
            if (anim)
            {
                anim.ResetTrigger(pJumpTrig);
                jumpTriggerSet = false;
            }
        }
    }

    bool CanJump()
    {
        return isGrounded &&
               (Time.time - lastJumpTime) >= jumpCooldown &&
               velocity.y <= 0.1f &&
               !jumpTriggerSet &&
               !isCrouching;
    }

    void PerformJump(float horizontalInput)
    {
        velocity.y = jumpSpeed;
        lastJumpTime = Time.time;

        if (anim)
        {
            int jumpFaceDirection = (horizontalInput != 0)
                ? (horizontalInput > 0 ? 1 : -1)
                : (facingRight ? 1 : -1);

            anim.ResetTrigger(pJumpTrig);
            anim.SetTrigger(pJumpTrig);
            jumpTriggerTime = Time.time;
            jumpTriggerSet = true;

            anim.SetInteger(pFace, jumpFaceDirection);
            lastFaceDirection = jumpFaceDirection;
        }
    }

    void UpdateAnimationParameters(float h, float v)
    {
        if (!anim) return;

        int dir = GetDir(h, v);

        anim.SetFloat("speed", Mathf.Abs(h));
        anim.SetFloat("speed2", Mathf.Abs(v));
        anim.SetInteger("walkDirection", dir);
        anim.SetBool(pIsGrounded, isGrounded);
        anim.SetFloat(pYVel, velocity.y);
        anim.SetBool(pFacingBack, isFacingBack);
        anim.SetFloat(pIsCrouching, crouchValue);

        if (Input.GetKey(KeyCode.Tab))
            Debug.Log($"[動畫參數] speed={Mathf.Abs(h):F2} speed2={Mathf.Abs(v):F2} walkDirection={dir} is2D={is2D}");

        if (isGrounded && !jumpTriggerSet)
        {
            int currentFaceDirection = facingRight ? 1 : -1;
            if (currentFaceDirection != lastFaceDirection)
            {
                anim.SetInteger(pFace, currentFaceDirection);
                lastFaceDirection = currentFaceDirection;
            }
        }

        // 閒置計時：靜止且在地面且非蹲下且非對話中才計時
        bool isIdle = dir == 0 && isGrounded && !isCrouching && !isInDialogue;
        if (isIdle)
            idleTimer += Time.deltaTime;
        else
            idleTimer = 0f;

        anim.SetFloat("idleTime", idleTimer);
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }

    /// <summary>
    /// 角度扇形方向判定。
    /// atan2(-v, h) 讓 0°=右、90°=前(v&lt;0)、180°=左、270°=後(v&gt;0)。
    ///
    /// walkDirection 值對照：
    ///   0 靜止 | 1 正後 | 2 正前 | 3 側移
    ///   4 右斜前 | 5 左斜前 | 6 斜後（左右由 Scale Flip 區分）
    /// </summary>
    int GetDir(float h, float v)
    {
        if (new Vector2(h, v).magnitude < stickMagnitudeThreshold) return 0;

        // atan2(-v, h)：0°=右, 90°=前(v<0), 180°=左, 270°=後(v>0)
        float angle = Mathf.Atan2(-v, h) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        float fLow = 90f - forwardSectorAngle; // e.g. 55°
        float fHigh = 90f + forwardSectorAngle; // e.g. 125°
        float bLow = 270f - backSectorAngle;    // e.g. 240°
        float bHigh = 270f + backSectorAngle;    // e.g. 300°

        // 正前方
        if (angle > fLow && angle < fHigh) return 2;

        // 右斜前（純右側移緩衝之後到前方左界）
        if (angle > sideSectorAngle && angle <= fLow) return 4;

        // 左斜前（前方右界到純左側移緩衝之前）
        if (angle >= fHigh && angle < 180f - sideSectorAngle) return 5;

        // 正後方
        if (angle > bLow && angle < bHigh) return 1;

        // 斜後（左右統一為 6，由 Scale Flip 區分朝向）
        if (angle >= 180f + sideSectorAngle && angle <= bLow) return 6;
        if (angle >= bHigh && angle <= 360f - sideSectorAngle) return 6;

        // 純側移（0°±side, 180°±side）
        return 3;
    }

    /// <summary>根據出生側設定角色面向（2D 模式用）。</summary>
    public void FaceFromSpawn(SpawnSide side)
    {
        if (!is2D) return;
        var s = transform.localScale;
        s.x = (side == SpawnSide.Left) ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
        transform.localScale = s;
        facingRight = (s.x > 0);
        lastFaceDirection = facingRight ? 1 : -1;
    }

    /// <summary>傳送角色到指定位置並記錄初始 Z 值。</summary>
    public void SetPositionForSpawn(Vector3 position)
    {
        initialZ = position;
        transform.position = position;
    }

    [ContextMenu("強制重置動畫")]
    public void ForceResetAnimation()
    {
        if (anim)
        {
            anim.ResetTrigger(pJumpTrig);
            anim.SetBool(pIsGrounded, true);
            anim.SetFloat(pYVel, 0f);
            anim.SetFloat(pIsCrouching, 0f);
            jumpTriggerSet = false;
            jumpRequested = false;
            velocity.y = 0f;
            crouchValue = 0f;
            isCrouching = false;
        }
    }

    [ContextMenu("重置跳躍狀態")]
    public void ResetJumpState()
    {
        if (anim) anim.ResetTrigger(pJumpTrig);
        jumpTriggerSet = false;
        jumpRequested = false;
    }

    /// <summary>鎖定或解鎖角色移動。</summary>
    public void SetMovementLocked(bool locked, string reason = "")
    {
        isMovementLocked = locked;
    }

    /// <summary>觸發背面轉向協程。</summary>
    public void TurnToBack(System.Action onComplete = null)
    {
        if (!enableBackFacing || isFacingBack || isTransitioning) return;
        StartCoroutine(TurnToBackCoroutine(onComplete));
    }

    /// <summary>觸發正面轉向協程。</summary>
    public void TurnToFront(System.Action onComplete = null)
    {
        if (!enableBackFacing || !isFacingBack || isTransitioning) return;
        StartCoroutine(TurnToFrontCoroutine(onComplete));
    }

    /// <summary>查詢角色是否正面朝後。</summary>
    public bool IsFacingBack() => isFacingBack;

    private System.Collections.IEnumerator TurnToBackCoroutine(System.Action onComplete)
    {
        isTransitioning = true;
        isFacingBack = true;
        if (anim) anim.SetBool(pFacingBack, true);
        yield return new WaitForSeconds(turnToBackDuration);
        isTransitioning = false;
        onComplete?.Invoke();
    }

    private System.Collections.IEnumerator TurnToFrontCoroutine(System.Action onComplete)
    {
        isTransitioning = true;
        isFacingBack = false;
        if (anim) anim.SetBool(pFacingBack, false);
        yield return new WaitForSeconds(turnToFrontDuration);
        isTransitioning = false;
        onComplete?.Invoke();
    }

    [ContextMenu("重置背面狀態")]
    public void ResetBackFacingState()
    {
        StopAllCoroutines();
        isTransitioning = false;
        isFacingBack = false;
        isMovementLocked = false;
        if (anim) anim.SetBool(pFacingBack, false);
    }

    /// <summary>判斷 Animator 是否正在播放任一睡著狀態。</summary>
    private bool IsInSleepState()
    {
        if (!anim) return false;
        AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
        return info.IsName("sleepStart") || info.IsName("loop") || info.IsName("sleepEnd");
    }
}
