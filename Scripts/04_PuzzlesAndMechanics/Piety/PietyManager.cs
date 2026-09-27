using UnityEngine;
using PixelCrushers.DialogueSystem;
using System;

/// <summary>
/// 虔誠值管理器 - 負責管理全域的虔誠值系統
/// 使用單例模式確保整個遊戲只有一個虔誠值管理器
/// </summary>
public class PietyManager : MonoBehaviour
{
    [Header("虔誠值設定")]
    [SerializeField] private float currentPiety = 90f;  // 當前虔誠值，初始設為90
    [SerializeField] private float minPiety = 0f;       // 虔誠值下限
    [SerializeField] private float maxPiety = 100f;     // 虔誠值上限

    // 單例實例，讓其他腳本可以通過 PietyManager.Instance 來存取
    public static PietyManager Instance { get; private set; }

    // 事件系統 - 當虔誠值發生變化時通知其他腳本
    public static event Action<float> OnPietyChanged;    // 虔誠值改變時觸發
    public static event Action<float> OnPietyReachedMin; // 虔誠值達到最小值時觸發
    public static event Action<float> OnPietyReachedMax; // 虔誠值達到最大值時觸發

    // 公開屬性，讓其他腳本可以讀取虔誠值資訊
    public float CurrentPiety => currentPiety;           // 獲取當前虔誠值
    public float MinPiety => minPiety;                   // 獲取最小虔誠值
    public float MaxPiety => maxPiety;                   // 獲取最大虔誠值
    public float PietyPercentage => currentPiety / maxPiety; // 獲取虔誠值百分比 (0-1)

    void Awake()
    {
        // 實作單例模式
        if (Instance == null)
        {
            Instance = this; // 設定當前物件為單例實例
            DontDestroyOnLoad(gameObject); // 切換場景時不銷毀此物件

            // 註冊供 Dialogue System 使用的 Lua 函數
            RegisterLuaFunctions();
        }
        else
        {
            // 如果已經有實例存在，銷毀重複的物件
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 遊戲開始時觸發虔誠值變更事件，更新所有UI
        OnPietyChanged?.Invoke(currentPiety);
    }

    /// <summary>
    /// 註冊 Lua 函數供 Dialogue System 使用
    /// 這樣就可以在對話中直接呼叫這些函數來操作虔誠值
    /// </summary>
    void RegisterLuaFunctions()
    {
        if (DialogueManager.instance != null)
        {
            // 註冊各種操作虔誠值的 Lua 函數
            Lua.RegisterFunction("GetPiety", this, SymbolExtensions.GetMethodInfo(() => GetPiety()));
            Lua.RegisterFunction("SetPiety", this, SymbolExtensions.GetMethodInfo(() => SetPiety(0)));
            Lua.RegisterFunction("AddPiety", this, SymbolExtensions.GetMethodInfo(() => AddPiety(0)));
            Lua.RegisterFunction("SubtractPiety", this, SymbolExtensions.GetMethodInfo(() => SubtractPiety(0)));
        }
    }

    /// <summary>
    /// 設定虔誠值為指定數值
    /// 會自動限制在最小值和最大值之間
    /// </summary>
    /// <param name="value">要設定的虔誠值</param>
    public void SetPiety(float value)
    {
        float previousPiety = currentPiety; // 記錄變更前的數值
        currentPiety = Mathf.Clamp(value, minPiety, maxPiety); // 限制數值範圍

        // 如果數值沒有實際變化，就不執行後續動作
        if (Mathf.Approximately(previousPiety, currentPiety)) return;

        // 觸發虔誠值變更事件，通知所有監聽的腳本
        OnPietyChanged?.Invoke(currentPiety);

        // 檢查是否達到邊界值並觸發相應事件
        if (currentPiety <= minPiety)
            OnPietyReachedMin?.Invoke(currentPiety); // 達到最小值
        else if (currentPiety >= maxPiety)
            OnPietyReachedMax?.Invoke(currentPiety); // 達到最大值

        // 在 Console 中輸出變更記錄，方便除錯
        Debug.Log($"虔誠值變更為: {currentPiety}");
    }

    /// <summary>
    /// 增加虔誠值
    /// </summary>
    /// <param name="amount">要增加的數量</param>
    public void AddPiety(float amount)
    {
        SetPiety(currentPiety + amount);
    }

    /// <summary>
    /// 減少虔誠值
    /// </summary>
    /// <param name="amount">要減少的數量</param>
    public void SubtractPiety(float amount)
    {
        SetPiety(currentPiety - amount);
    }

    /// <summary>
    /// 重置虔誠值到初始值 (90)
    /// </summary>
    public void ResetPiety()
    {
        SetPiety(90f);
    }

    // ===== 以下是供 Dialogue System 的 Lua 使用的函數 =====

    /// <summary>
    /// Lua 函數 - 獲取當前虔誠值
    /// 在對話中可以用 GetPiety() 來獲取
    /// </summary>
    public double GetPiety()
    {
        return currentPiety;
    }

    /// <summary>
    /// Lua 函數 - 設定虔誠值
    /// 在對話中可以用 SetPiety(50) 來設定
    /// </summary>
    public void SetPiety(double value)
    {
        SetPiety((float)value);
    }

    /// <summary>
    /// Lua 函數 - 增加虔誠值
    /// 在對話中可以用 AddPiety(10) 來增加
    /// </summary>
    public void AddPiety(double amount)
    {
        AddPiety((float)amount);
    }

    /// <summary>
    /// Lua 函數 - 減少虔誠值
    /// 在對話中可以用 SubtractPiety(5) 來減少
    /// </summary>
    public void SubtractPiety(double amount)
    {
        SubtractPiety((float)amount);
    }

    // ===== 保存/載入系統整合 =====

    void OnEnable()
    {
        // 註冊到 Dialogue System 的數據持久化系統
        PersistentDataManager.RegisterPersistentData(gameObject);
    }

    void OnDisable()
    {
        // 從數據持久化系統中移除註冊
        PersistentDataManager.UnregisterPersistentData(gameObject);
    }

    /// <summary>
    /// 保存數據 - 返回要保存的虔誠值字符串
    /// </summary>
    public string GetData()
    {
        return currentPiety.ToString();
    }

    /// <summary>
    /// 載入數據 - 從保存的字符串中恢復虔誠值
    /// </summary>
    /// <param name="data">保存的數據字符串</param>
    public void ApplyData(string data)
    {
        if (float.TryParse(data, out float loadedPiety))
        {
            SetPiety(loadedPiety);
        }
    }
}
