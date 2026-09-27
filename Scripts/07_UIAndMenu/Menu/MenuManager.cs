using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class MenuManager : MonoBehaviour
{
    [Header("场景设定")]
    [Tooltip("开始游戏时要载入的场景名称")]
    public string firstSceneName = "Chapter1-3";

    [Header("导航模式")]
    [Tooltip("启用鼠标控制")]
    public bool enableMouseControl = true;

    [Tooltip("启用手柄/键盘导航")]
    public bool enableGamepadNavigation = true;

    [Tooltip("等待开场动画完成后才启用导航")]
    public bool waitForIntroAnimation = true;

    [Header("导航设置")]
    [Tooltip("摇杆必须回到中心才能再次触发导航")]
    public bool requireStickReturn = true;

    [Tooltip("输入死区（摇杆阈值）")]
    public float inputDeadzone = 0.5f;

    [Tooltip("摇杆回中阈值（低于此值视为回到中心）")]
    public float returnToNeutralThreshold = 0.3f;

    [Header("打字机效果设置")]
    [Tooltip("打字机效果速度（每秒字符数）")]
    public float typewriterSpeed = 30f;

    [Tooltip("文字淡入速度")]
    public float textFadeSpeed = 5f;

    [Header("Raycast 设置")]
    [Tooltip("可点击物体应该在这个 Layer 上")]
    public LayerMask clickableLayer = -1;

    [Tooltip("Raycast 最大距离")]
    public float maxRayDistance = 1000f;

    [Header("音效")]
    public AudioClip navigateSound;
    public AudioClip selectSound;

    [Header("调试")]
    public bool showDebugRays = false;
    public bool showDebugLogs = true;

    private List<MenuClickable> menuItems = new List<MenuClickable>();
    private int currentNavigationIndex = 0;
    private GameObject currentHoverObject;
    private MenuClickable currentClickable;
    private Coroutine typewriterCoroutine;
    private Camera mainCamera;
    private AudioSource audioSource;
    private bool isUsingMouse = false;
    private bool isNavigationEnabled = false;

    private bool stickWasNeutral = true;
    private bool upPressed = false;
    private bool downPressed = false;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[MenuManager] ❌ 找不到主摄像机！");
            return;
        }

        SetupAudio();
        EnsureGameStateManager();
        StartCoroutine(InitializeMenuItems());

        if (!waitForIntroAnimation)
        {
            isNavigationEnabled = true;
        }

        if (showDebugLogs)
        {
            Debug.Log($"[MenuManager] ✅ 初始化完成");
            Debug.Log($"[MenuManager] Clickable Layer: {LayerMaskToString(clickableLayer)}");
            Debug.Log($"[MenuManager] Navigation Enabled: {isNavigationEnabled}");
        }
    }

    void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (navigateSound != null || selectSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    IEnumerator InitializeMenuItems()
    {
        yield return new WaitForEndOfFrame();

        MenuClickable[] foundItems = FindObjectsOfType<MenuClickable>();
        menuItems = foundItems
            .Where(item => item.gameObject.activeInHierarchy)
            .OrderByDescending(item => item.transform.position.y)
            .ToList();

        // ★ 根據是否有可繼續的階段來控制 Continue 按鈕的顯示
        RefreshContinueButton();

        if (showDebugLogs)
        {
            Debug.Log($"[MenuManager] 找到 {menuItems.Count} 个菜单项：");
            for (int i = 0; i < menuItems.Count; i++)
            {
                Debug.Log($"  [{i}] {menuItems[i].gameObject.name} (Y: {menuItems[i].transform.position.y:F2})");
            }
        }
    }

    /// <summary>根據 HasSession 顯示或隱藏 ContinueGame 按鈕，並重建導航列表。</summary>
    void RefreshContinueButton()
    {
        MenuClickable[] allItems = FindObjectsOfType<MenuClickable>(true);
        foreach (var item in allItems)
        {
            if (item.menuAction == MenuAction.ContinueGame)
            {
                bool shouldShow = BackToMenuManager.HasSession;
                item.gameObject.SetActive(shouldShow);

                if (showDebugLogs)
                    Debug.Log($"[MenuManager] Continue 按鈕 {(shouldShow ? "顯示" : "隱藏")}（HasSession: {BackToMenuManager.HasSession}）");
            }
            else if (item.menuAction == MenuAction.StartGame)
            {
                bool shouldShow = !BackToMenuManager.HasSession;
                item.gameObject.SetActive(shouldShow);

                if (showDebugLogs)
                    Debug.Log($"[MenuManager] Start 按鈕 {(shouldShow ? "顯示" : "隱藏")}（HasSession: {BackToMenuManager.HasSession}）");
            }
        }

        // 重建 menuItems（因為 active 狀態改變了）
        menuItems = FindObjectsOfType<MenuClickable>()
            .Where(item => item.gameObject.activeInHierarchy)
            .OrderByDescending(item => item.transform.position.y)
            .ToList();
    }


    void EnsureGameStateManager()
    {
        if (GameStateManager.Instance == null)
        {
            GameObject go = new GameObject("GameStateManager");
            go.AddComponent<GameStateManager>();
            Debug.Log("[MenuManager] 自动创建 GameStateManager");
        }
    }

    void Update()
    {
        if (mainCamera == null || !isNavigationEnabled) return;

        if (enableMouseControl)
        {
            HandleMouseControl();
        }

        if (enableGamepadNavigation && !isUsingMouse)
        {
            HandleGamepadNavigation();
            HandleSubmit();
        }
    }

    public void SetNavigationEnabled(bool enabled)
    {
        bool wasEnabled = isNavigationEnabled;
        isNavigationEnabled = enabled;

        if (showDebugLogs)
            Debug.Log($"[MenuManager] 导航状态变更: {wasEnabled} → {enabled}");

        if (enabled && !wasEnabled && menuItems.Count > 0 && enableGamepadNavigation)
        {
            SelectNavigationItem(0);

            if (showDebugLogs)
                Debug.Log($"[MenuManager] 🎮 导航已启用，默认选中第一项");
        }
    }

    void HandleMouseControl()
    {
        if (Mouse.current == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (showDebugRays)
            Debug.DrawRay(ray.origin, ray.direction * maxRayDistance, Color.green, 0.1f);

        if (Physics.Raycast(ray, out hit, maxRayDistance, clickableLayer))
        {
            if (showDebugRays)
                Debug.DrawLine(ray.origin, hit.point, Color.red, 0.1f);

            GameObject hitObject = hit.collider.gameObject;

            if (showDebugLogs && currentHoverObject != hitObject)
                Debug.Log($"[MenuManager] 🎯 Raycast 击中: {hitObject.name}");

            var clickable = hitObject.GetComponent<MenuClickable>();

            if (clickable != null)
            {
                if (currentHoverObject != hitObject)
                {
                    isUsingMouse = true;

                    int index = menuItems.IndexOf(clickable);
                    if (index >= 0)
                    {
                        DeselectNavigationItem();
                        currentNavigationIndex = index;
                        OnHoverEnter(clickable);
                    }

                    currentHoverObject = hitObject;
                    currentClickable = clickable;
                }

                if (Mouse.current.leftButton.wasPressedThisFrame)
                    OnClick(clickable);

                return;
            }
            else if (showDebugLogs && currentHoverObject != hitObject)
            {
                Debug.LogWarning($"[MenuManager] ⚠️ {hitObject.name} 没有 MenuClickable 组件！");
            }
        }

        if (currentHoverObject != null)
        {
            OnHoverExit();
            currentHoverObject = null;
            currentClickable = null;
        }
    }

    void HandleGamepadNavigation()
    {
        if (menuItems.Count == 0) return;

        bool upInput = false;
        bool downInput = false;

        if (Keyboard.current != null)
        {
            upInput = Keyboard.current.upArrowKey.wasPressedThisFrame ||
                     Keyboard.current.wKey.wasPressedThisFrame;
            downInput = Keyboard.current.downArrowKey.wasPressedThisFrame ||
                       Keyboard.current.sKey.wasPressedThisFrame;
        }

        if (Gamepad.current != null)
        {
            upInput |= Gamepad.current.dpad.up.wasPressedThisFrame;
            downInput |= Gamepad.current.dpad.down.wasPressedThisFrame;

            Vector2 stickInput = Gamepad.current.leftStick.ReadValue();
            float stickMagnitude = stickInput.magnitude;

            if (requireStickReturn)
            {
                if (stickMagnitude < returnToNeutralThreshold)
                {
                    stickWasNeutral = true;
                    upPressed = false;
                    downPressed = false;
                }
                else if (stickWasNeutral && stickMagnitude > inputDeadzone)
                {
                    if (stickInput.y > inputDeadzone && !upPressed)
                    {
                        upInput = true;
                        upPressed = true;
                        stickWasNeutral = false;

                        if (showDebugLogs)
                            Debug.Log($"[MenuManager] 🕹️ 摇杆向上触发 (Y: {stickInput.y:F2})");
                    }
                    else if (stickInput.y < -inputDeadzone && !downPressed)
                    {
                        downInput = true;
                        downPressed = true;
                        stickWasNeutral = false;

                        if (showDebugLogs)
                            Debug.Log($"[MenuManager] 🕹️ 摇杆向下触发 (Y: {stickInput.y:F2})");
                    }
                }
            }
            else
            {
                if (stickInput.y > inputDeadzone)
                    upInput = true;
                else if (stickInput.y < -inputDeadzone)
                    downInput = true;
            }
        }

        if (upInput)
        {
            isUsingMouse = false;
            NavigateMenu(-1);
        }
        else if (downInput)
        {
            isUsingMouse = false;
            NavigateMenu(1);
        }
    }

    void HandleSubmit()
    {
        bool submitPressed = false;

        if (Gamepad.current != null)
            submitPressed = Gamepad.current.buttonSouth.wasPressedThisFrame;

        if (Keyboard.current != null)
        {
            submitPressed |= Keyboard.current.enterKey.wasPressedThisFrame ||
                            Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        if (submitPressed && currentClickable != null)
            OnClick(currentClickable);
    }

    void NavigateMenu(int direction)
    {
        if (menuItems.Count == 0) return;

        DeselectNavigationItem();

        currentNavigationIndex += direction;

        if (currentNavigationIndex < 0)
            currentNavigationIndex = menuItems.Count - 1;
        else if (currentNavigationIndex >= menuItems.Count)
            currentNavigationIndex = 0;

        SelectNavigationItem(currentNavigationIndex);
        PlaySound(navigateSound);

        if (showDebugLogs)
            Debug.Log($"[MenuManager] 🎮 导航到: {menuItems[currentNavigationIndex].gameObject.name} (索引: {currentNavigationIndex})");
    }

    void SelectNavigationItem(int index)
    {
        if (index < 0 || index >= menuItems.Count) return;
        currentClickable = menuItems[index];
        OnHoverEnter(currentClickable);
    }

    void DeselectNavigationItem()
    {
        if (currentClickable != null)
            OnHoverExit();
    }

    void OnHoverEnter(MenuClickable clickable)
    {
        if (showDebugLogs)
            Debug.Log($"<color=cyan>[MenuManager] ✅ 选中: {clickable.gameObject.name}</color>");

        clickable.OnHoverEnter();

        if (clickable.dedicatedText != null && !string.IsNullOrEmpty(clickable.hoverDescription))
        {
            if (typewriterCoroutine != null)
                StopCoroutine(typewriterCoroutine);

            typewriterCoroutine = StartCoroutine(TypewriterEffect(clickable.dedicatedText, clickable.hoverDescription));
        }
        else if (clickable.dedicatedText == null)
        {
            Debug.LogWarning($"[MenuManager] ⚠️ {clickable.gameObject.name} 没有设置 Dedicated Text！");
        }
    }

    void OnHoverExit()
    {
        if (showDebugLogs)
            Debug.Log($"<color=grey>[MenuManager] 取消选中</color>");

        if (currentClickable != null)
        {
            currentClickable.OnHoverExit();

            if (currentClickable.dedicatedText != null)
            {
                if (typewriterCoroutine != null)
                    StopCoroutine(typewriterCoroutine);

                StartCoroutine(FadeOutText(currentClickable.dedicatedText));
            }
        }
    }

    void OnClick(MenuClickable clickable)
    {
        if (showDebugLogs)
            Debug.Log($"<color=green>[MenuManager] 🎮 执行: {clickable.gameObject.name}</color>");

        PlaySound(selectSound);
        clickable.OnClick();

        switch (clickable.menuAction)
        {
            case MenuAction.StartGame:
                OnStartGame();
                break;

            case MenuAction.ContinueGame:
                OnContinueGame();
                break;

            case MenuAction.QuitGame:
                OnQuitGame();
                break;

            case MenuAction.Custom:
                break;
        }
    }

    IEnumerator TypewriterEffect(TMP_Text textComponent, string fullText)
    {
        if (textComponent == null) yield break;

        CanvasGroup cg = textComponent.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = textComponent.gameObject.AddComponent<CanvasGroup>();

        textComponent.text = "";
        cg.alpha = 1f;

        float charDelay = 1f / typewriterSpeed;

        for (int i = 0; i <= fullText.Length; i++)
        {
            textComponent.text = fullText.Substring(0, i);
            yield return new WaitForSeconds(charDelay);
        }
    }

    IEnumerator FadeOutText(TMP_Text textComponent)
    {
        if (textComponent == null) yield break;

        CanvasGroup cg = textComponent.GetComponent<CanvasGroup>();
        if (cg == null) yield break;

        while (cg.alpha > 0f)
        {
            cg.alpha -= Time.deltaTime * textFadeSpeed;
            yield return null;
        }

        textComponent.text = "";
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    void OnStartGame()
    {
        Debug.Log($"<color=green>[MenuManager] 🎮 开始游戏</color>");

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.StartNewGame(firstSceneName);
        else
            Debug.LogError("[MenuManager] ❌ GameStateManager 不存在！");
    }

    /// <summary>繼續遊戲：回到返回主畫面前的場景，狀態完整保留。</summary>
    void OnContinueGame()
    {
        Debug.Log($"<color=cyan>[MenuManager] ▶️ 繼續遊戲</color>");

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.ContinueGame();
        else
            Debug.LogError("[MenuManager] ❌ GameStateManager 不存在！");
    }

    void OnQuitGame()
    {
        Debug.Log($"<color=yellow>[MenuManager] 👋 退出游戏</color>");

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.QuitGame();
        }
        else
        {
            Debug.LogError("[MenuManager] ❌ GameStateManager 不存在！");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    string LayerMaskToString(LayerMask mask)
    {
        string result = "";
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) != 0)
            {
                if (result.Length > 0) result += ", ";
                result += LayerMask.LayerToName(i);
            }
        }
        return string.IsNullOrEmpty(result) ? "Nothing" : result;
    }
}
