using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class BackToMenuManager : MonoBehaviour
{
    public static BackToMenuManager Instance { get; private set; }
    public static bool SkipIntroAnimation { get; set; } = false;

    /// <summary>返回主畫面前所在的場景，供繼續遊戲使用。</summary>
    public static string LastGameScene { get; private set; }

    /// <summary>是否有可繼續的遊戲階段（狀態未被重置）。</summary>
    public static bool HasSession { get; private set; } = false;

    [Header("菜單場景設定")]
    public string menuSceneName = "Menu";

    [Header("返回菜單熱鍵")]
    public KeyCode returnToMenuKey = KeyCode.Escape;
    public bool enableKeyboardHotkey = true;
    public bool enableGamepadMenuButton = true;

    [Header("調試設定")]
    public bool enableDebugLog = true;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Log("✅ BackToMenuManager 初始化完成");
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name == menuSceneName) return;

        bool shouldReturn = false;

        if (enableKeyboardHotkey && Input.GetKeyDown(returnToMenuKey))
        {
            Log($"🎹 檢測到鍵盤熱鍵 {returnToMenuKey}");
            shouldReturn = true;
        }

        if (enableGamepadMenuButton && Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
        {
            Log("🎮 檢測到手柄 Menu 鍵");
            shouldReturn = true;
        }

        if (shouldReturn) ReturnToMenu();
    }

    void OnDestroy()
    {
        // 不再訂閱 sceneLoaded，讓 GameStateManager 負責場景事件
    }

    /// <summary>
    /// 返回主畫面。保留當前遊戲狀態以供繼續遊戲使用，不重置任何資料。
    /// </summary>
    public void ReturnToMenu()
    {
        // 記住當前章節場景（排除 persistent 和 menu 本身）
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name != menuSceneName && s.name != "persistent")
            {
                LastGameScene = s.name;
                break;
            }
        }

        HasSession = !string.IsNullOrEmpty(LastGameScene);

        Log($"🔙 返回菜單，保留階段（LastGameScene: {LastGameScene}）");

        SkipIntroAnimation = true;

        // ★ 不呼叫 ResetAllGameState，狀態完整保留供繼續遊戲
        if (GlobalSceneTransition.Inst != null)
            GlobalSceneTransition.Go(menuSceneName, "Default");
        else
            SceneManager.LoadScene(menuSceneName);
    }

    /// <summary>清除 Session 記錄（新遊戲開始後呼叫）。</summary>
    public static void ClearSession()
    {
        LastGameScene = null;
        HasSession = false;
    }

    void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log($"<color=yellow>[BackToMenuManager]</color> {message}");
    }
}
