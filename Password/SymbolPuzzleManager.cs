using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using PixelCrushers.DialogueSystem;

public class SymbolPuzzleManager : MonoBehaviour
{
    [Header("Puzzle Settings")]
    [SerializeField] private int[] correctAnswer = new int[] { 3, 1, 4, 2, 5 };

    [Header("Camera System")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera puzzleCamera;

    [Header("Transition")]
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private float transitionDuration = 1f;

    [Header("UI References")]
    [SerializeField] private SymbolSlot[] answerSlots;
    [SerializeField] private DraggableSymbol[] draggableSymbols;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject puzzlePanel;

    [Header("Symbol Sprites")]
    [SerializeField] private Sprite[] symbolSprites;

    [Header("Chest & Rewards")]
    [SerializeField] private GameObject chestObject;
    [SerializeField] private string rewardItemID;

    [Header("Components to Disable")]
    [SerializeField] private MonoBehaviour[] playerMovementComponents;
    [SerializeField] private MonoBehaviour[] bookPanelComponents;
    [SerializeField] private bool autoFindComponents = true;

    [Header("Gamepad Virtual Cursor")]
    [SerializeField] private RectTransform virtualCursor;
    [SerializeField] private float cursorSpeed = 800f;
    [SerializeField] private bool enableGamepadCursor = true;

    [Header("Events")]
    public UnityEvent onPuzzleSolved;
    public UnityEvent onEnterPuzzle;
    public UnityEvent onExitPuzzle;

    private const int TOTAL_SYMBOLS = 5;
    private bool isInPuzzleMode = false;
    private bool puzzleSolved = false;
    private bool[] originalPlayerMovementStates;
    private bool[] originalBookPanelStates;

    private Vector2 cursorPosition;
    private bool isGamepadDragging = false;
    private GameObject currentDragTarget = null;
    private PointerEventData pointerEventData;
    private Canvas canvas;
    private bool originalSendNavigationEvents = true;

    private void Awake()
    {
        if (autoFindComponents)
        {
            AutoFindComponents();
        }

        ValidateSetup();
        canvas = puzzlePanel.GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        InitializePuzzle();

        submitButton.onClick.AddListener(CheckAnswer);
        resetButton.onClick.AddListener(ResetPuzzle);
        exitButton.onClick.AddListener(ExitPuzzleMode);

        if (mainCamera) mainCamera.gameObject.SetActive(true);
        if (puzzleCamera) puzzleCamera.gameObject.SetActive(false);
        if (puzzlePanel) puzzlePanel.SetActive(false);

        if (virtualCursor != null)
        {
            virtualCursor.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (isInPuzzleMode && enableGamepadCursor)
        {
            HandleGamepadCursorControl();
        }
    }

    private void HandleGamepadCursorControl()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null) return;

        Vector2 leftStick = gamepad.leftStick.ReadValue();

        if (leftStick.magnitude > 0.1f && virtualCursor != null)
        {
            cursorPosition += leftStick * cursorSpeed * Time.deltaTime;

            cursorPosition.x = Mathf.Clamp(cursorPosition.x, 0, Screen.width);
            cursorPosition.y = Mathf.Clamp(cursorPosition.y, 0, Screen.height);

            virtualCursor.position = cursorPosition;

            if (isGamepadDragging && currentDragTarget != null)
            {
                UpdateDrag();
            }
        }

        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            if (!isGamepadDragging)
            {
                StartGamepadDrag();
            }
            else
            {
                EndGamepadDrag();
            }
        }
    }

    private void StartGamepadDrag()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        pointerEventData = new PointerEventData(eventSystem)
        {
            position = cursorPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerEventData, results);

        foreach (var result in results)
        {
            DraggableSymbol symbol = result.gameObject.GetComponent<DraggableSymbol>();

            if (symbol == null)
            {
                symbol = result.gameObject.GetComponentInParent<DraggableSymbol>();
            }

            if (symbol != null)
            {
                currentDragTarget = symbol.gameObject;
                isGamepadDragging = true;

                ExecuteEvents.Execute(currentDragTarget, pointerEventData, ExecuteEvents.beginDragHandler);
                Debug.Log($"[SymbolPuzzle] 開始手柄拖拽: {currentDragTarget.name}");
                return;
            }

            Button button = result.gameObject.GetComponent<Button>();
            if (button == null)
            {
                button = result.gameObject.GetComponentInParent<Button>();
            }

            if (button != null && button.interactable)
            {
                button.onClick.Invoke();
                Debug.Log($"[SymbolPuzzle] 點擊按鈕: {button.name}");
                return;
            }
        }
    }

    private void UpdateDrag()
    {
        if (currentDragTarget == null || pointerEventData == null) return;

        pointerEventData.position = cursorPosition;
        ExecuteEvents.Execute(currentDragTarget, pointerEventData, ExecuteEvents.dragHandler);
    }

    private void EndGamepadDrag()
    {
        if (currentDragTarget == null || pointerEventData == null) return;

        pointerEventData.position = cursorPosition;
        ExecuteEvents.Execute(currentDragTarget, pointerEventData, ExecuteEvents.endDragHandler);

        Debug.Log($"[SymbolPuzzle] 結束手柄拖拽: {currentDragTarget.name}");

        currentDragTarget = null;
        isGamepadDragging = false;
    }

    private void AutoFindComponents()
    {
        PlayerMovement[] playerMovements = FindObjectsOfType<PlayerMovement>();
        if (playerMovements.Length > 0)
        {
            playerMovementComponents = new MonoBehaviour[playerMovements.Length];
            for (int i = 0; i < playerMovements.Length; i++)
            {
                playerMovementComponents[i] = playerMovements[i];
            }
            Debug.Log($"[SymbolPuzzle] 找到 {playerMovements.Length} 個 PlayerMovement");
        }

        BookTutorialCoach_UITK[] bookCoaches = FindObjectsOfType<BookTutorialCoach_UITK>();
        if (bookCoaches.Length > 0)
        {
            bookPanelComponents = new MonoBehaviour[bookCoaches.Length];
            for (int i = 0; i < bookCoaches.Length; i++)
            {
                bookPanelComponents[i] = bookCoaches[i];
            }
            Debug.Log($"[SymbolPuzzle] 找到 {bookCoaches.Length} 個 BookTutorialCoach_UITK");
        }

        if (playerMovementComponents != null)
        {
            originalPlayerMovementStates = new bool[playerMovementComponents.Length];
        }
        if (bookPanelComponents != null)
        {
            originalBookPanelStates = new bool[bookPanelComponents.Length];
        }
    }

    private void ValidateSetup()
    {
        if (!mainCamera) Debug.LogError("[SymbolPuzzle] 缺少 Main Camera!");
        if (!puzzleCamera) Debug.LogError("[SymbolPuzzle] 缺少 Puzzle Camera!");
        if (!fadeCanvas) Debug.LogWarning("[SymbolPuzzle] 缺少 Fade Canvas，將跳過淡入淡出");
        if (symbolSprites.Length != TOTAL_SYMBOLS) Debug.LogError("[SymbolPuzzle] 需要 5 個符號圖片！");
    }

    private void InitializePuzzle()
    {
        Debug.Log("[SymbolPuzzle] 開始初始化謎題");

        if (symbolSprites == null || symbolSprites.Length != TOTAL_SYMBOLS)
        {
            Debug.LogError($"[SymbolPuzzle] 符號圖片數量錯誤！當前: {(symbolSprites?.Length ?? 0)}, 需要: {TOTAL_SYMBOLS}");
            return;
        }

        if (draggableSymbols == null || draggableSymbols.Length != TOTAL_SYMBOLS)
        {
            Debug.LogError($"[SymbolPuzzle] 可拖拽符號數量錯誤！當前: {(draggableSymbols?.Length ?? 0)}, 需要: {TOTAL_SYMBOLS}");
            return;
        }

        if (answerSlots == null || answerSlots.Length != TOTAL_SYMBOLS)
        {
            Debug.LogError($"[SymbolPuzzle] 槽位數量錯誤！當前: {(answerSlots?.Length ?? 0)}, 需要: {TOTAL_SYMBOLS}");
            return;
        }

        for (int i = 0; i < draggableSymbols.Length; i++)
        {
            if (draggableSymbols[i] != null)
            {
                draggableSymbols[i].Initialize(i + 1, symbolSprites[i], this);
            }
            else
            {
                Debug.LogError($"[SymbolPuzzle] 可拖拽符號 [{i}] 是 null！");
            }
        }

        for (int i = 0; i < answerSlots.Length; i++)
        {
            if (answerSlots[i] != null)
            {
                answerSlots[i].Initialize(i);
            }
            else
            {
                Debug.LogError($"[SymbolPuzzle] 槽位 [{i}] 是 null！");
            }
        }

        if (feedbackText != null)
        {
            feedbackText.text = "將符號由小至大、由左至右拖拽到槽位中，然後提交你的答案";
        }

        Debug.Log("[SymbolPuzzle] 謎題初始化完成");
    }

    public void EnterPuzzleMode()
    {
        QuestState questState = QuestLog.GetQuestState("解箱子謎題");
        if (questState != QuestState.Active)
        {
            Debug.Log($"[SymbolPuzzle] 任务 'SolveBoxPuzzle' 状态: {questState}，需要 Active");
            DialogueManager.ShowAlert("現在還不能打開這箱子...");
            return;
        }

        if (isInPuzzleMode)
        {
            Debug.LogWarning("[SymbolPuzzle] 已经在谜题模式中");
            return;
        }

        if (puzzleSolved)
        {
            Debug.Log("[SymbolPuzzle] 谜题已经解开");

            QuestState solvedQuestState = QuestLog.GetQuestState("解箱子謎題");
            if (solvedQuestState != QuestState.Success)
            {
                QuestLog.SetQuestState("解箱子謎題", QuestState.Success);
                Debug.Log("[SymbolPuzzle] 任务 '解箱子謎題' 已完成！");
            }

            if (InventoryManager.Inst != null && !InventoryManager.Inst.Owns(4))
            {
                InventoryManager.Inst.Add(4);
                Debug.Log("[SymbolPuzzle] 已添加物品 ID=4 到 Inventory");
            }

            ItemPopup.Show(1);

            DialogueManager.ShowAlert("这个谜题已经解开了，箱子里的笔记已经拿到了。");
            return;
        }

        Debug.Log("[SymbolPuzzle] 进入谜题模式");
        StartCoroutine(TransitionToPuzzle());
    }

    public void ExitPuzzleMode()
    {
        if (!isInPuzzleMode)
        {
            Debug.LogWarning("[SymbolPuzzle] 不在解謎模式中");
            return;
        }

        Debug.Log("[SymbolPuzzle] 退出解謎模式");
        StartCoroutine(TransitionToMain());
    }

    private IEnumerator TransitionToPuzzle()
    {
        isInPuzzleMode = true;
        onEnterPuzzle?.Invoke();

        CameraStateManager.SetCameraState(false);

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            originalSendNavigationEvents = eventSystem.sendNavigationEvents;
            eventSystem.sendNavigationEvents = false;
            Debug.Log("[SymbolPuzzle] 已禁用導航事件");
        }

        if (fadeCanvas)
        {
            yield return StartCoroutine(Fade(0f, 1f, transitionDuration * 0.4f));
        }

        mainCamera.gameObject.SetActive(false);
        puzzleCamera.gameObject.SetActive(true);
        puzzlePanel.SetActive(true);

        DisablePlayerComponents();

        if (virtualCursor != null)
        {
            cursorPosition = new Vector2(Screen.width / 2, Screen.height / 2);
            virtualCursor.position = cursorPosition;
            virtualCursor.gameObject.SetActive(true);
        }

        if (fadeCanvas)
        {
            yield return StartCoroutine(Fade(1f, 0f, transitionDuration * 0.6f));
        }

        Debug.Log("[SymbolPuzzle] 轉場完成");
    }

    private IEnumerator TransitionToMain()
    {
        if (fadeCanvas)
        {
            yield return StartCoroutine(Fade(0f, 1f, transitionDuration * 0.4f));
        }

        if (virtualCursor != null)
        {
            virtualCursor.gameObject.SetActive(false);
        }

        puzzlePanel.SetActive(false);
        puzzleCamera.gameObject.SetActive(false);
        mainCamera.gameObject.SetActive(true);

        EnablePlayerComponents();

        CameraStateManager.SetCameraState(true);

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            eventSystem.sendNavigationEvents = originalSendNavigationEvents;
            Debug.Log("[SymbolPuzzle] 已恢復導航事件");
        }

        isInPuzzleMode = false;
        isGamepadDragging = false;
        currentDragTarget = null;
        onExitPuzzle?.Invoke();

        if (fadeCanvas)
        {
            yield return StartCoroutine(Fade(1f, 0f, transitionDuration * 0.6f));
        }

        Debug.Log("[SymbolPuzzle] 回到主場景");
    }

    private void DisablePlayerComponents()
    {
        if (playerMovementComponents != null)
        {
            for (int i = 0; i < playerMovementComponents.Length; i++)
            {
                if (playerMovementComponents[i] != null)
                {
                    originalPlayerMovementStates[i] = playerMovementComponents[i].enabled;
                    playerMovementComponents[i].enabled = false;
                }
            }
        }

        if (bookPanelComponents != null)
        {
            for (int i = 0; i < bookPanelComponents.Length; i++)
            {
                if (bookPanelComponents[i] != null)
                {
                    originalBookPanelStates[i] = bookPanelComponents[i].enabled;
                    bookPanelComponents[i].enabled = false;
                }
            }
        }
    }

    private void EnablePlayerComponents()
    {
        if (playerMovementComponents != null && originalPlayerMovementStates != null)
        {
            for (int i = 0; i < playerMovementComponents.Length; i++)
            {
                if (playerMovementComponents[i] != null)
                {
                    playerMovementComponents[i].enabled = originalPlayerMovementStates[i];
                }
            }
        }

        if (bookPanelComponents != null && originalBookPanelStates != null)
        {
            for (int i = 0; i < bookPanelComponents.Length; i++)
            {
                if (bookPanelComponents[i] != null)
                {
                    bookPanelComponents[i].enabled = originalBookPanelStates[i];
                }
            }
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (!fadeCanvas) yield break;

        fadeCanvas.gameObject.SetActive(true);
        fadeCanvas.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvas.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        fadeCanvas.alpha = to;

        if (to <= 0f)
        {
            fadeCanvas.blocksRaycasts = false;
            fadeCanvas.gameObject.SetActive(false);
        }
    }

    public void CheckAnswer()
    {
        int[] currentAnswer = new int[TOTAL_SYMBOLS];
        bool allSlotsFilled = true;

        for (int i = 0; i < answerSlots.Length; i++)
        {
            if (answerSlots[i].HasSymbol)
            {
                currentAnswer[i] = answerSlots[i].CurrentSymbolValue;
            }
            else
            {
                allSlotsFilled = false;
                break;
            }
        }

        if (!allSlotsFilled)
        {
            feedbackText.text = "請填滿所有槽位！";
            return;
        }

        int correctPositions = CountCorrectPositions(currentAnswer);

        if (correctPositions == TOTAL_SYMBOLS)
        {
            OnPuzzleSolved();
        }
        else
        {
            feedbackText.text = $"有 {correctPositions} 個符號在正確的位置上";
        }
    }

    private int CountCorrectPositions(int[] guess)
    {
        int count = 0;
        for (int i = 0; i < TOTAL_SYMBOLS; i++)
        {
            if (guess[i] == correctAnswer[i])
            {
                count++;
            }
        }
        return count;
    }

    private void OnPuzzleSolved()
    {
        puzzleSolved = true;
        feedbackText.text = "恭喜！你解開了謎題！";

        submitButton.interactable = false;
        resetButton.interactable = false;

        foreach (var symbol in draggableSymbols)
        {
            symbol.SetInteractable(false);
        }

        onPuzzleSolved?.Invoke();

        QuestLog.SetQuestState("解箱子謎題", QuestState.Success);
        Debug.Log("[SymbolPuzzle] 任务 '解箱子謎題' 已完成！");

        // 设置 FindGift 变量为 true
        DialogueLua.SetVariable("FindGift", true);
        Debug.Log("[SymbolPuzzle] 已设置 Variable['FindGift'] = true");

        QuestState otherQuestState = QuestLog.GetQuestState("與小羊對話-尋找紙片");
        if (otherQuestState == QuestState.Success)
        {
            QuestLog.SetQuestState("回去找小羊", QuestState.Active);
            Debug.Log("[SymbolPuzzle] 两个前置任务都完成了，激活任务 '回去找小羊'");
            DialogueManager.ShowAlert("所有任务都完成了！回去找小羊吧。");
        }

        if (InventoryManager.Inst != null)
        {
            InventoryManager.Inst.Add(3);
            Debug.Log("[SymbolPuzzle] 已添加物品 ID=3 到 Inventory");

            InventoryManager.Inst.Add(203);
            InventoryManager.Inst.Add(204);
            InventoryManager.Inst.Add(205);
            InventoryManager.Inst.Add(206);
            InventoryManager.Inst.Add(207);

            ItemPopup.Show(3);
            Debug.Log("[SymbolPuzzle] 显示物品弹窗: ID=3");
        }
        else
        {
            Debug.LogError("[SymbolPuzzle] InventoryManager.Inst 为空！");
        }

        StartCoroutine(OpenChestAndExit());
    }


    private IEnumerator OpenChestAndExit()
    {
        yield return new WaitForSeconds(1.5f);

        if (chestObject != null)
        {
            Animator chestAnimator = chestObject.GetComponent<Animator>();
            if (chestAnimator != null)
            {
                chestAnimator.SetTrigger("Open");
            }
        }

        yield return new WaitForSeconds(2f);

        ExitPuzzleMode();
    }

    public void ResetPuzzle()
    {
        foreach (var slot in answerSlots)
        {
            slot.ClearSlot();
        }

        foreach (var symbol in draggableSymbols)
        {
            symbol.ResetPosition();
        }

        feedbackText.text = "已重置，重新開始猜測吧！";
    }

    public SymbolSlot FindNearestSlot(Vector2 position)
    {
        SymbolSlot nearest = null;
        float minDistance = float.MaxValue;
        float snapDistance = 150f;

        foreach (var slot in answerSlots)
        {
            if (slot == null) continue;

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            if (slotRect == null) continue;

            float distance = Vector2.Distance(position, slotRect.position);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = slot;
            }
        }

        if (minDistance <= snapDistance && nearest != null)
        {
            return nearest;
        }

        return null;
    }
}
