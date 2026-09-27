using UnityEngine;
using PixelCrushers.DialogueSystem;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class DialogueContinueBridge : MonoBehaviour
{
    [Tooltip("如果沒有指定會自動尋找場景中第一個可用的 Dialogue UI。")]
    public AbstractDialogueUI dialogueUI;

    [Header("按鈕引用")]
    public GameObject btn1;
    public GameObject btn6;

    [Header("可點擊按鈕")]
    public Transform slidesParent;

    [Header("十字鍵導航設定")]
    [Tooltip("按左鍵時切換到前一張圖片")]
    public bool enableLeftNavigation = true;
    
    [Tooltip("按右鍵時切換到下一張圖片")]
    public bool enableRightNavigation = true;
    
    [Tooltip("按上鍵時切換到前一張圖片")]
    public bool enableUpNavigation = false;
    
    [Tooltip("按下鍵時切換到下一張圖片")]
    public bool enableDownNavigation = false;

    [Header("Debug")]
    public bool showDebug = true;

    [Header("狀態控制")]
    private bool waitingForBtn6 = false;
    private bool waitingForSlidesDone = false;
    
    private PlayerInputActions inputActions;
    private Button[] picButtons;
    private int currentPicIndex = -1;
    private float navigationCooldown = 0.2f;
    private float lastNavigationTime = 0f;

    void Awake()
    {
        inputActions = new PlayerInputActions();
        SetupPicButtons();
    }

    void SetupPicButtons()
    {
        if (slidesParent == null)
        {
            slidesParent = transform.Find("Slides");
        }

        if (slidesParent == null)
        {
            Debug.LogWarning("[DialogueContinueBridge] 找不到 Slides 父物件");
            return;
        }

        // 🎯 收集所有 pic 按鈕
        picButtons = new Button[6];
        
        for (int i = 1; i <= 6; i++)
        {
            Transform picTransform = slidesParent.Find($"pic{i}");
            if (picTransform != null)
            {
                Transform btnTransform = picTransform.Find($"Pic{i}_Btn");
                if (btnTransform != null)
                {
                    Button picButton = btnTransform.GetComponent<Button>();
                    if (picButton != null)
                    {
                        picButtons[i - 1] = picButton;
                        
                        int picIndex = i;
                        // 🎯 添加 SlidesDone 調用（只在 pic6 時觸發）
                        if (i == 6)
                        {
                            picButton.onClick.AddListener(() => OnPicButtonClicked(picIndex));
                        }
                        
                        if (showDebug) Debug.Log($"[DialogueContinueBridge] 已註冊 Pic{picIndex}_Btn");
                    }
                }
            }
        }
    }

    void OnEnable()
    {
        inputActions?.Player.Enable();
    }

    void OnDisable()
    {
        inputActions?.Player.Disable();
    }

    void OnDestroy()
    {
        inputActions?.Dispose();
    }

    void Start()
    {
        if (btn6 != null)
        {
            var button6 = btn6.GetComponent<Button>();
            if (button6 != null)
            {
                button6.onClick.AddListener(OnBtn6Clicked);
            }
        }
    }

    void Update()
    {
        if (!waitingForSlidesDone) return;

        // 🎯 更新當前激活的 pic 索引
        UpdateCurrentPicIndex();

        // 🎯 處理十字鍵導航
        HandleDirectionalNavigation();
    }

    void UpdateCurrentPicIndex()
    {
        if (slidesParent == null) return;

        for (int i = 1; i <= 6; i++)
        {
            Transform picTransform = slidesParent.Find($"pic{i}");
            if (picTransform != null && picTransform.gameObject.activeSelf)
            {
                if (currentPicIndex != i - 1)
                {
                    currentPicIndex = i - 1;
                    if (showDebug) Debug.Log($"[UpdateCurrentPicIndex] 當前 pic 索引: {currentPicIndex + 1}");
                }
                break;
            }
        }
    }

    void HandleDirectionalNavigation()
    {
        if (inputActions == null || currentPicIndex < 0) return;

        // 🎯 冷卻時間，防止連續觸發
        if (Time.time - lastNavigationTime < navigationCooldown) return;

        Vector2 move = inputActions.Player.Move.ReadValue<Vector2>();

        // 🎯 左鍵：前一張圖片
        if (enableLeftNavigation && move.x < -0.5f)
        {
            NavigateToPreviousPic();
            lastNavigationTime = Time.time;
        }
        // 🎯 右鍵：下一張圖片
        else if (enableRightNavigation && move.x > 0.5f)
        {
            NavigateToNextPic();
            lastNavigationTime = Time.time;
        }
        // 🎯 上鍵：前一張圖片
        else if (enableUpNavigation && move.y > 0.5f)
        {
            NavigateToPreviousPic();
            lastNavigationTime = Time.time;
        }
        // 🎯 下鍵：下一張圖片
        else if (enableDownNavigation && move.y < -0.5f)
        {
            NavigateToNextPic();
            lastNavigationTime = Time.time;
        }
    }

    void NavigateToPreviousPic()
    {
        if (currentPicIndex > 0)
        {
            int previousIndex = currentPicIndex - 1;
            if (showDebug) Debug.Log($"[NavigateToPreviousPic] 從 pic{currentPicIndex + 1} → pic{previousIndex + 1}");
            
            // 🎯 直接點擊前一張的按鈕（但這不存在，因為按鈕是單向的）
            // 所以我們需要反向邏輯
            Debug.LogWarning("[NavigateToPreviousPic] 當前設計不支援回到前一張圖片");
        }
        else
        {
            if (showDebug) Debug.Log("[NavigateToPreviousPic] 已經是第一張圖片");
        }
    }

    void NavigateToNextPic()
    {
        if (currentPicIndex >= 0 && currentPicIndex < picButtons.Length)
        {
            Button currentButton = picButtons[currentPicIndex];
            if (currentButton != null && currentButton.gameObject.activeSelf)
            {
                if (showDebug) Debug.Log($"[NavigateToNextPic] 點擊 Pic{currentPicIndex + 1}_Btn");
                currentButton.onClick.Invoke();
            }
        }
    }

    void OnPicButtonClicked(int picIndex)
    {
        if (showDebug) Debug.Log($"[OnPicButtonClicked] Pic{picIndex}_Btn 被點擊！waitingForSlidesDone={waitingForSlidesDone}");

        // 🎯 只有在等待 SlidesDone 時才觸發
        if (!waitingForSlidesDone) return;

        Debug.Log($"[OnPicButtonClicked] ✅ Pic{picIndex}_Btn 觸發 SlidesDone");
        DisableInteraction();
        SendSlidesDone();
    }

    public void Continue()
    {
        if (waitingForBtn6)
        {
            if (showDebug) Debug.Log("[Continue] 正在等待 Btn6，無法繼續");
            return;
        }

        if (dialogueUI == null) dialogueUI = FindObjectOfType<AbstractDialogueUI>();

        if (dialogueUI != null)
        {
            dialogueUI.OnContinue();
        }
        else
        {
            SendMessage("OnContinue", SendMessageOptions.DontRequireReceiver);
            if (DialogueManager.instance != null)
                DialogueManager.instance.SendMessage("OnContinueConversation", SendMessageOptions.DontRequireReceiver);
        }
    }

    // 🎯 啟用互動（由 Sequence 調用）
    public void EnableInteraction()
    {
        Debug.Log("[EnableInteraction] ✅ 被調用！");
        waitingForSlidesDone = true;
        currentPicIndex = -1;
    }

    public void DisableInteraction()
    {
        Debug.Log("[DisableInteraction] 被調用");
        waitingForSlidesDone = false;
        currentPicIndex = -1;
    }

    public void StartWaitingForBtn6()
    {
        Debug.Log("[StartWaitingForBtn6] 開始等待 Btn6");
        waitingForBtn6 = true;

        DisableContinueButton();

        if (btn1 != null) btn1.SetActive(true);
        if (btn6 != null) btn6.SetActive(true);
    }

    void OnBtn6Clicked()
    {
        if (waitingForBtn6)
        {
            Debug.Log("[OnBtn6Clicked] Btn6 被點擊");
            waitingForBtn6 = false;
            EnableContinueButton();
            SendSlidesDone();
        }
    }

    void DisableContinueButton()
    {
        if (dialogueUI != null)
        {
            var continueButton = dialogueUI.GetComponentInChildren<Button>();
            if (continueButton != null && continueButton.name.ToLower().Contains("continue"))
            {
                continueButton.interactable = false;
            }
        }
    }

    void EnableContinueButton()
    {
        if (dialogueUI != null)
        {
            var continueButton = dialogueUI.GetComponentInChildren<Button>();
            if (continueButton != null && continueButton.name.ToLower().Contains("continue"))
            {
                continueButton.interactable = true;
            }
        }
    }

    public void SendSlidesDone()
    {
        Debug.Log("[SendSlidesDone] 🎉 SlidesDone 被送出！");
        Sequencer.Message("SlidesDone");
    }
}
