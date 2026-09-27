using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class WorldItem : MonoBehaviour, IPointerClickHandler
{
    [Header("資料")]
    public int itemID;

    [Header("彈窗顯示設定")]
    [Tooltip("是否根據 ItemData 的 Page 設定來顯示彈窗")]
    public bool usePageBasedPopup = true;
    [Tooltip("當前所在的 Page（用於判斷是否顯示彈窗）")]
    public BookTabs currentPage = BookTabs.Page1;

    [Tooltip("等待 Dialogue System 對話結束後才顯示彈窗")]
    public bool waitForDialogueEnd = true;

    [Header("提示物件")]
    public GameObject exclaim;

    [Tooltip("互動提示 UI（如 E 鍵圖示）")]
    public GameObject interactPrompt;

    [Tooltip("是否顯示互動提示")]
    public bool showInteractPrompt = true;

    [Header("使用後設定")]
    public bool destroyAfterUse = true;

    [Header("重複觸發設定")]
    public bool allowRepeatTrigger = false;
    public float repeatCooldown = 10f;

    [Header("自動拾取設定")]
    [Tooltip("當物件被 SetActive(true) 時自動給予物品")]
    public bool autoPickupOnActivate = false;
    [Tooltip("自動拾取後延遲多久銷毀物件")]
    public float autoDestroyDelay = 1f;

    [Header("Bark 對話設定")]
    public bool enableBark = false;
    [Tooltip("等待彈窗關閉後才開始 Bark")]
    public bool waitForPopupClose = true;
    [Tooltip("Bark 對話的最大輪數（防止無限循環）")]
    public int maxBarkRounds = 3;

    [Header("角色行為控制")]
    public bool triggerBackFacing = false;
    public bool lockMovementDuringBark = true;
    public float backFacingDelay = 0.5f;
    public float frontFacingDelay = 0.1f;

    bool playerInRange;
    bool picked;
    bool isProcessing;
    float lastTriggerTime = -999f;
    bool hasAutoPickedUp;

    private PlayerInputActions inputActions;
    private static HashSet<int> registeredItemIDs = new HashSet<int>();
    private static WorldItem currentBarkingItem = null;

    private static List<PixelCrushers.DialogueSystem.DialogueActor> sceneActors = new List<PixelCrushers.DialogueSystem.DialogueActor>();
    private static bool actorsCached = false;

    void Awake()
    {
        inputActions = new PlayerInputActions();

        if (!registeredItemIDs.Contains(itemID))
        {
            registeredItemIDs.Add(itemID);
        }
    }

    void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Enable();
        }

        if (autoPickupOnActivate && !picked && !hasAutoPickedUp)
        {
            Debug.Log($"[WorldItem] 物件啟用，觸發自動拾取: {gameObject.name}, itemID: {itemID}");
            hasAutoPickedUp = true;
            Invoke(nameof(GiveItemDirectly), 0.1f);
        }
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }

        if (currentBarkingItem == this)
        {
            CleanupBarkState();
        }
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Dispose();
        }

        if (currentBarkingItem == this)
        {
            CleanupBarkState();
        }
        if (DialogueInputPriority.HasInstance)
            DialogueInputPriority.SetBarkActive(false);
    }

    void Reset()
    {
        var col2D = GetComponent<Collider2D>();
        if (col2D) col2D.isTrigger = true;

        var col3D = GetComponent<Collider>();
        if (col3D) col3D.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (picked && !allowRepeatTrigger) return;

        Debug.Log($"[WorldItem] 玩家進入觸發範圍，itemID: {itemID}");

        playerInRange = true;

        if (exclaim) exclaim.SetActive(true);

        if (showInteractPrompt && interactPrompt)
            interactPrompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log($"[WorldItem] 玩家離開觸發範圍，itemID: {itemID}");

        playerInRange = false;

        if (exclaim) exclaim.SetActive(false);

        if (interactPrompt)
            interactPrompt.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (picked && !allowRepeatTrigger) return;

        Debug.Log($"[WorldItem] 玩家進入觸發範圍（3D），itemID: {itemID}");

        playerInRange = true;

        if (exclaim) exclaim.SetActive(true);

        if (showInteractPrompt && interactPrompt)
            interactPrompt.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log($"[WorldItem] 玩家離開觸發範圍（3D），itemID: {itemID}");

        playerInRange = false;

        if (exclaim) exclaim.SetActive(false);

        if (interactPrompt)
            interactPrompt.SetActive(false);
    }

    void Update()
    {
        if (DialogueInputPriority.ShouldBlockWorldItemInput)
            return;

        if (playerInRange && CanTrigger() && inputActions.Player.Interact.WasPressedThisFrame())
        {
            Debug.Log($"[WorldItem] 偵測到互動鍵按下，itemID: {itemID}");
            StartCoroutine(PickupSequence());
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (DialogueInputPriority.ShouldBlockWorldItemInput)
            return;

        if (playerInRange && CanTrigger())
        {
            Debug.Log($"[WorldItem] 偵測到滑鼠點擊，itemID: {itemID}");
            StartCoroutine(PickupSequence());
        }
    }

    bool CanTrigger()
    {
        if (!allowRepeatTrigger && picked) return false;

        if (allowRepeatTrigger && picked)
            return Time.time - lastTriggerTime >= repeatCooldown;

        return !isProcessing;
    }

    public void GiveItemDirectly()
    {
        Debug.Log($"[WorldItem] ========== 開始執行 GiveItemDirectly ==========");
        Debug.Log($"[WorldItem] 物件名稱: {gameObject.name}");
        Debug.Log($"[WorldItem] itemID: {itemID}");

        PixelCrushers.DialogueSystem.DialogueLua.SetVariable("LastPickedItem", itemID);
        PixelCrushers.DialogueSystem.DialogueLua.SetVariable($"Item_{itemID}", true);
        Debug.Log($"[WorldItem] ✓ Lua 變量已設定");

        if (InventoryManager.Inst != null)
        {
            InventoryManager.Inst.Add(itemID);
            Debug.Log($"[WorldItem] ✓ 物品已加入背包");
        }

        bool shouldShow = ShouldShowPopup();
        Debug.Log($"[WorldItem] ShouldShowPopup 判斷結果: {shouldShow}");

        if (shouldShow)
        {
            if (waitForDialogueEnd)
            {
                StartCoroutine(ShowPopupAfterDialogueEnd(itemID));
            }
            else
            {
                ItemPopup.Show(itemID);
                Debug.Log($"[WorldItem] ✓ 彈窗已顯示");
            }
        }

        picked = true;

        Debug.Log($"[WorldItem] ========== GiveItemDirectly 執行完畢 ==========");

        if (autoPickupOnActivate && destroyAfterUse)
        {
            Debug.Log($"[WorldItem] 將在 {autoDestroyDelay} 秒後銷毀物件");
            Destroy(gameObject, autoDestroyDelay);
        }
    }

    IEnumerator PickupSequence()
    {
        if (isProcessing) yield break;
        isProcessing = true;
        picked = true;
        lastTriggerTime = Time.time;

        PixelCrushers.DialogueSystem.DialogueLua.SetVariable("LastPickedItem", itemID);
        PixelCrushers.DialogueSystem.DialogueLua.SetVariable($"Item_{itemID}", true);

        InventoryManager.Inst.Add(itemID);

        bool showedPopup = false;
        if (ShouldShowPopup())
        {
            if (waitForDialogueEnd)
            {
                yield return StartCoroutine(ShowPopupAfterDialogueEnd(itemID));
            }
            else
            {
                ItemPopup.Show(itemID);
            }
            showedPopup = true;
            Debug.Log($"[WorldItem] 彈窗已顯示，itemID: {itemID}");
        }

        if (exclaim) exclaim.SetActive(false);
        if (interactPrompt) interactPrompt.SetActive(false);

        if (enableBark)
        {
            if (showedPopup && waitForPopupClose)
            {
                Debug.Log($"[WorldItem] 等待彈窗關閉...");
                yield return StartCoroutine(WaitForPopupClose());
            }

            yield return StartCoroutine(BarkSequence());
        }

        var sparkle = GetComponent<SparkleInteractableItem>();
        if (sparkle != null) sparkle.MarkAsPickedUp();

        if (destroyAfterUse)
        {
            Destroy(gameObject);
        }
        else
        {
            if (allowRepeatTrigger)
            {
                isProcessing = false;
                if (exclaim) exclaim.SetActive(false);
                if (interactPrompt) interactPrompt.SetActive(false);
            }
            else
            {
                var col2D = GetComponent<Collider2D>();
                if (col2D) col2D.enabled = false;

                var col3D = GetComponent<Collider>();
                if (col3D) col3D.enabled = false;
            }
        }
    }

    IEnumerator ShowPopupAfterDialogueEnd(int id)
    {
        if (DialogueInputPriority.IsConversationActive)
        {
            Debug.Log($"[WorldItem] 偵測到 Dialogue System 對話進行中，等待對話結束...");

            while (DialogueInputPriority.IsConversationActive)
            {
                yield return null;
            }

            Debug.Log($"[WorldItem] ✓ 對話已結束");
            yield return new WaitForSeconds(0.2f);
        }

        ItemPopup.Show(id);
        Debug.Log($"[WorldItem] ✓ 彈窗已顯示，itemID: {id}");
    }

    IEnumerator WaitForPopupClose()
    {
        GameObject popupObj = GameObject.Find("ItemPopupPanel(Clone)");
        if (popupObj == null)
        {
            popupObj = GameObject.Find("ItemPopupPanel");
        }

        if (popupObj != null)
        {
            Debug.Log($"[WorldItem] 找到彈窗物件，等待關閉");

            while (popupObj != null && popupObj.activeSelf)
            {
                yield return null;
            }

            Debug.Log($"[WorldItem] 彈窗已關閉");
        }

        yield return new WaitForSeconds(0.3f);
    }

    void CacheSceneActors()
    {
        sceneActors.Clear();

        var actors = FindObjectsOfType<PixelCrushers.DialogueSystem.DialogueActor>();

        foreach (var actor in actors)
        {
            if (actor.barkUISettings.barkUI != null)
            {
                sceneActors.Add(actor);
                Debug.Log($"[WorldItem] 找到角色: {actor.actor}，GameObject: {actor.gameObject.name}");
            }
        }

        actorsCached = true;
        Debug.Log($"[WorldItem] 共找到 {sceneActors.Count} 個有 Bark UI 的角色");
    }

    GameObject GetActorGameObject(string actorName)
    {
        foreach (var actor in sceneActors)
        {
            if (actor.actor == actorName)
            {
                Debug.Log($"[WorldItem] 從緩存中找到角色: {actorName} → {actor.gameObject.name}");
                return actor.gameObject;
            }
        }

        var allActors = FindObjectsOfType<PixelCrushers.DialogueSystem.DialogueActor>();
        foreach (var actor in allActors)
        {
            if (actor.actor == actorName)
            {
                Debug.Log($"[WorldItem] 實時找到角色: {actorName} → {actor.gameObject.name}");

                if (!sceneActors.Contains(actor))
                {
                    sceneActors.Add(actor);
                }

                return actor.gameObject;
            }
        }

        Debug.LogWarning($"[WorldItem] 找不到角色: {actorName}，將使用主角代替");
        return null;
    }

    string GetCurrentSpeaker(int barkStep)
    {
        var conversation = PixelCrushers.DialogueSystem.DialogueManager.masterDatabase.GetConversation("ItemPickupReactions");

        if (conversation == null)
        {
            Debug.LogWarning("[WorldItem] 找不到 ItemPickupReactions 會話");
            return "諾恩";
        }

        foreach (var entry in conversation.dialogueEntries)
        {
            if (entry.conditionsString.Contains($"BarkStep_Item{itemID}") &&
                entry.conditionsString.Contains($"== {barkStep}"))
            {
                var actor = PixelCrushers.DialogueSystem.DialogueManager.masterDatabase.GetActor(entry.ActorID);

                if (actor != null)
                {
                    Debug.Log($"[WorldItem] BarkStep {barkStep} 對應的 Actor: {actor.Name}");
                    return actor.Name;
                }
            }
        }

        return "諾恩";
    }

    IEnumerator BarkSequence()
    {
        if (DialogueInputPriority.IsConversationActive)
        {
            Debug.Log($"[WorldItem] 檢測到對話正在進行，跳過 Bark");
            yield break;
        }

        string playingVar = $"BarkPlaying_Item{itemID}";
        string barkStepVar = $"BarkStep_Item{itemID}";

        if (currentBarkingItem != null && currentBarkingItem != this)
        {
            Debug.Log($"[WorldItem] 偵測到其他物品正在播放 Bark（Item {currentBarkingItem.itemID}），強制停止");
            currentBarkingItem.ForceStopBark();
        }

        if (PixelCrushers.DialogueSystem.DialogueLua.GetVariable(playingVar).asBool)
        {
            Debug.Log($"[WorldItem] Item {itemID} 的 Bark 已在播放，跳過");
            yield break;
        }

        currentBarkingItem = this;

        DialogueInputPriority.SetBarkActive(true);

        CacheSceneActors();

        ResetAllOtherBarkSteps();

        PixelCrushers.DialogueSystem.DialogueLua.SetVariable(barkStepVar, 0);
        PixelCrushers.DialogueSystem.DialogueLua.SetVariable(playingVar, true);

        Debug.Log($"[WorldItem] ========== 開始 Bark 序列 ==========");
        Debug.Log($"[WorldItem] itemID: {itemID}");
        Debug.Log($"[WorldItem] maxBarkRounds: {maxBarkRounds}");
        Debug.Log($"[WorldItem] 初始 BarkStep: 0");

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError($"[WorldItem] 找不到 Player！");
            CleanupBarkState();
            yield break;
        }

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm == null)
        {
            Debug.LogError($"[WorldItem] Player 沒有 PlayerMovement 組件！");
            CleanupBarkState();
            yield break;
        }

        if (triggerBackFacing)
        {
            yield return new WaitForSeconds(backFacingDelay);
            bool turnDone = false;
            pm.TurnToBack(() => turnDone = true);
            while (!turnDone) yield return null;
        }

        if (lockMovementDuringBark)
            pm.SetMovementLocked(true, $"Item {itemID}");

        yield return new WaitForSeconds(0.5f);

        int previousStep = -1;
        for (int i = 0; i < maxBarkRounds; i++)
        {
            if (currentBarkingItem != this)
            {
                Debug.Log($"[WorldItem] Bark 被其他物品中斷");
                break;
            }

            if (DialogueInputPriority.IsConversationActive)
            {
                Debug.Log($"[WorldItem] 檢測到對話開始，中斷 Bark");
                break;
            }

            int currentStep = PixelCrushers.DialogueSystem.DialogueLua.GetVariable(barkStepVar).asInt;

            Debug.Log($"[WorldItem] Bark 輪次 {i + 1}/{maxBarkRounds}");
            Debug.Log($"[WorldItem] 當前 BarkStep: {currentStep}");

            if (i > 0 && currentStep == previousStep)
            {
                Debug.Log($"[WorldItem] BarkStep 未增加（{previousStep} → {currentStep}），對話結束");
                break;
            }

            previousStep = currentStep;

            string currentSpeaker = GetCurrentSpeaker(currentStep);
            Debug.Log($"[WorldItem] 當前說話者: {currentSpeaker}");

            GameObject speakerObject = GetActorGameObject(currentSpeaker);

            Transform barker = (speakerObject != null) ? speakerObject.transform : player.transform;

            Debug.Log($"[WorldItem] 使用 {barker.gameObject.name} 的 Bark UI");

            PixelCrushers.DialogueSystem.DialogueManager.Bark("ItemPickupReactions", barker);

            yield return StartCoroutine(WaitForInteractOrInterrupt());

            if (currentBarkingItem != this)
            {
                Debug.Log($"[WorldItem] 等待互動鍵時被中斷");
                break;
            }

            if (DialogueInputPriority.IsConversationActive)
            {
                Debug.Log($"[WorldItem] 等待互動鍵時檢測到對話開始，中斷 Bark");
                break;
            }

            int newStep = PixelCrushers.DialogueSystem.DialogueLua.GetVariable(barkStepVar).asInt;
            Debug.Log($"[WorldItem] Bark 後 BarkStep: {newStep}");

            HideAllBarkUIs();

            if (newStep >= maxBarkRounds)
            {
                Debug.Log($"[WorldItem] 達到最大步數，結束 Bark");
                break;
            }

            yield return new WaitForSeconds(0.3f);
        }

        if (triggerBackFacing)
        {
            yield return new WaitForSeconds(frontFacingDelay);
            bool turnDone = false;
            pm.TurnToFront(() => turnDone = true);
            while (!turnDone) yield return null;
        }

        if (lockMovementDuringBark)
            pm.SetMovementLocked(false, $"Item {itemID}");

        HideAllBarkUIs();
        yield return new WaitForSeconds(0.1f);

        DialogueInputPriority.SetBarkActive(false);

        CleanupBarkState();

        Debug.Log($"[WorldItem] ========== Bark 序列結束 ==========");
    }

    void ResetAllOtherBarkSteps()
    {
        Debug.Log($"[WorldItem] 開始重置其他物品的 BarkStep...");

        foreach (int id in registeredItemIDs)
        {
            if (id != itemID)
            {
                string otherBarkStepVar = $"BarkStep_Item{id}";
                int oldValue = PixelCrushers.DialogueSystem.DialogueLua.GetVariable(otherBarkStepVar).asInt;

                PixelCrushers.DialogueSystem.DialogueLua.SetVariable(otherBarkStepVar, 999);

                Debug.Log($"[WorldItem] 重置 Item {id} 的 BarkStep: {oldValue} → 999");
            }
        }

        Debug.Log($"[WorldItem] ✓ 其他物品的 BarkStep 已重置");
    }

    IEnumerator WaitForInteractOrInterrupt()
    {
        yield return new WaitForSeconds(0.1f);

        while (inputActions.Player.Interact.IsPressed())
        {
            if (currentBarkingItem != this) yield break;
            if (DialogueInputPriority.IsConversationActive) yield break;
            yield return null;
        }

        while (!inputActions.Player.Interact.WasPressedThisFrame())
        {
            if (currentBarkingItem != this) yield break;
            if (DialogueInputPriority.IsConversationActive) yield break;
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);
    }

    public void ForceStopBark()
    {
        Debug.Log($"[WorldItem] 強制停止 Item {itemID} 的 Bark");

        StopAllCoroutines();
        HideAllBarkUIs();

        DialogueInputPriority.SetBarkActive(false);

        CleanupBarkState();

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            PlayerMovement pm = player.GetComponent<PlayerMovement>();
            if (pm != null && lockMovementDuringBark)
            {
                pm.SetMovementLocked(false, $"Item {itemID}");
            }
        }
    }

    void CleanupBarkState()
    {
        string playingVar = $"BarkPlaying_Item{itemID}";
        PixelCrushers.DialogueSystem.DialogueLua.SetVariable(playingVar, false);

        if (currentBarkingItem == this)
        {
            currentBarkingItem = null;
            DialogueInputPriority.SetBarkActive(false);
        }

        actorsCached = false;

        Debug.Log($"[WorldItem] Item {itemID} Bark 狀態已清理");
    }

    void HideAllBarkUIs()
    {
        int hiddenCount = 0;

        var allBarkUIs = FindObjectsOfType<PixelCrushers.DialogueSystem.StandardBarkUI>();
        foreach (var barkUI in allBarkUIs)
        {
            if (barkUI != null && barkUI.isActiveAndEnabled)
            {
                barkUI.Hide();
                hiddenCount++;
                Debug.Log($"[WorldItem] 隱藏 Bark UI: {barkUI.gameObject.name}");
            }
        }

        var allWrappers = FindObjectsOfType<PixelCrushers.DialogueSystem.Wrappers.StandardBarkUI>();
        foreach (var wrapper in allWrappers)
        {
            if (wrapper != null && wrapper.isActiveAndEnabled)
            {
                wrapper.Hide();
                hiddenCount++;
                Debug.Log($"[WorldItem] 隱藏 Wrapper Bark UI: {wrapper.gameObject.name}");
            }
        }

        foreach (var actor in sceneActors)
        {
            if (actor != null && actor.barkUISettings.barkUI != null)
            {
                var barkUIComponent = actor.barkUISettings.barkUI.GetComponent<PixelCrushers.DialogueSystem.StandardBarkUI>();
                if (barkUIComponent == null)
                    barkUIComponent = actor.barkUISettings.barkUI.GetComponent<PixelCrushers.DialogueSystem.Wrappers.StandardBarkUI>();

                if (barkUIComponent != null)
                {
                    barkUIComponent.Hide();
                    Debug.Log($"[WorldItem] 通過角色隱藏 Bark UI: {actor.actor}");
                }
            }
        }

        // ScreenSpaceBarkUI 不繼承 StandardBarkUI，需要單獨處理
        ScreenSpaceBarkUI.Instance?.Hide();

        Debug.Log($"[WorldItem] ✓ 共隱藏 {hiddenCount} 個 Bark UI");
    }


    void HideBarkUI()
    {
        HideAllBarkUIs();
        // 同時關閉 ScreenSpaceBarkUI（HideAllBarkUIs 不包含非 StandardBarkUI）
        ScreenSpaceBarkUI.Instance?.Hide();

    }

    bool ShouldShowPopup()
    {
        if (!usePageBasedPopup)
        {
            return true;
        }

        if (InventoryManager.Inst == null || InventoryManager.Inst.database == null)
        {
            return true;
        }

        ItemData data = InventoryManager.Inst.database.Get(itemID);
        if (data == null)
        {
            return true;
        }

        bool visible = data.VisibleOn(currentPage);
        return visible;
    }

    [ContextMenu("重置物品狀態")]
    public void ResetItem()
    {
        picked = false;
        isProcessing = false;
        playerInRange = false;
        lastTriggerTime = -999f;
        hasAutoPickedUp = false;

        var col2D = GetComponent<Collider2D>();
        if (col2D) col2D.enabled = true;

        var col3D = GetComponent<Collider>();
        if (col3D) col3D.enabled = true;

        if (exclaim) exclaim.SetActive(false);
        if (interactPrompt) interactPrompt.SetActive(false);

        CleanupBarkState();
        PixelCrushers.DialogueSystem.DialogueLua.SetVariable($"BarkStep_Item{itemID}", 0);

        Debug.Log($"[WorldItem] 物品狀態已重置: {gameObject.name}");
    }
}
