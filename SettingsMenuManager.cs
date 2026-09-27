using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

public enum FocusTarget
{
    BottomCarousel,
    SfxVolume,
    AmbienceVolume,
    MainVolume,
    SaveSlots,
    BackButton      // 第 3 頁「返回菜單」按鈕
}

public class SettingsMenuManager : MonoBehaviour
{
    public static SettingsMenuManager Instance { get; private set; }

    [Header("面板设定")]
    public GameObject settingPanel;
    public KeyCode toggleKey = KeyCode.Escape;

    [Header("背景图片")]
    public Image backgroundImage;
    public Sprite[] backgroundSprites = new Sprite[3];

    [Header("卷轴动画")]
    public Animator scrollAnimator;
    public string animationBoolName = "IsPlaying";
    public float animationResetDelay = 0.1f;
    public float contentSwitchDelay = 0.3f;

    [Header("底部选项 Carousel")]
    public SettingsCarousel bottomCarousel;

    [Header("存档格设置（第0页）")]
    public RectTransform saveContentRoot;
    public List<RectTransform> saveSlots;
    public float saveSlotSpacing = 420f;
    public float saveSlotCenterScale = 1.0f;
    public float saveSlotSideScale = 0.75f;
    public float saveSlotSideAlpha = 0.75f;
    public float saveSlotSmoothTime = 0.18f;

    [Header("音量控制（第1页）")]
    public UnityEngine.UI.Slider mainVolumeSlider;
    public UnityEngine.UI.Slider ambienceVolumeSlider;
    public UnityEngine.UI.Slider sfxVolumeSlider;
    public TextMeshProUGUI mainVolumeLabel;
    public TextMeshProUGUI ambienceVolumeLabel;
    public TextMeshProUGUI sfxVolumeLabel;
    [Range(1, 20)]
    public int volumeStepPercent = 5;

    [Header("卷轴内容父物件")]
    public GameObject[] scrollContentPages;

    [Header("返回菜單按鈕（第3頁）")]
    [Tooltip("BackButton 的 Button 組件，用於手把選中高亮與 onClick 掛接")]
    public Button backMenuButton;

    [Header("角色控制锁定")]
    public bool lockPlayerMovement = true;

    [Header("開啟 Setting 時隱藏的 Persistent HUD（跨場景）")]
    [Tooltip("DontDestroyOnLoad 物件路徑，例如：BookRoot、Dialogue Manager/Canvas/Slider")]
    public string[] crossSceneHidePaths;

    [Tooltip("Quest Tracker HUD 父物件路徑，例如 \"Dialogue Manager/Canvas\"")]
    public string questTrackerContainerPath = "Dialogue Manager/Canvas";

    [Tooltip("需要隱藏的 Quest Tracker HUD 名稱前綴")]
    public string questTrackerPrefix = "Basic Standard Quest Tracker HUD";

    private bool isOpen = false;
    private float previousTimeScale = 1f;
    private FocusTarget currentFocus = FocusTarget.BottomCarousel;
    private int currentPageIndex = 0;

    private int saveSlotCount;
    private float saveSlotPos;
    private float saveSlotVel;
    private int saveSlotTargetIndex = 0;

    // Gamepad stick navigation
    private const float GamepadStickThreshold = 0.5f;
    private bool _stickUpHeld, _stickDownHeld, _stickLeftHeld, _stickRightHeld;
    private bool _gamepadUp, _gamepadDown, _gamepadLeft, _gamepadRight;

    // 跨場景 HUD lazy cache
    private List<Transform> _crossSceneTargets;
    private Transform _questTrackerContainer;
    private bool _hudWasVisible;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SettingsMenuManager] 检测到重复实例，销毁新实例");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[SettingsMenuManager] ✅ 单例已创建，已设置 DontDestroyOnLoad");

        if (settingPanel)
            settingPanel.SetActive(false);

        if (scrollAnimator)
        {
            scrollAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            scrollAnimator.enabled = true;
            scrollAnimator.SetBool(animationBoolName, false);
        }

        if (bottomCarousel)
            bottomCarousel.SetEnabled(false);

        // 掛接 BackButton 的 onClick
        if (backMenuButton != null)
            backMenuButton.onClick.AddListener(BackToMenu);

        InitializeSaveSlots();

        Debug.Log("[SettingsMenuManager] 初始化完成");
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Debug.Log("[SettingsMenuManager] 单例已清除");
        }
    }

    private void InitializeSaveSlots()
    {
        if (saveContentRoot == null) return;

        if (saveSlots == null || saveSlots.Count == 0)
        {
            saveSlots = new List<RectTransform>();
            for (int i = 0; i < saveContentRoot.childCount; i++)
            {
                var rt = saveContentRoot.GetChild(i) as RectTransform;
                if (rt) saveSlots.Add(rt);
            }
        }

        foreach (var slot in saveSlots)
        {
            if (!slot.GetComponent<CanvasGroup>())
                slot.gameObject.AddComponent<CanvasGroup>();
        }

        saveSlotCount = saveSlots.Count;
        saveSlotTargetIndex = saveSlotCount / 2;
        saveSlotPos = saveSlotTargetIndex;

        LayoutSaveSlotsImmediate();

        Debug.Log($"[SettingsMenuManager] 初始化存档格: {saveSlotCount} 个，默认选中: {saveSlotTargetIndex}");
    }

    void Update()
    {
        var gp = Gamepad.current;

        bool togglePressed = Input.GetKeyDown(toggleKey) ||
                             (gp != null && gp.startButton.wasPressedThisFrame);
        if (togglePressed)
        {
            if (!isOpen) OpenSettings();
            else CloseSettings();
        }

        if (!isOpen) return;

        if (gp != null && gp.bButton.wasPressedThisFrame)
        {
            CloseSettings();
            return;
        }

        ReadGamepadDirections();
        HandleNavigation();
        HandleCurrentFocusInput();

        if (currentPageIndex == 0)
            UpdateSaveSlotsLayout();
    }

    /// <summary>讀取 L 搖桿與 D-pad，產生每次進入閾值只觸發一次的方向事件。</summary>
    private void ReadGamepadDirections()
    {
        var gp = Gamepad.current;
        Vector2 stick = gp != null ? gp.leftStick.ReadValue() : Vector2.zero;

        bool upNow = stick.y > GamepadStickThreshold;
        bool downNow = stick.y < -GamepadStickThreshold;
        bool leftNow = stick.x < -GamepadStickThreshold;
        bool rightNow = stick.x > GamepadStickThreshold;

        bool dpadUp = gp != null && gp.dpad.up.wasPressedThisFrame;
        bool dpadDown = gp != null && gp.dpad.down.wasPressedThisFrame;
        bool dpadLeft = gp != null && gp.dpad.left.wasPressedThisFrame;
        bool dpadRight = gp != null && gp.dpad.right.wasPressedThisFrame;

        _gamepadUp = (upNow && !_stickUpHeld) || dpadUp;
        _gamepadDown = (downNow && !_stickDownHeld) || dpadDown;
        _gamepadLeft = (leftNow && !_stickLeftHeld) || dpadLeft;
        _gamepadRight = (rightNow && !_stickRightHeld) || dpadRight;

        _stickUpHeld = upNow;
        _stickDownHeld = downNow;
        _stickLeftHeld = leftNow;
        _stickRightHeld = rightNow;
    }

    private void HandleNavigation()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || _gamepadUp)
            NavigateUp();

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || _gamepadDown)
            NavigateDown();
    }

    private void NavigateUp()
    {
        FocusTarget nextFocus = currentFocus;

        if (currentPageIndex == 0)
        {
            if (currentFocus == FocusTarget.BottomCarousel)
                nextFocus = FocusTarget.SaveSlots;
        }
        else if (currentPageIndex == 1)
        {
            switch (currentFocus)
            {
                case FocusTarget.BottomCarousel: nextFocus = FocusTarget.SfxVolume; break;
                case FocusTarget.SfxVolume: nextFocus = FocusTarget.AmbienceVolume; break;
                case FocusTarget.AmbienceVolume: nextFocus = FocusTarget.MainVolume; break;
                case FocusTarget.MainVolume:
                    Debug.Log("[SettingsMenuManager] 已在最上层（主音量）");
                    return;
            }
        }
        else if (currentPageIndex == 3)
        {
            // Back 頁：從 Carousel 往上選中 BackButton
            if (currentFocus == FocusTarget.BottomCarousel)
                nextFocus = FocusTarget.BackButton;
            else if (currentFocus == FocusTarget.BackButton)
            {
                Debug.Log("[SettingsMenuManager] 已在最上层（BackButton）");
                return;
            }
        }

        if (nextFocus != currentFocus)
            SwitchFocus(nextFocus);
    }

    private void NavigateDown()
    {
        FocusTarget nextFocus = currentFocus;

        if (currentPageIndex == 0)
        {
            if (currentFocus == FocusTarget.SaveSlots)
                nextFocus = FocusTarget.BottomCarousel;
            else if (currentFocus == FocusTarget.BottomCarousel)
            {
                Debug.Log("[SettingsMenuManager] 已在最下层（CarouselRoot）");
                return;
            }
        }
        else if (currentPageIndex == 1)
        {
            switch (currentFocus)
            {
                case FocusTarget.MainVolume: nextFocus = FocusTarget.AmbienceVolume; break;
                case FocusTarget.AmbienceVolume: nextFocus = FocusTarget.SfxVolume; break;
                case FocusTarget.SfxVolume: nextFocus = FocusTarget.BottomCarousel; break;
                case FocusTarget.BottomCarousel:
                    Debug.Log("[SettingsMenuManager] 已在最下层（CarouselRoot）");
                    return;
            }
        }
        else if (currentPageIndex == 3)
        {
            // Back 頁：從 BackButton 往下回到 Carousel
            if (currentFocus == FocusTarget.BackButton)
                nextFocus = FocusTarget.BottomCarousel;
            else if (currentFocus == FocusTarget.BottomCarousel)
            {
                Debug.Log("[SettingsMenuManager] 已在最下层（CarouselRoot）");
                return;
            }
        }

        if (nextFocus != currentFocus)
            SwitchFocus(nextFocus);
    }

    private void HandleCurrentFocusInput()
    {
        if (currentFocus == FocusTarget.BottomCarousel)
            HandleBottomCarouselInput();
        else if (currentFocus == FocusTarget.SaveSlots)
            HandleSaveSlotInput();
        else if (IsVolumeFocus(currentFocus))
            HandleVolumeInput();
        else if (currentFocus == FocusTarget.BackButton)
            HandleBackButtonInput();
    }

    private void HandleBottomCarouselInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || _gamepadLeft)
            if (bottomCarousel) bottomCarousel.Move(-1);

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || _gamepadRight)
            if (bottomCarousel) bottomCarousel.Move(+1);
    }

    private void HandleSaveSlotInput()
    {
        if (saveSlotCount == 0) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || _gamepadLeft)
            MoveSaveSlot(-1);

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || _gamepadRight)
            MoveSaveSlot(+1);

        var gp = Gamepad.current;
        bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) ||
                       (gp != null && gp.aButton.wasPressedThisFrame);
        if (confirm)
            SelectCurrentSaveSlot();
    }

    private void HandleVolumeInput()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || _gamepadRight)
            AdjustCurrentVolume(+1);

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || _gamepadLeft)
            AdjustCurrentVolume(-1);
    }

    /// <summary>BackButton 焦點下：A 鍵或 Enter 確認返回菜單。</summary>
    private void HandleBackButtonInput()
    {
        var gp = Gamepad.current;
        bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) ||
                       (gp != null && gp.aButton.wasPressedThisFrame);
        if (confirm)
            BackToMenu();
    }

    /// <summary>關閉 Setting 並透過 BackToMenuManager 返回主菜單。</summary>
    public void BackToMenu()
    {
        CloseSettings();

        if (BackToMenuManager.Instance != null)
            BackToMenuManager.Instance.ReturnToMenu();
        else
            Debug.LogWarning("[SettingsMenuManager] BackToMenuManager 不存在，無法返回菜單");
    }

    private void AdjustCurrentVolume(int direction)
    {
        UnityEngine.UI.Slider targetSlider = GetCurrentVolumeSlider();
        if (targetSlider == null) return;

        float step = volumeStepPercent / 100f;
        float newValue = Mathf.Clamp01(targetSlider.value + (step * direction));
        targetSlider.value = newValue;

        int percentage = Mathf.RoundToInt(newValue * 100f);
        Debug.Log($"[SettingsMenuManager] {GetVolumeFocusName(currentFocus)}: {percentage}%");
    }

    private UnityEngine.UI.Slider GetCurrentVolumeSlider()
    {
        switch (currentFocus)
        {
            case FocusTarget.MainVolume: return mainVolumeSlider;
            case FocusTarget.AmbienceVolume: return ambienceVolumeSlider;
            case FocusTarget.SfxVolume: return sfxVolumeSlider;
            default: return null;
        }
    }

    private TextMeshProUGUI GetCurrentVolumeLabel()
    {
        switch (currentFocus)
        {
            case FocusTarget.MainVolume: return mainVolumeLabel;
            case FocusTarget.AmbienceVolume: return ambienceVolumeLabel;
            case FocusTarget.SfxVolume: return sfxVolumeLabel;
            default: return null;
        }
    }

    private string GetVolumeFocusName(FocusTarget focus)
    {
        switch (focus)
        {
            case FocusTarget.MainVolume: return "主音量";
            case FocusTarget.AmbienceVolume: return "环境音量";
            case FocusTarget.SfxVolume: return "音效音量";
            default: return "未知";
        }
    }

    private bool IsVolumeFocus(FocusTarget focus) =>
        focus == FocusTarget.MainVolume ||
        focus == FocusTarget.AmbienceVolume ||
        focus == FocusTarget.SfxVolume;

    private void UpdateVolumeLabel(FocusTarget focus, bool isBold)
    {
        TextMeshProUGUI label = null;
        switch (focus)
        {
            case FocusTarget.MainVolume: label = mainVolumeLabel; break;
            case FocusTarget.AmbienceVolume: label = ambienceVolumeLabel; break;
            case FocusTarget.SfxVolume: label = sfxVolumeLabel; break;
        }
        if (label != null)
            label.fontStyle = isBold ? FontStyles.Bold : FontStyles.Normal;
    }

    private void MoveSaveSlot(int dir)
    {
        int oldIndex = saveSlotTargetIndex;
        saveSlotTargetIndex = (saveSlotTargetIndex + (dir % saveSlotCount) + saveSlotCount) % saveSlotCount;
        if (oldIndex != saveSlotTargetIndex)
            Debug.Log($"[SettingsMenuManager] 存档格移动: {oldIndex} → {saveSlotTargetIndex}");
    }

    private void SelectCurrentSaveSlot()
    {
        Debug.Log($"[SettingsMenuManager] 选择存档格: {saveSlotTargetIndex}");
    }

    private void UpdateSaveSlotsLayout()
    {
        if (saveSlotCount == 0) return;

        saveSlotPos = Mathf.SmoothDamp(saveSlotPos, WrapIndex(saveSlotTargetIndex, saveSlotCount),
            ref saveSlotVel, saveSlotSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        LayoutSaveSlots();
    }

    private void LayoutSaveSlots()
    {
        if (saveSlotCount == 0) return;

        for (int i = 0; i < saveSlotCount; i++)
        {
            float raw = i - saveSlotPos;
            raw = Mathf.Repeat(raw + saveSlotCount * 0.5f, saveSlotCount) - saveSlotCount * 0.5f;

            var rt = saveSlots[i];
            rt.anchoredPosition = new Vector2(raw * saveSlotSpacing, 0f);

            float t = Mathf.Clamp01(1f - Mathf.Abs(raw));
            float scale = Mathf.Lerp(saveSlotSideScale, saveSlotCenterScale, t);
            rt.localScale = Vector3.one * scale;

            var cg = rt.GetComponent<CanvasGroup>();
            cg.alpha = Mathf.Lerp(saveSlotSideAlpha, 1f, t);
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        int center = WrapIndex(Mathf.RoundToInt(saveSlotPos), saveSlotCount);
        var centerCg = saveSlots[center].GetComponent<CanvasGroup>();
        centerCg.alpha = 1f;
        centerCg.interactable = true;
        centerCg.blocksRaycasts = true;
    }

    private void LayoutSaveSlotsImmediate()
    {
        if (saveSlotCount == 0) return;
        float tmpVel = 0f;
        saveSlotPos = Mathf.SmoothDamp(saveSlotPos, WrapIndex(saveSlotTargetIndex, saveSlotCount), ref tmpVel, 0f);
        LayoutSaveSlots();
    }

    private void SwitchFocus(FocusTarget newFocus)
    {
        if (currentFocus == newFocus) return;

        OnFocusExit(currentFocus);
        currentFocus = newFocus;
        OnFocusEnter(currentFocus);

        Debug.Log($"[SettingsMenuManager] 焦点切换至: {GetFocusName(currentFocus)}");
    }

    private string GetFocusName(FocusTarget focus)
    {
        switch (focus)
        {
            case FocusTarget.BottomCarousel: return "CarouselRoot";
            case FocusTarget.SaveSlots: return "存档格";
            case FocusTarget.MainVolume: return "主音量";
            case FocusTarget.AmbienceVolume: return "环境音量";
            case FocusTarget.SfxVolume: return "音效音量";
            case FocusTarget.BackButton: return "BackButton";
            default: return focus.ToString();
        }
    }

    private void OnFocusEnter(FocusTarget focus)
    {
        if (focus == FocusTarget.BottomCarousel)
        {
            if (bottomCarousel) bottomCarousel.SetEnabled(true);
            Debug.Log("[SettingsMenuManager] ✅ 进入 CarouselRoot");
        }
        else if (focus == FocusTarget.SaveSlots)
        {
            Debug.Log($"[SettingsMenuManager] ✅ 进入存档格（默认: 存档{saveSlotTargetIndex + 1}）");
        }
        else if (IsVolumeFocus(focus))
        {
            UpdateVolumeLabel(focus, true);
            Debug.Log($"[SettingsMenuManager] ✅ 进入 {GetVolumeFocusName(focus)}");
        }
        else if (focus == FocusTarget.BackButton)
        {
            // 用 EventSystem 選中 BackButton，觸發 Button 的高亮狀態
            if (backMenuButton != null)
                EventSystem.current?.SetSelectedGameObject(backMenuButton.gameObject);
            Debug.Log("[SettingsMenuManager] ✅ 进入 BackButton");
        }
    }

    private void OnFocusExit(FocusTarget focus)
    {
        if (focus == FocusTarget.BottomCarousel)
        {
            if (bottomCarousel) bottomCarousel.SetEnabled(false);
        }
        else if (IsVolumeFocus(focus))
        {
            UpdateVolumeLabel(focus, false);
        }
        else if (focus == FocusTarget.BackButton)
        {
            // 取消 EventSystem 選中，讓 Button 回到 Normal 狀態
            if (EventSystem.current?.currentSelectedGameObject == backMenuButton?.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void OpenSettings()
    {
        if (isOpen) return;

        isOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (lockPlayerMovement && PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.SetMovementLocked(true, "设定菜单");
            Debug.Log("[SettingsMenuManager] 🔒 已锁定角色移动");
        }

        // 記錄 HUD 當前狀態，再隱藏（避免在 Menu 封面開設定時弄亂 PressAnyKeyToStart 的隱藏）
        _hudWasVisible = IsHudCurrentlyVisible();
        if (_hudWasVisible)
            SetHudVisible(false);

        Debug.Log("[SettingsMenuManager] 开启设定菜单");

        RandomizeBackground();

        if (settingPanel)
            settingPanel.SetActive(true);

        currentFocus = FocusTarget.BottomCarousel;
        currentPageIndex = 0;

        if (bottomCarousel)
        {
            bottomCarousel.SetEnabled(true);
            bottomCarousel.ResetToIndex(0);
        }

        saveSlotTargetIndex = saveSlotCount / 2;
        saveSlotPos = saveSlotTargetIndex;
        saveSlotVel = 0;
        LayoutSaveSlotsImmediate();

        ShowScrollContent(0);
    }

    public void CloseSettings()
    {
        if (!isOpen) return;

        isOpen = false;
        Time.timeScale = previousTimeScale;

        if (lockPlayerMovement && PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.SetMovementLocked(false, "设定菜单");
            Debug.Log("[SettingsMenuManager] 🔓 已解锁角色移动");
        }

        // 只在 HUD 原本可見時才恢復（Menu 封面的狀況 HUD 本來就隱藏，不應恢復）
        if (_hudWasVisible)
            SetHudVisible(true);

        Debug.Log("[SettingsMenuManager] 关闭设定菜单");

        OnFocusExit(currentFocus);

        if (bottomCarousel)
            bottomCarousel.SetEnabled(false);

        if (settingPanel)
            settingPanel.SetActive(false);
    }

    // ─── 跨場景 HUD 控制 ───────────────────────────────────────────────────────

    /// <summary>解析 crossSceneHidePaths 並快取 Transform（DontDestroyOnLoad 可被 Find）。</summary>
    private List<Transform> GetCrossSceneTargets()
    {
        if (_crossSceneTargets != null) return _crossSceneTargets;

        _crossSceneTargets = new List<Transform>();
        if (crossSceneHidePaths == null) return _crossSceneTargets;

        foreach (var path in crossSceneHidePaths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            var go = GameObject.Find(path);
            if (go != null)
            {
                _crossSceneTargets.Add(go.transform);
                Debug.Log($"[SettingsMenuManager] 跨場景目標快取: {path}");
            }
            else
            {
                Debug.LogWarning($"[SettingsMenuManager] 找不到跨場景目標: {path}");
            }
        }
        return _crossSceneTargets;
    }

    /// <summary>查找並快取 Quest Tracker 父容器。</summary>
    private Transform GetQuestTrackerContainer()
    {
        if (_questTrackerContainer != null) return _questTrackerContainer;
        if (string.IsNullOrEmpty(questTrackerContainerPath)) return null;

        var go = GameObject.Find(questTrackerContainerPath);
        if (go != null)
        {
            _questTrackerContainer = go.transform;
            Debug.Log($"[SettingsMenuManager] Quest Tracker 容器快取: {questTrackerContainerPath}");
        }
        else
        {
            Debug.LogWarning($"[SettingsMenuManager] 找不到 Quest Tracker 容器: {questTrackerContainerPath}");
        }
        return _questTrackerContainer;
    }

    /// <summary>檢查 HUD 當前是否可見（以第一個跨場景目標為準）。</summary>
    private bool IsHudCurrentlyVisible()
    {
        var targets = GetCrossSceneTargets();
        if (targets.Count == 0) return true;

        var first = targets[0];
        if (first == null) return true;

        var cg = first.GetComponent<CanvasGroup>();
        return cg != null ? cg.alpha > 0f : first.gameObject.activeSelf;
    }

    /// <summary>設定所有 Persistent HUD 的顯示狀態。</summary>
    private void SetHudVisible(bool visible)
    {
        foreach (var target in GetCrossSceneTargets())
        {
            if (target == null) continue;
            ApplyVisibility(target, visible);
        }

        if (!string.IsNullOrEmpty(questTrackerPrefix))
        {
            var container = GetQuestTrackerContainer();
            if (container != null)
            {
                foreach (Transform child in container)
                {
                    if (child.name.StartsWith(questTrackerPrefix))
                        ApplyVisibility(child, visible);
                }
            }
        }
    }

    /// <summary>優先使用 CanvasGroup 控制能見度，否則退回 SetActive。</summary>
    private void ApplyVisibility(Transform target, bool visible)
    {
        var cg = target.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }
        else
        {
            target.gameObject.SetActive(visible);
        }
    }

    // ─── 其他工具 ──────────────────────────────────────────────────────────────

    private void RandomizeBackground()
    {
        if (backgroundImage && backgroundSprites != null && backgroundSprites.Length > 0)
        {
            int randomIndex = Random.Range(0, backgroundSprites.Length);
            if (backgroundSprites[randomIndex] != null)
            {
                backgroundImage.sprite = backgroundSprites[randomIndex];
                Debug.Log($"[SettingsMenuManager] 随机背景: {randomIndex}");
            }
        }
    }

    public void OnPageChanged(int newPageIndex)
    {
        Debug.Log($"[SettingsMenuManager] 页面切换: {currentPageIndex} → {newPageIndex}");

        if (scrollAnimator)
            StartCoroutine(PlayScrollAnimation());

        StartCoroutine(DelayedContentSwitch(newPageIndex));
    }

    private IEnumerator PlayScrollAnimation()
    {
        scrollAnimator.SetBool(animationBoolName, true);
        yield return new WaitForSecondsRealtime(animationResetDelay);
        scrollAnimator.SetBool(animationBoolName, false);
    }

    private IEnumerator DelayedContentSwitch(int newPageIndex)
    {
        yield return new WaitForSecondsRealtime(contentSwitchDelay);

        OnFocusExit(currentFocus);
        currentPageIndex = newPageIndex;
        currentFocus = FocusTarget.BottomCarousel;
        OnFocusEnter(currentFocus);
        ShowScrollContent(newPageIndex);

        Debug.Log($"[SettingsMenuManager] 内容已切换至: {newPageIndex}");
    }

    private void ShowScrollContent(int index)
    {
        if (scrollContentPages == null || scrollContentPages.Length == 0) return;

        for (int i = 0; i < scrollContentPages.Length; i++)
        {
            if (scrollContentPages[i])
                scrollContentPages[i].SetActive(i == index);
        }

        Debug.Log($"[SettingsMenuManager] 显示内容页: {index}");
    }

    int WrapIndex(int i, int count) => (i % count + count) % count;

    public bool IsOpen => isOpen;
}
