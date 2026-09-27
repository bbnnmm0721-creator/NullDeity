using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// 教學引導（UI Toolkit 版）
public class BookTutorialCoach_UITK : MonoBehaviour
{
    [Header("References")]
    public UIDocument uiDoc;              // 你的 UIDocument（包含頁籤的那個）
    public Behaviour playerMovement;      // 角色移動元件（例如 PlayerMovement）
    public PanelAnim panelAnim;           // 你現有的書面板控制腳本（用來判斷開關）

    [Header("Target Names in UXML (Q by name)")]
    public string bookButtonName = "book";   // 書按鈕（若是 UGUI 可留空→只等 Q 開書）
    public string page1TabName = "btn1";
    public string page2TabName = "btn2";
    public string page3TabName = "btn3";
    public string page4TabName = "btn4";

    [Header("Overlay look")]
    [Range(0, 1)] public float dimOpacity = 0.75f;
    public Color outlineColor = new Color(1f, 1f, 1f, 0.95f);
    public float padding = 12f;

    // ───────────────────── internal
    VisualElement root;         // UIDocument 的 root
    VisualElement dimTop, dimBottom, dimLeft, dimRight, outline;
    bool running;
    [Header("Camera State Control")]
    public bool disableOnNonMainCamera = true;  // 🎯 NEW: 是否在非主相機時禁用

    private bool originalEnabledState;          // 🎯 NEW: 記住原始啟用狀態
    private bool isControlledByCameraState;     // 🎯 NEW: 是否被相機狀態控制


    void Reset()
    {
        uiDoc = GetComponent<UIDocument>();
    }

    void Awake()
    {
        if (!uiDoc) uiDoc = GetComponent<UIDocument>();
        root = uiDoc ? uiDoc.rootVisualElement : null;

        CreateOverlay();
        HideOverlay();

        if (playerMovement) playerMovement.enabled = false; // 鎖移動
        // 🎯 NEW: 註冊相機狀態監聽
        if (disableOnNonMainCamera)
        {
            originalEnabledState = enabled;

            if (CameraStateManager.Instance != null)
            {
                CameraStateManager.Instance.onCameraStateChanged.AddListener(OnCameraStateChanged);
                Debug.Log("[BookTutorialCoach_UITK] 已註冊相機狀態監聽");
            }
        }
    }
    void OnDestroy()
    {
        // 🎯 NEW: 取消註冊
        if (CameraStateManager.Instance != null)
        {
            CameraStateManager.Instance.onCameraStateChanged.RemoveListener(OnCameraStateChanged);
        }
    }
    /// <summary>
    /// 🎯 NEW: 相機狀態變更回調
    /// </summary>
    void OnCameraStateChanged(bool isMainCameraActive)
    {
        if (!disableOnNonMainCamera) return;

        if (isMainCameraActive)
        {
            // 主相機啟用 - 恢復原始狀態
            if (isControlledByCameraState)
            {
                enabled = originalEnabledState;
                isControlledByCameraState = false;
                Debug.Log("[BookTutorialCoach_UITK] 主相機啟用，恢復書籍面板");
            }
        }
        else
        {
            // 非主相機啟用 - 禁用面板
            if (!isControlledByCameraState)
            {
                originalEnabledState = enabled;
                enabled = false;
                isControlledByCameraState = true;
                Debug.Log("[BookTutorialCoach_UITK] 非主相機啟用，禁用書籍面板");
            }
        }
    }
    void OnEnable()
    {
        if (!running) StartCoroutine(CoRun());
    }

    // ──────────────────────────────────────────────
    IEnumerator CoRun()
    {
        running = true;

        // STEP 0：等 UIDocument 有 root
        while (root == null) { yield return null; }

        // 取頁籤元素（可為空→略過）
        var bookBtn = Query(bookButtonName);
        var tab1 = Query(page1TabName);
        var tab2 = Query(page2TabName);
        var tab3 = Query(page3TabName);
        var tab4 = Query(page4TabName);

        // STEP 1：引導打開書（若書按鈕在 UITK，就高亮；否則只等待 Q 開）
        if (bookBtn != null) { ShowHoleOn(bookBtn); BringOverlayToFront(); }
        // 等到面板真的開啟
        while (!(panelAnim && panelAnim.gameObject.activeInHierarchy)) yield return null;

        // STEP 2~5：依序導覽四個分頁
        yield return GuideClick(tab1);
        yield return GuideClick(tab2);
        yield return GuideClick(tab3);
        yield return GuideClick(tab4);

        // STEP 6：提示關書（把洞移回書按鈕；若沒有 UITK 按鈕，直接關遮罩並等關閉）
        if (bookBtn != null) { ShowHoleOn(bookBtn); BringOverlayToFront(); }
        while (panelAnim && panelAnim.gameObject.activeInHierarchy) yield return null;

        HideOverlay();
        if (playerMovement) playerMovement.enabled = true; // 解鎖
        running = false;
    }

    IEnumerator GuideClick(VisualElement target)
    {
        if (target == null) yield break;

        bool clicked = false;
        // 只要收到一次 ClickEvent 就往下
        void OnClicked(ClickEvent e) { clicked = true; }

        target.RegisterCallback<ClickEvent>(OnClicked);
        ShowHoleOn(target);
        BringOverlayToFront();

        // 等待點擊目標
        while (!clicked) yield return null;

        target.UnregisterCallback<ClickEvent>(OnClicked);
        yield return null;
    }

    // ───────────────────── Overlay（四片 + 外框）
    void CreateOverlay()
    {
        if (root == null) return;

        dimTop = NewBlock("dimTop");
        dimBottom = NewBlock("dimBottom");
        dimLeft = NewBlock("dimLeft");
        dimRight = NewBlock("dimRight");
        outline = new VisualElement { name = "outline" };
        outline.style.position = Position.Absolute;
        outline.style.borderTopColor = outlineColor;
        outline.style.borderRightColor = outlineColor;
        outline.style.borderBottomColor = outlineColor;
        outline.style.borderLeftColor = outlineColor;
        outline.style.borderTopWidth = 2;
        outline.style.borderRightWidth = 2;
        outline.style.borderBottomWidth = 2;
        outline.style.borderLeftWidth = 2;
        outline.pickingMode = PickingMode.Ignore;

        root.Add(dimTop);
        root.Add(dimBottom);
        root.Add(dimLeft);
        root.Add(dimRight);
        root.Add(outline);

        void ApplyDimStyle(VisualElement ve)
        {
            ve.style.position = Position.Absolute;
            ve.style.backgroundColor = new Color(0, 0, 0, dimOpacity);
            ve.pickingMode = PickingMode.Position; // 擋其他點擊
        }
        VisualElement NewBlock(string n)
        {
            var ve = new VisualElement { name = n };
            ApplyDimStyle(ve);
            return ve;
        }
    }

    void HideOverlay()
    {
        if (dimTop != null) dimTop.style.display = DisplayStyle.None;
        if (dimBottom != null) dimBottom.style.display = DisplayStyle.None;
        if (dimLeft != null) dimLeft.style.display = DisplayStyle.None;
        if (dimRight != null) dimRight.style.display = DisplayStyle.None;
        if (outline != null) outline.style.display = DisplayStyle.None;
    }

    void ShowHoleOn(VisualElement target)
    {
        if (target == null || root == null) return;

        // 先顯示
        dimTop.style.display = dimBottom.style.display =
        dimLeft.style.display = dimRight.style.display =
        outline.style.display = DisplayStyle.Flex;

        // 目標在 Panel 空間的座標
        var hole = target.worldBound;
        // 外擴 padding
        hole = new Rect(hole.x - padding, hole.y - padding, hole.width + padding * 2f, hole.height + padding * 2f);

        var full = root.worldBound;

        // 把 worldBound 轉成 root 區域的相對座標
        float L = 0, T = 0; // root 左上角在畫面（root 本身就是世界座標基準，直接減）
        Rect localHole = new Rect(hole.x - full.x, hole.y - full.y, hole.width, hole.height);

        // Top
        SetRect(dimTop, 0, 0, full.width, localHole.yMin);
        // Bottom
        SetRect(dimBottom, 0, localHole.yMax, full.width, full.height - localHole.yMax);
        // Left
        SetRect(dimLeft, 0, localHole.yMin, localHole.xMin, localHole.height);
        // Right
        SetRect(dimRight, localHole.xMax, localHole.yMin, full.width - localHole.xMax, localHole.height);

        // 外框
        SetRect(outline, localHole.xMin, localHole.yMin, localHole.width, localHole.height);

        void SetRect(VisualElement ve, float x, float y, float w, float h)
        {
            ve.style.left = x;
            ve.style.top = y;
            ve.style.width = w;
            ve.style.height = h;
        }
    }

    void BringOverlayToFront()
    {
        dimTop?.BringToFront();
        dimBottom?.BringToFront();
        dimLeft?.BringToFront();
        dimRight?.BringToFront();
        outline?.BringToFront();
    }


    VisualElement Query(string name)
    {
        if (string.IsNullOrEmpty(name) || root == null) return null;
        var ve = root.Q<VisualElement>(name);
        return ve;
    }
}
