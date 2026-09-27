using UnityEngine;
using System.Collections;
using PixelCrushers.DialogueSystem;

public class DialogueHintAnimator : MonoBehaviour
{
    [Header("提示物件設定")]
    [Tooltip("DialogueHint 子物件的名稱")]
    public string hintObjectName = "DialogueHint";

    [Header("動畫設定")]
    [Tooltip("提示動畫的 Sprite 序列")]
    public Sprite[] animationSprites;

    [Tooltip("動畫播放速度（每秒幾幀）")]
    [Range(1f, 30f)]
    public float animationSpeed = 8f;

    [Tooltip("是否循環播放")]
    public bool loopAnimation = true;

    [Header("觸發設定")]
    [Tooltip("觸發提示的 Layer Mask（通常是 Player Layer）")]
    public LayerMask triggerLayers = -1;

    [Tooltip("觸發提示的 Tag（留空表示任何 Tag）")]
    public string triggerTag = "Player";

    [Header("對話條目檢查")]
    [Tooltip("啟用對話條目檢查（類似 Skip If No Valid Entries）")]
    public bool skipIfNoValidEntries = false;

    [Tooltip("對話名稱（留空將自動從 DialogueSystemTrigger 獲取）")]
    [ConversationPopup(true)]
    public string conversationTitle = string.Empty;

    [Tooltip("條件檢查間隔（秒）")]
    [Range(0.1f, 5f)]
    public float conditionCheckInterval = 0.5f;

    [Header("對話控制")]
    [Tooltip("對話期間自動隱藏提示")]
    public bool hideOnConversation = true;

    [Tooltip("對話結束後自動恢復提示")]
    public bool restoreAfterConversation = true;

    [Header("視覺效果")]
    [Tooltip("浮動效果強度")]
    [Range(0f, 2f)]
    public float floatStrength = 0.5f;

    [Tooltip("浮動速度")]
    [Range(0.5f, 5f)]
    public float floatSpeed = 2f;

    [Tooltip("縮放脈動效果")]
    [Range(0f, 0.5f)]
    public float scaleEffect = 0.1f;

    [Header("顏色效果")]
    [Tooltip("是否啟用顏色閃爍")]
    public bool enableColorFlash = true;

    [Tooltip("閃爍顏色")]
    public Color flashColor = Color.white;

    [Tooltip("閃爍速度")]
    [Range(0.5f, 5f)]
    public float flashSpeed = 1.5f;

    [Header("音效設定")]
    [Tooltip("提示出現時的音效")]
    public AudioClip appearSound;

    [Tooltip("提示消失時的音效")]
    public AudioClip disappearSound;

    [Header("調試設定")]
    [Tooltip("顯示調試訊息")]
    public bool showDebugInfo = true;

    private Transform hintTransform;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private Usable usableComponent;

    private bool isAnimating = false;
    private bool playerInRange = false;
    private bool conversationActive = false;
    private bool wasAnimatingBeforeConversation = false;
    private bool hasValidEntries = false;

    private int currentSpriteIndex = 0;
    private float animationTimer = 0f;

    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Color originalColor;

    private Coroutine animationCoroutine;
    private Coroutine effectsCoroutine;
    private Coroutine validityCheckCoroutine;

    private Transform lastKnownPlayer;

    void Start()
    {
        InitializeComponents();
        SetupInitialState();
        RegisterDialogueEvents();
        GetConversationTitle();

        if (skipIfNoValidEntries)
        {
            StartValidityMonitoring();
        }
    }

    void InitializeComponents()
    {
        hintTransform = transform.Find(hintObjectName);
        if (hintTransform == null)
        {
            Debug.LogError($"[DialogueHint] 找不到子物件 '{hintObjectName}'！");
            return;
        }

        spriteRenderer = hintTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[DialogueHint] {hintObjectName} 沒有 SpriteRenderer 組件！");
            return;
        }

        usableComponent = GetComponent<Usable>();

        Collider npcCollider = GetComponent<Collider>();
        if (npcCollider && !npcCollider.isTrigger)
        {
            Debug.LogWarning($"[DialogueHint] {gameObject.name} 的 Collider 不是 Trigger！請啟用 Is Trigger 選項");
        }

        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
        }

        originalPosition = hintTransform.localPosition;
        originalScale = hintTransform.localScale;
        originalColor = spriteRenderer.color;

        if (showDebugInfo)
            Debug.Log($"[DialogueHint] {gameObject.name} 初始化完成，提示物件: {hintTransform.name}");
    }

    void SetupInitialState()
    {
        if (!hintTransform || !spriteRenderer) return;

        spriteRenderer.enabled = false;

        if (animationSprites != null && animationSprites.Length > 0)
        {
            spriteRenderer.sprite = animationSprites[0];
        }
        else
        {
            Debug.LogWarning("[DialogueHint] 沒有設定動畫 Sprite！");
        }
    }

    void GetConversationTitle()
    {
        if (!string.IsNullOrEmpty(conversationTitle))
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {gameObject.name} 使用手動設定的對話名稱: {conversationTitle}");
            return;
        }

        DialogueSystemTrigger dsTrigger = GetComponent<DialogueSystemTrigger>();
        if (dsTrigger != null && !string.IsNullOrEmpty(dsTrigger.conversation))
        {
            conversationTitle = dsTrigger.conversation;
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {gameObject.name} 從 DialogueSystemTrigger 獲取對話名稱: {conversationTitle}");
            return;
        }

        ConversationStarter conversationStarter = GetComponent<ConversationStarter>();
        if (conversationStarter != null && !string.IsNullOrEmpty(conversationStarter.conversation))
        {
            conversationTitle = conversationStarter.conversation;
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {gameObject.name} 從 ConversationStarter 獲取對話名稱: {conversationTitle}");
            return;
        }

        if (string.IsNullOrEmpty(conversationTitle) && skipIfNoValidEntries)
        {
            Debug.LogWarning($"[DialogueHint] {gameObject.name} 啟用了 Skip If No Valid Entries，但找不到對話名稱！請手動設置 Conversation Title。");
        }
    }

    #region 對話條目有效性檢查

    void StartValidityMonitoring()
    {
        if (string.IsNullOrEmpty(conversationTitle))
        {
            hasValidEntries = true;
            return;
        }

        if (validityCheckCoroutine != null)
        {
            StopCoroutine(validityCheckCoroutine);
        }
        validityCheckCoroutine = StartCoroutine(MonitorConversationValidity());
    }

    void StopValidityMonitoring()
    {
        if (validityCheckCoroutine != null)
        {
            StopCoroutine(validityCheckCoroutine);
            validityCheckCoroutine = null;
        }
    }

    IEnumerator MonitorConversationValidity()
    {
        while (skipIfNoValidEntries)
        {
            bool previousValidity = hasValidEntries;
            hasValidEntries = CheckConversationValidity();

            if (previousValidity != hasValidEntries)
            {
                if (showDebugInfo)
                    Debug.Log($"[DialogueHint] {gameObject.name} 對話有效性變化: {previousValidity} -> {hasValidEntries}");

                if (!hasValidEntries && isAnimating)
                {
                    HideHint();
                }
                else if (hasValidEntries && playerInRange && !conversationActive)
                {
                    ShowHint();
                }
            }

            yield return new WaitForSeconds(conditionCheckInterval);
        }
    }

    bool CheckConversationValidity()
    {
        if (!skipIfNoValidEntries || string.IsNullOrEmpty(conversationTitle))
        {
            return true;
        }

        Transform actor = lastKnownPlayer != null ? lastKnownPlayer : transform;
        Transform conversant = transform;

        bool hasValid = DialogueManager.ConversationHasValidEntry(conversationTitle, actor, conversant);

        if (showDebugInfo && Time.frameCount % 300 == 0)
        {
            Debug.Log($"[DialogueHint] {gameObject.name} 對話 '{conversationTitle}' 有效性: {hasValid}");
        }

        return hasValid;
    }

    #endregion

    #region Dialogue System 事件處理

    void RegisterDialogueEvents()
    {
        if (!hideOnConversation) return;

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted += OnConversationStarted;
            DialogueManager.instance.conversationEnded += OnConversationEnded;

            if (showDebugInfo)
                Debug.Log("[DialogueHint] 已註冊對話事件監聽");
        }
        else
        {
            if (showDebugInfo)
                Debug.LogWarning("[DialogueHint] DialogueManager.instance 為空，無法註冊對話事件");
        }
    }

    void UnregisterDialogueEvents()
    {
        if (!hideOnConversation) return;

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted -= OnConversationStarted;
            DialogueManager.instance.conversationEnded -= OnConversationEnded;

            if (showDebugInfo)
                Debug.Log("[DialogueHint] 已取消對話事件監聽");
        }
    }

    void OnConversationStarted(Transform actor)
    {
        conversationActive = true;
        wasAnimatingBeforeConversation = isAnimating;

        if (showDebugInfo)
            Debug.Log($"[DialogueHint] 對話開始，強制隱藏提示（之前狀態: {(wasAnimatingBeforeConversation ? "顯示" : "隱藏")}）");

        ForceHideForConversation();
    }

    void OnConversationEnded(Transform actor)
    {
        conversationActive = false;

        if (showDebugInfo)
            Debug.Log($"[DialogueHint] 對話結束，恢復提示狀態（玩家在範圍內: {playerInRange}，之前狀態: {(wasAnimatingBeforeConversation ? "顯示" : "隱藏")}）");

        if (!gameObject.activeInHierarchy)
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {gameObject.name} 已被禁用，跳過恢復提示");
            return;
        }

        if (restoreAfterConversation && playerInRange)
        {
            ShowHint();
        }
    }

    void ForceHideForConversation()
    {
        if (!isAnimating) return;

        isAnimating = false;

        if (spriteRenderer)
            spriteRenderer.enabled = false;

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        if (effectsCoroutine != null)
        {
            StopCoroutine(effectsCoroutine);
            effectsCoroutine = null;
        }

        ResetTransform();

        if (showDebugInfo)
            Debug.Log($"[DialogueHint] 因對話開始而強制隱藏提示");
    }

    #endregion

    #region Trigger 事件處理

    void OnTriggerEnter(Collider other)
    {
        if (ShouldTrigger(other))
        {
            playerInRange = true;
            lastKnownPlayer = other.transform;

            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {other.name} 進入觸發範圍");

            if (!conversationActive)
            {
                ShowHint();
            }
            else
            {
                if (showDebugInfo)
                    Debug.Log("[DialogueHint] 對話進行中，不顯示提示");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (ShouldTrigger(other))
        {
            playerInRange = false;

            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {other.name} 離開觸發範圍");

            HideHint();
        }
    }

    bool ShouldTrigger(Collider other)
    {
        if (!usableComponent || !usableComponent.enabled)
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] Usable 組件未啟用，忽略 {other.name}");
            return false;
        }

        if (triggerLayers != -1 && !IsInLayerMask(other.gameObject, triggerLayers))
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {other.name} 不在指定 Layer (當前: {LayerMask.LayerToName(other.gameObject.layer)})");
            return false;
        }

        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag))
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {other.name} Tag 不符合，需要: {triggerTag}，實際: {other.tag}");
            return false;
        }

        return true;
    }

    bool IsInLayerMask(GameObject obj, LayerMask layerMask)
    {
        return (layerMask.value & (1 << obj.layer)) > 0;
    }

    #endregion

    #region 動畫控制

    void ShowHint()
    {
        if (!gameObject.activeInHierarchy)
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {gameObject.name} 未激活，無法顯示提示");
            return;
        }

        if (conversationActive && hideOnConversation)
        {
            if (showDebugInfo)
                Debug.Log("[DialogueHint] 對話進行中，拒絕顯示提示");
            return;
        }

        if (skipIfNoValidEntries && !hasValidEntries)
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {gameObject.name} 對話無有效條目，無法顯示提示");
            return;
        }

        if (isAnimating || !hintTransform || !spriteRenderer) return;

        isAnimating = true;
        spriteRenderer.enabled = true;
        PlaySound(appearSound);

        if (animationCoroutine == null && animationSprites != null && animationSprites.Length > 0)
            animationCoroutine = StartCoroutine(PlayAnimation());

        if (effectsCoroutine == null)
            effectsCoroutine = StartCoroutine(PlayEffects());

        if (showDebugInfo)
            Debug.Log($"[DialogueHint] {gameObject.name} 提示動畫已啟動");
    }

    void HideHint()
    {
        if (!isAnimating || !hintTransform || !spriteRenderer) return;

        isAnimating = false;
        spriteRenderer.enabled = false;
        PlaySound(disappearSound);

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        if (effectsCoroutine != null)
        {
            StopCoroutine(effectsCoroutine);
            effectsCoroutine = null;
        }

        ResetTransform();

        if (showDebugInfo)
            Debug.Log($"[DialogueHint] {gameObject.name} 提示動畫已停止");
    }

    IEnumerator PlayAnimation()
    {
        while (isAnimating && animationSprites != null && animationSprites.Length > 0)
        {
            spriteRenderer.sprite = animationSprites[currentSpriteIndex];

            animationTimer += Time.deltaTime;
            if (animationTimer >= 1f / animationSpeed)
            {
                animationTimer = 0f;
                currentSpriteIndex++;

                if (currentSpriteIndex >= animationSprites.Length)
                {
                    if (loopAnimation)
                    {
                        currentSpriteIndex = 0;
                    }
                    else
                    {
                        currentSpriteIndex = animationSprites.Length - 1;
                        break;
                    }
                }
            }

            yield return null;
        }
    }

    IEnumerator PlayEffects()
    {
        while (isAnimating && hintTransform)
        {
            float time = Time.time;

            if (floatStrength > 0)
            {
                Vector3 floatOffset = Vector3.up * Mathf.Sin(time * floatSpeed) * floatStrength;
                hintTransform.localPosition = originalPosition + floatOffset;
            }

            if (scaleEffect > 0)
            {
                float scale = 1f + Mathf.Sin(time * floatSpeed * 1.5f) * scaleEffect;
                hintTransform.localScale = originalScale * scale;
            }

            if (enableColorFlash && spriteRenderer)
            {
                float flash = (Mathf.Sin(time * flashSpeed) + 1f) * 0.5f;
                spriteRenderer.color = Color.Lerp(originalColor, flashColor, flash * 0.3f);
            }

            yield return null;
        }
    }

    void ResetTransform()
    {
        if (!hintTransform) return;

        hintTransform.localPosition = originalPosition;
        hintTransform.localScale = originalScale;
        if (spriteRenderer)
            spriteRenderer.color = originalColor;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip && audioSource)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    #endregion

    #region 公開方法

    public void ForceShow()
    {
        if (!gameObject.activeInHierarchy)
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {gameObject.name} 未激活，無法強制顯示");
            return;
        }

        if (!hintTransform || !spriteRenderer) return;

        isAnimating = true;
        spriteRenderer.enabled = true;
        PlaySound(appearSound);

        if (animationCoroutine == null && animationSprites != null && animationSprites.Length > 0)
            animationCoroutine = StartCoroutine(PlayAnimation());

        if (effectsCoroutine == null)
            effectsCoroutine = StartCoroutine(PlayEffects());

        if (showDebugInfo)
            Debug.Log($"[DialogueHint] {gameObject.name} 強制顯示提示");
    }

    public void ForceHide()
    {
        HideHint();
    }

    public bool IsAnimating()
    {
        return isAnimating;
    }

    public bool IsConversationActive()
    {
        return conversationActive;
    }

    public bool HasValidEntries()
    {
        return hasValidEntries;
    }

    public void RefreshValidity()
    {
        if (skipIfNoValidEntries)
        {
            hasValidEntries = CheckConversationValidity();
            if (showDebugInfo)
                Debug.Log($"[DialogueHint] {gameObject.name} 手動刷新有效性: {hasValidEntries}");
        }
    }

    [ContextMenu("強制顯示提示")]
    public void TestShow()
    {
        ForceShow();
    }

    [ContextMenu("強制隱藏提示")]
    public void TestHide()
    {
        ForceHide();
    }

    [ContextMenu("檢查對話有效性")]
    public void TestCheckValidity()
    {
        bool result = CheckConversationValidity();
        Debug.Log($"[DialogueHint] {gameObject.name} 對話 '{conversationTitle}' 有效性: {result}");
    }

    [ContextMenu("重新獲取對話名稱")]
    public void RefreshConversationTitle()
    {
        conversationTitle = string.Empty;
        GetConversationTitle();
    }

    [ContextMenu("重置提示狀態")]
    public void ResetHintState()
    {
        StopAllCoroutines();
        isAnimating = false;
        playerInRange = false;

        if (spriteRenderer)
            spriteRenderer.enabled = false;

        ResetTransform();

        if (skipIfNoValidEntries)
        {
            StartValidityMonitoring();
        }

        if (showDebugInfo)
            Debug.Log($"[DialogueHint] {gameObject.name} 狀態已重置");
    }

    #endregion

    #region Unity 生命週期

    void OnEnable()
    {
        if (hideOnConversation)
        {
            StartCoroutine(DelayedEventRegistration());
        }

        if (skipIfNoValidEntries)
        {
            StartValidityMonitoring();
        }
    }

    void OnDisable()
    {
        UnregisterDialogueEvents();
        StopValidityMonitoring();
    }

    void OnDestroy()
    {
        UnregisterDialogueEvents();
        StopAllCoroutines();
    }

    IEnumerator DelayedEventRegistration()
    {
        yield return null;

        if (DialogueManager.instance == null)
        {
            while (DialogueManager.instance == null)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }

        RegisterDialogueEvents();
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        Collider npcCollider = GetComponent<Collider>();
        if (npcCollider && npcCollider.isTrigger)
        {
            Gizmos.color = conversationActive ? Color.red : (hasValidEntries ? Color.green : Color.yellow);
            Vector3 center = npcCollider.bounds.center;
            Vector3 size = npcCollider.bounds.size;

            if (npcCollider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(center, sphere.radius * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z));
            }
            else if (npcCollider is CapsuleCollider)
            {
                Gizmos.DrawWireCube(center, size);
            }
            else if (npcCollider is BoxCollider)
            {
                Gizmos.DrawWireCube(center, size);
            }
        }

        if (hintTransform)
        {
            Gizmos.color = isAnimating ? Color.yellow : Color.gray;
            Gizmos.DrawWireCube(hintTransform.position, Vector3.one * 0.5f);
        }
    }
}
