using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BookTabsUGUI : MonoBehaviour
{
    [Header("标签数组（需要与 pages 数组对应）")]
    public Button[] tabs;

    [Header("页面数组")]
    public GameObject[] pages;

    [Header("切换设定")]
    public int defaultPage = 0;
    public bool reserveTabSpace = true;
    public bool log = false;

    [Header("D-pad 导航")]
    [Tooltip("启用 D-pad 上/下切换页面")]
    public bool enableDpadNavigation = true;
    public float navigationCooldown = 0.2f;

    [Header("標籤音效")]
    public AudioClip tabSound;

    int _current = -1;
    bool _switching;
    float lastSwitchTime;
    float lastNavigateTime = -999f;
    AudioSource _audioSource;

    private PlayerInputActions inputActions;

    void Awake()
    {
        if (enableDpadNavigation)
            inputActions = new PlayerInputActions();

        for (int i = 0; i < tabs.Length; i++)
        {
            if (!tabs[i]) continue;
            int idx = i;
            tabs[i].onClick.AddListener(() =>
            {
                if (log) Debug.Log($"[BookTabsUGUI] Click Tab {idx}");
                SwitchTo(idx);
            });

            if (reserveTabSpace && !tabs[i].GetComponent<CanvasGroup>())
                tabs[i].gameObject.AddComponent<CanvasGroup>();

            if (log) Debug.Log($"[BookTabsUGUI] Tab {i} setup: {tabs[i].name}");
        }

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.loop = false;
    }

    void OnEnable()
    {
        if (enableDpadNavigation && inputActions != null)
            inputActions.Player.Enable();

        _current = -1;
        if (pages != null && pages.Length > 0)
            SwitchTo(Mathf.Clamp(defaultPage, 0, pages.Length - 1));
    }

    void OnDisable()
    {
        if (enableDpadNavigation && inputActions != null)
            inputActions.Player.Disable();
    }

    void OnDestroy()
    {
        inputActions?.Dispose();
    }

    void Update()
    {
        if (_switching && Time.time - lastSwitchTime > 5f)
        {
            Debug.LogWarning("[BookTabsUGUI] Switch operation timeout, forcing reset");
            _switching = false;
        }

        if (enableDpadNavigation && inputActions != null && gameObject.activeInHierarchy)
            HandleDpadNavigation();
    }

    void HandleDpadNavigation()
    {
        if (Time.time - lastNavigateTime < navigationCooldown) return;

        if (inputActions.Player.NavigateUp.WasPressedThisFrame())
        {
            lastNavigateTime = Time.time;
            NavigatePrevious();
        }
        else if (inputActions.Player.NavigateDown.WasPressedThisFrame())
        {
            lastNavigateTime = Time.time;
            NavigateNext();
        }
    }

    void NavigatePrevious()
    {
        if (_current < 0) _current = 0;
        int targetPage = Mathf.Max(_current - 1, 0);
        if (log) Debug.Log($"[BookTabsUGUI] D-pad Up: Page {targetPage + 1}");
        SwitchTo(targetPage);
    }

    void NavigateNext()
    {
        if (_current < 0) _current = 0;
        int maxPage = pages != null ? pages.Length - 1 : 0;
        int targetPage = Mathf.Min(_current + 1, maxPage);
        if (log) Debug.Log($"[BookTabsUGUI] D-pad Down: Page {targetPage + 1}");
        SwitchTo(targetPage);
    }

    public void GoPage1() => SwitchTo(0);
    public void GoPage2() => SwitchTo(1);
    public void GoPage3() => SwitchTo(2);
    public void GoPage4() => SwitchTo(3);

    /// <summary>切換至指定分頁，-1 代表初始載入（不播音效）。</summary>
    public void SwitchTo(int index)
    {
        if (log) Debug.Log($"[BookTabsUGUI] SwitchTo called with index: {index}");

        if (_switching)
        {
            if (log) Debug.Log($"[BookTabsUGUI] Already switching, ignored (time: {Time.time - lastSwitchTime:F2}s ago)");
            return;
        }

        if (pages == null || pages.Length == 0)
        {
            Debug.LogError("[BookTabsUGUI] Pages array is null or empty!");
            return;
        }

        if (index < 0 || index >= pages.Length)
        {
            Debug.LogError($"[BookTabsUGUI] Index out of range: {index}, pages.Length: {pages.Length}");
            return;
        }

        if (_current == index)
        {
            if (log) Debug.Log($"[BookTabsUGUI] Already on page {index}");
            return;
        }

        // _current >= 0 代表是使用者觸發的切換（非初始 OnEnable 載入）
        bool playAudio = _current >= 0 && tabSound != null;

        lastSwitchTime = Time.time;
        if (log) Debug.Log($"[BookTabsUGUI] Starting switch from {_current} to {index}");
        StartCoroutine(SwitchDeferred(index, playAudio));
    }

    IEnumerator SwitchDeferred(int index, bool playAudio)
    {
        _switching = true;

        if (playAudio)
            _audioSource.PlayOneShot(tabSound);

        if (log) Debug.Log($"[BookTabsUGUI] SwitchDeferred started for index {index}");

        if (pages != null && pages.Length > 0)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null)
                {
                    pages[i].SetActive(i == index);
                    if (log) Debug.Log($"[BookTabsUGUI] Page {i} set to {i == index}");
                }
                else
                {
                    Debug.LogWarning($"[BookTabsUGUI] Page {i} is null!");
                }
            }
        }

        yield return null;

        if (tabs != null && tabs.Length > 0)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                var b = tabs[i];
                if (!b)
                {
                    Debug.LogWarning($"[BookTabsUGUI] Tab {i} is null!");
                    continue;
                }

                if (reserveTabSpace)
                {
                    var cg = b.GetComponent<CanvasGroup>();
                    if (!cg) cg = b.gameObject.AddComponent<CanvasGroup>();

                    bool isCurrentTab = (i == index);
                    cg.alpha = isCurrentTab ? 0f : 1f;
                    cg.interactable = !isCurrentTab;
                    cg.blocksRaycasts = !isCurrentTab;

                    if (log) Debug.Log($"[BookTabsUGUI] Tab {i}: alpha={cg.alpha}, interactable={cg.interactable}");

                    if (!b.gameObject.activeInHierarchy)
                    {
                        b.gameObject.SetActive(true);
                        if (log) Debug.Log($"[BookTabsUGUI] Activated tab {i} GameObject");
                    }
                }
                else
                {
                    b.gameObject.SetActive(i != index);
                    if (log) Debug.Log($"[BookTabsUGUI] Tab {i} GameObject set to {i != index}");
                }
            }
        }

        _current = index;
        _switching = false;

        if (log) Debug.Log($"[BookTabsUGUI] Switched to {index}, buttons updated, _switching reset to false");
    }

    [ContextMenu("强制重置状态")]
    public void ForceResetSwitching()
    {
        _switching = false;
        Debug.Log("[BookTabsUGUI] Forced reset _switching to false");
    }

    [ContextMenu("检查状态")]
    public void CheckState()
    {
        Debug.Log($"[BookTabsUGUI] Current state:");
        Debug.Log($"  - _switching: {_switching}");
        Debug.Log($"  - _current: {_current}");
        Debug.Log($"  - tabs.Length: {(tabs != null ? tabs.Length : -1)}");
        Debug.Log($"  - pages.Length: {(pages != null ? pages.Length : -1)}");
    }

    [ContextMenu("强制切换到Page1")]
    public void ForceGoPage1()
    {
        _switching = false;
        _current = -1;
        SwitchTo(0);
    }
}
