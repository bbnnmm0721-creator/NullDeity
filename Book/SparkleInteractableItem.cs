using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class SparkleInteractableItem : MonoBehaviour
{
    [Header("物品資料")]
    [Tooltip("物品 ID，對應 ItemData 的 id")]
    public int itemID = 101;

    [Header("偵測設定")]
    [Tooltip("玩家進入這個距離內會顯現閃閃")]
    [Range(1f, 500f)]
    public float activationDistance = 3f;

    [Tooltip("偵測更新頻率")]
    [Range(0.1f, 2f)]
    public float detectionFrequency = 0.5f;

    [Header("偵測的效果物件")]
    [Tooltip("閃閃物件，當玩家靠近時會亮")]
    public GameObject sparkleObject;

    [Tooltip("互動提示物件（如按鈕的圖示）")]
    public GameObject interactionHint;

    [Header("閃閃動畫設定")]
    [Tooltip("閃閃大小範圍")]
    public Vector2 scaleRange = new Vector2(0.8f, 1.2f);

    [Tooltip("閃閃動畫速度")]
    [Range(0.5f, 3f)]
    public float sparkleSpeed = 1.5f;

    [Tooltip("閃閃旋轉速度")]
    [Range(0f, 360f)]
    public float rotationSpeed = 45f;

    [Header("🎨 顏色設定")]
    [Tooltip("未拾取時的顏色")]
    public Color normalColor = Color.white;

    [Tooltip("已拾取後的顏色（灰色/暗色）")]
    public Color pickedUpColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);

    [Tooltip("顏色變化速度")]
    [Range(0.1f, 5f)]
    public float colorTransitionSpeed = 2f;

    [Header("音效設定")]
    [Tooltip("發現物品時的音效")]
    public AudioClip discoverySound;

    [Header("調試設定")]
    [Tooltip("顯示調試信息")]
    public bool showDebugInfo = true;

    [Tooltip("顯示偵測範圍")]
    public bool showDetectionRange = true;

    // 內部變數
    private Transform playerTransform;
    private bool playerInRange = false;
    private bool isPickedUp = false;
    private bool sparkleVisible = false;
    private AudioSource audioSource;
    private Coroutine detectionCoroutine;
    private Coroutine sparkleAnimationCoroutine;
    private Coroutine colorTransitionCoroutine;
    private Vector3 originalScale;
    private SpriteRenderer spriteRenderer;
    private bool isInitialized = false;

    void OnEnable()
    {
        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} OnEnable 執行");

        // 🆕 延迟初始化和启动检测
        StartCoroutine(DelayedInitialization());
    }

    void OnDisable()
    {
        StopAllDetectionCoroutines();

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} OnDisable 執行");
    }

    /// <summary>
    /// 🆕 延遲初始化，等待 PlayerMovement.Instance 就緒
    /// </summary>
    IEnumerator DelayedInitialization()
    {
        // 等待下一幀，確保所有物件都已初始化
        yield return null;

        if (!isInitialized)
        {
            InitializeComponents();
            isInitialized = true;
        }

        SetupInitialState();

        // 🆕 再次檢查玩家是否存在
        if (playerTransform == null)
        {
            yield return StartCoroutine(WaitForPlayer());
        }

        StartDetection();
    }

    /// <summary>
    /// 🆕 等待玩家對象就緒
    /// </summary>
    IEnumerator WaitForPlayer()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (playerTransform == null && elapsed < timeout)
        {
            // 嘗試查找玩家
            if (PlayerMovement.Instance != null)
            {
                playerTransform = PlayerMovement.Instance.transform;
                if (showDebugInfo)
                    Debug.Log($"[SparkleItem] {gameObject.name} 找到 PlayerMovement.Instance");
                break;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                if (showDebugInfo)
                    Debug.Log($"[SparkleItem] {gameObject.name} 透過 Tag 找到玩家");
                break;
            }

            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (playerTransform == null)
        {
            Debug.LogWarning($"[SparkleItem] {gameObject.name} 無法找到玩家！");
        }
    }

    void InitializeComponents()
    {
        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 開始初始化組件");

        // 🎯 自動查找 sparkleObject（如果未設置）
        if (sparkleObject == null)
        {
            Transform sparkleChild = transform.Find("SparclePoint");
            if (sparkleChild != null)
            {
                sparkleObject = sparkleChild.gameObject;
                if (showDebugInfo)
                    Debug.Log($"[SparkleItem] {gameObject.name} 自動找到 SparclePoint");
            }
            else
            {
                Debug.LogWarning($"[SparkleItem] {gameObject.name} 找不到 SparclePoint 子對象！");
            }
        }

        // 尋找玩家
        if (PlayerMovement.Instance != null)
        {
            playerTransform = PlayerMovement.Instance.transform;
            if (showDebugInfo)
                Debug.Log($"[SparkleItem] {gameObject.name} 從 Instance 找到玩家");
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                if (showDebugInfo)
                    Debug.Log($"[SparkleItem] {gameObject.name} 從 Tag 找到玩家");
            }
        }

        // 設定 AudioSource
        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;
        }

        // 確保 Collider 是 Trigger
        Collider col = GetComponent<Collider>();
        if (col && !col.isTrigger)
        {
            col.isTrigger = true;
            if (showDebugInfo)
                Debug.Log($"[SparkleItem] {gameObject.name} 的 Collider 已設為 Trigger");
        }

        // 🎨 獲取 SpriteRenderer（從 sparkleObject）
        if (sparkleObject)
        {
            originalScale = sparkleObject.transform.localScale;
            spriteRenderer = sparkleObject.GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
            {
                Debug.LogWarning($"[SparkleItem] {gameObject.name} 的 sparkleObject 沒有 SpriteRenderer！");
            }
            else
            {
                // 設定初始顏色
                spriteRenderer.color = normalColor;
                if (showDebugInfo)
                    Debug.Log($"[SparkleItem] {gameObject.name} SpriteRenderer 顏色設為 {normalColor}");
            }
        }
        else
        {
            Debug.LogError($"[SparkleItem] {gameObject.name} 沒有設置 sparkleObject！");
        }

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 初始化完成");
    }

    void SetupInitialState()
    {
        // 🎯 只有在未拾取時才隱藏
        if (!isPickedUp)
        {
            SetSparkleVisibility(false);
            SetInteractionHintVisibility(false);
        }

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 初始狀態設置完成 - 已拾取:{isPickedUp}, SparkleObject:{sparkleObject != null}");
    }

    void StartDetection()
    {
        // 先停止舊的協程
        StopAllDetectionCoroutines();

        // 只有在未拾取且有玩家時才啟動偵測
        if (playerTransform && !isPickedUp)
        {
            detectionCoroutine = StartCoroutine(DetectionLoop());

            if (showDebugInfo)
                Debug.Log($"[SparkleItem] {gameObject.name} 偵測循環已啟動");
        }
        else
        {
            if (showDebugInfo)
            {
                if (!playerTransform)
                    Debug.LogWarning($"[SparkleItem] {gameObject.name} 無法啟動偵測：找不到玩家");
                if (isPickedUp)
                    Debug.Log($"[SparkleItem] {gameObject.name} 無法啟動偵測：物品已被拾取");
            }
        }
    }

    void StopAllDetectionCoroutines()
    {
        if (detectionCoroutine != null)
        {
            StopCoroutine(detectionCoroutine);
            detectionCoroutine = null;
        }

        if (sparkleAnimationCoroutine != null)
        {
            StopCoroutine(sparkleAnimationCoroutine);
            sparkleAnimationCoroutine = null;
        }

        if (colorTransitionCoroutine != null)
        {
            StopCoroutine(colorTransitionCoroutine);
            colorTransitionCoroutine = null;
        }
    }

    IEnumerator DetectionLoop()
    {
        while (!isPickedUp && playerTransform)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            bool shouldBeVisible = distance <= activationDistance;

            if (shouldBeVisible != sparkleVisible)
            {
                if (shouldBeVisible)
                    ShowSparkle();
                else
                    HideSparkle();
            }

            yield return new WaitForSeconds(detectionFrequency);
        }

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 偵測循環結束");
    }

    void ShowSparkle()
    {
        if (isPickedUp) return;

        sparkleVisible = true;
        SetSparkleVisibility(true);

        PlaySound(discoverySound);

        if (sparkleAnimationCoroutine == null)
            sparkleAnimationCoroutine = StartCoroutine(SparkleAnimation());

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 閃閃已顯現");
    }

    void HideSparkle()
    {
        sparkleVisible = false;
        playerInRange = false;
        SetSparkleVisibility(false);
        SetInteractionHintVisibility(false);

        if (sparkleAnimationCoroutine != null)
        {
            StopCoroutine(sparkleAnimationCoroutine);
            sparkleAnimationCoroutine = null;
        }

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 閃閃已隱藏");
    }

    IEnumerator SparkleAnimation()
    {
        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 開始閃閃動畫");

        while (sparkleVisible && sparkleObject && !isPickedUp)
        {
            float time = Time.time * sparkleSpeed;
            float scale = Mathf.Lerp(scaleRange.x, scaleRange.y,
                (Mathf.Sin(time) + 1f) * 0.5f);

            sparkleObject.transform.localScale = originalScale * scale;

            if (rotationSpeed > 0)
            {
                sparkleObject.transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 閃閃動畫結束");
    }

    void SetSparkleVisibility(bool visible)
    {
        if (sparkleObject)
        {
            sparkleObject.SetActive(visible);

            if (showDebugInfo)
                Debug.Log($"[SparkleItem] {gameObject.name} SparkleObject 設為 {(visible ? "顯示" : "隱藏")}");
        }
    }

    void SetInteractionHintVisibility(bool visible)
    {
        if (interactionHint)
            interactionHint.SetActive(visible);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || isPickedUp) return;

        playerInRange = true;

        if (sparkleVisible)
        {
            SetInteractionHintVisibility(true);

            if (showDebugInfo)
                Debug.Log($"[SparkleItem] 玩家進入 {gameObject.name} 的互動範圍");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || isPickedUp) return;

        playerInRange = false;
        SetInteractionHintVisibility(false);

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] 玩家離開 {gameObject.name} 的互動範圍");
    }

    void PlaySound(AudioClip clip)
    {
        if (clip && audioSource)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (showDetectionRange)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, activationDistance);

            Collider col = GetComponent<Collider>();
            if (col && col.isTrigger)
            {
                Gizmos.color = Color.green;
                if (col is SphereCollider sphere)
                {
                    Gizmos.DrawWireSphere(transform.position, sphere.radius);
                }
                else if (col is BoxCollider box)
                {
                    Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
                    Gizmos.DrawWireCube(Vector3.zero, box.size);
                    Gizmos.matrix = Matrix4x4.identity;
                }
            }
        }
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }

    public bool IsPickedUp() => isPickedUp;
    public bool IsSparkleVisible() => sparkleVisible;
    public bool IsPlayerInRange() => playerInRange;
    public float GetDistanceToPlayer()
    {
        if (playerTransform)
            return Vector3.Distance(transform.position, playerTransform.position);
        return float.MaxValue;
    }

    public void MarkAsPickedUp()
    {
        if (isPickedUp) return;

        isPickedUp = true;

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 已標記為已拾取");

        StopAllDetectionCoroutines();

        if (spriteRenderer != null)
        {
            colorTransitionCoroutine = StartCoroutine(TransitionToPickedUpColor());
        }

        SetInteractionHintVisibility(false);

        if (sparkleObject)
        {
            sparkleObject.transform.localScale = originalScale;
        }
    }

    IEnumerator TransitionToPickedUpColor()
    {
        if (spriteRenderer == null) yield break;

        Color startColor = spriteRenderer.color;
        float elapsed = 0f;
        float duration = 1f / colorTransitionSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            spriteRenderer.color = Color.Lerp(startColor, pickedUpColor, t);

            yield return null;
        }

        spriteRenderer.color = pickedUpColor;

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 顏色變更完成");
    }

    [ContextMenu("重置物品狀態")]
    public void ResetItemState()
    {
        isPickedUp = false;
        sparkleVisible = false;
        playerInRange = false;

        SetSparkleVisibility(false);
        SetInteractionHintVisibility(false);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }

        if (sparkleObject)
        {
            sparkleObject.transform.localScale = originalScale;
        }

        StartDetection();

        if (showDebugInfo)
            Debug.Log($"[SparkleItem] {gameObject.name} 狀態已重置");
    }

    /// <summary>
    /// 🆕 手動強制啟動偵測（供外部調用）
    /// </summary>
    [ContextMenu("強制啟動偵測")]
    public void ForceStartDetection()
    {
        Debug.Log($"[SparkleItem] {gameObject.name} 手動強制啟動偵測");
        StartCoroutine(DelayedInitialization());
    }
}
