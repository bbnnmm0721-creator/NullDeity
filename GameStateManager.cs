using UnityEngine;
using PixelCrushers.DialogueSystem;
using UnityEngine.SceneManagement;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Persistent 場景對象管理")]
    [Tooltip("persistent 場景中的玩家角色")]
    public GameObject playerCharacter;

    [Tooltip("persistent 場景中的重要物品容器")]
    public GameObject importantItemContainer;

    [Tooltip("persistent 場景中的對話速度 Slider")]
    public GameObject dialogueSlider;

    [Header("場景設定")]
    [Tooltip("菜單場景名稱")]
    public string menuSceneName = "Menu";

    [Header("調試設定")]
    [Tooltip("啟用詳細日誌")]
    public bool enableDebugLog = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        Log("✅ GameStateManager 初始化完成");
    }

    void Start()
    {
        FindPersistentObjects();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == menuSceneName)
        {
            Log("📍 載入菜單場景，禁用遊戲對象");
            DisableGameObjects();
            // ★ 不再自動重置狀態，由 New Game 按鈕明確觸發
        }
        else if (scene.name != "persistent")
        {
            Log($"📍 載入遊戲場景: {scene.name}");
            EnableGameObjects();
        }
    }

    void FindPersistentObjects()
    {
        if (playerCharacter == null)
        {
            playerCharacter = GameObject.Find("character");
            if (playerCharacter != null)
                Log($"🔍 自動找到 playerCharacter: {playerCharacter.name}");
        }

        if (importantItemContainer == null)
        {
            importantItemContainer = GameObject.Find("Imporant Item");
            if (importantItemContainer != null)
                Log($"🔍 自動找到 importantItemContainer: {importantItemContainer.name}");
        }

        if (dialogueSlider == null)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Transform sliderTransform = canvas.transform.Find("Slider");
                if (sliderTransform != null)
                {
                    dialogueSlider = sliderTransform.gameObject;
                    Log($"🔍 自動找到 dialogueSlider: {dialogueSlider.name}");
                }
            }
        }
    }

    public void ResetAllGameState()
    {
        Log("🔄 開始重置所有遊戲狀態...");

        ResetDialogueSystem();
        ResetQuestSystem();
        ResetPuzzleProgress();
        ResetPlayerPrefs();
        ResetScenePortals();
        DisableGameObjects();

        Log("✅ 所有遊戲狀態已重置完成！");
    }

    void ResetDialogueSystem()
    {
        Log("🗨️ 重置 Dialogue System...");
        DialogueManager.ResetDatabase(DatabaseResetOptions.RevertToDefault);
        Log("   - Dialogue Database 已重置到初始狀態");
    }

    void ResetQuestSystem()
    {
        Log("📋 重置 Quest System...");

        string[] allQuests = QuestLog.GetAllQuests();

        foreach (string questName in allQuests)
        {
            if (!string.IsNullOrEmpty(questName))
            {
                QuestLog.SetQuestState(questName, QuestState.Unassigned);

                int entryCount = QuestLog.GetQuestEntryCount(questName);
                for (int i = 1; i <= entryCount; i++)
                {
                    QuestLog.SetQuestEntryState(questName, i, QuestState.Unassigned);
                }
            }
        }

        Log($"   - 已重置 {allQuests.Length} 個任務狀態");
    }

    void ResetPuzzleProgress()
    {
        Log("🧩 重置解謎進度...");

        string[] puzzleKeys = new string[]
        {
            "PuzzleRings_Solved",
            "SymbolPuzzle_Solved",
            "BoxPuzzle_Solved",
            "CircleRotationPuzzle_SecondFloor_Solved",
        };

        int deletedCount = 0;

        foreach (string key in puzzleKeys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                Log($"   - 已刪除解謎進度: {key}");
                deletedCount++;
            }
        }

        DeleteAllPlayerPrefsWithSuffix("_Solved");

        PlayerPrefs.Save();
        Log($"   - 解謎進度已清除（共 {deletedCount} 個）");
    }

    void DeleteAllPlayerPrefsWithSuffix(string suffix)
    {
        string[] allPossiblePuzzleIDs = new string[]
        {
            "PuzzleRings",
            "SymbolPuzzle",
            "BoxPuzzle",
            "CircleRotationPuzzle_SecondFloor",
            "CircleRotationPuzzle_FirstFloor",
            "CircleRotationPuzzle_ThirdFloor",
        };

        foreach (string puzzleID in allPossiblePuzzleIDs)
        {
            string key = puzzleID + suffix;
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                Log($"   - 已刪除: {key}");
            }
        }
    }

    void ResetPlayerPrefs()
    {
        Log("💾 清除 PlayerPrefs...");

        int count = 0;

        string[] itemKeys = new string[]
        {
            "LastPickedItem",
            "Item_Book",
            "Item_Key"
        };

        foreach (string key in itemKeys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                count++;
            }
        }

        PlayerPrefs.Save();
        Log($"   - 已清除 {count} 個 PlayerPrefs 鍵值");
    }

    void ResetScenePortals()
    {
        Log("🚪 重置場景傳送門狀態...");
        InteractiveScenePortal.ResetLoadingState();
        SceneLoader.ResetLoadingState();
        Log("   - 傳送門狀態已重置");
    }

    void DisableGameObjects()
    {
        Log("🔒 禁用遊戲對象...");

        if (playerCharacter != null && playerCharacter.activeSelf)
        {
            playerCharacter.SetActive(false);
            Log("   - 已禁用 character");
        }

        if (importantItemContainer != null && importantItemContainer.activeSelf)
        {
            importantItemContainer.SetActive(false);
            Log("   - 已禁用 Imporant Item");
        }

        if (dialogueSlider != null && dialogueSlider.activeSelf)
        {
            dialogueSlider.SetActive(false);
            Log("   - 已禁用 Dialogue Slider");
        }
    }

    void EnableGameObjects()
    {
        Log("✅ 啟用遊戲對象...");

        if (playerCharacter != null && !playerCharacter.activeSelf)
        {
            playerCharacter.SetActive(true);
            Log("   - 已啟用 character");
        }

        if (importantItemContainer != null && !importantItemContainer.activeSelf)
        {
            importantItemContainer.SetActive(true);
            Log("   - 已啟用 Imporant Item");
        }

        if (dialogueSlider != null && !dialogueSlider.activeSelf)
        {
            dialogueSlider.SetActive(true);
            Log("   - 已啟用 Dialogue Slider");
        }
    }

    /// <summary>開始新遊戲：完整重置所有狀態後載入第一章。</summary>
    public void StartNewGame(string firstSceneName = "Chapter1-3")
    {
        Log($"🎮 開始新遊戲，載入場景: {firstSceneName}");

        FindPersistentObjects();
        ResetAllGameState();

        // 清除繼續遊戲的 Session，確保 Continue 按鈕正確隱藏
        BackToMenuManager.ClearSession();

        EnableGameObjects();

        TravelData.SetNextSpawn("Default");
        GlobalSceneTransition.Go(firstSceneName, "Default");
    }

    /// <summary>
    /// 繼續遊戲：載入返回主畫面前的場景。
    /// 所有遊戲狀態（對話變數、任務、物品）完整保留在 DDOL 物件中，不重置。
    /// </summary>
    public void ContinueGame()
    {
        if (!BackToMenuManager.HasSession || string.IsNullOrEmpty(BackToMenuManager.LastGameScene))
        {
            Log("⚠️ 沒有可繼續的遊戲階段，忽略 ContinueGame");
            return;
        }

        string targetScene = BackToMenuManager.LastGameScene;
        Log($"▶️ 繼續遊戲，載入場景: {targetScene}");

        FindPersistentObjects();
        EnableGameObjects();

        TravelData.SetNextSpawn("Default");
        GlobalSceneTransition.Go(targetScene, "Default");
    }

    public void QuitGame()
    {
        Log("👋 退出遊戲");

        ResetAllGameState();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log($"<color=cyan>[GameStateManager]</color> {message}");
    }
}
