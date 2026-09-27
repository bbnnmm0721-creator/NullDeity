using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class PanelCanvasToggle : MonoBehaviour
{
    [Tooltip("進入這些場景時『關閉』自己；其餘場景自動開啟")]
    [SerializeField] string[] hideInScenes = { "menu" };

    [Header("啟動行為")]
    [Tooltip("遊戲一開始是否視為已解鎖（建議先打勾驗證流程）")]
    [SerializeField] bool defaultUnlocked = false;

    [Tooltip("載入新場景後延後幾幀再重申一次顯示狀態，避免被其他腳本在 Start() 關掉")]
    [SerializeField] int reassertFramesAfterLoad = 2;

    [SerializeField] bool debugLog = true;

    [Header("物品解鎖設定")]
    [Tooltip("是否忽略物品解鎖，總是使用場景規則")]
    [SerializeField] bool ignoreItemUnlock = false;

    // ---- 全域解鎖狀態 & 實例清單 ----
    static bool sUnlocked;
    static bool sInit;
    static readonly List<PanelCanvasToggle> sAll = new List<PanelCanvasToggle>();

    /* ───────── Runner：專門跑延遲協程（不會被關） ───────── */
    class Runner : MonoBehaviour { }
    static Runner sRunner;
    static void EnsureRunner()
    {
        if (sRunner) return;
        var go = new GameObject("[PanelCanvasToggle.Runner]");
        DontDestroyOnLoad(go);
        sRunner = go.AddComponent<Runner>();
    }

    void Awake()
    {
        if (!sInit) { sUnlocked = defaultUnlocked; sInit = true; }
        if (!sAll.Contains(this)) sAll.Add(this);

        ToggleByScene(SceneManager.GetActiveScene());      // 先套用一次
        SceneManager.sceneLoaded += OnSceneLoaded;         // 之後每次載入場景都重算
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        sAll.Remove(this);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 先依規則立刻切（這一步可能把自己關掉）
        ToggleByScene(scene);

        // 若需要延後「重申一次」，交給常駐 Runner 來做
        if (reassertFramesAfterLoad > 0)
        {
            EnsureRunner();
            sRunner.StartCoroutine(CoReassertLater(reassertFramesAfterLoad));
        }
    }

    static IEnumerator CoReassertLater(int frames)
    {
        for (int i = 0; i < frames; i++) yield return null;

        var cur = SceneManager.GetActiveScene();
        sAll.RemoveAll(i => i == null);
        foreach (var i in sAll) i.ToggleByScene(cur);
    }

    void ToggleByScene(Scene scene)
    {
        bool inHiddenScene = false;
        for (int i = 0; i < hideInScenes.Length; i++)
        {
            if (string.Equals(scene.name, hideInScenes[i], System.StringComparison.Ordinal))
            { inHiddenScene = true; break; }
        }

        bool shouldHide = inHiddenScene || (!sUnlocked && !ignoreItemUnlock);

        if (debugLog)
        {
            Debug.Log(
                $"[PanelCanvasToggle] scene={scene.name}, unlocked={sUnlocked}, inHidden={inHiddenScene}, ignoreItemUnlock={ignoreItemUnlock} => SetActive({!shouldHide})",
                this
            );
        }

        gameObject.SetActive(!shouldHide);
    }

    // ===== 全域 API：一次改變所有並立刻刷新 =====
    public static void SetUnlocked(bool value)
    {
        bool wasUnlocked = sUnlocked;
        sUnlocked = value;
        Debug.Log($"[PanelCanvasToggle] 全域解鎖狀態從 {wasUnlocked} 變更為 {sUnlocked}");

        var cur = SceneManager.GetActiveScene();
        sAll.RemoveAll(i => i == null);
        foreach (var i in sAll) i.ToggleByScene(cur);
    }

    public static bool IsUnlocked => sUnlocked;

    /// <summary>強制顯示（無論解鎖與場景規則）</summary>
    public void ForceActivate()
    {
        gameObject.SetActive(true);
        Debug.Log($"[PanelCanvasToggle] {gameObject.name} 已被強制激活");
    }
}
