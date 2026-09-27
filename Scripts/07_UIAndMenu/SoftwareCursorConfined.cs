using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class SoftwareCursorConfined : MonoBehaviour
{
    public static SoftwareCursorConfined Instance { get; private set; }

    [Header("游標設定")]
    public Sprite cursorSprite;

    [Header("顯示大小")]
    public float cursorSize = 128f;

    [Header("熱點偏移（0~1）")]
    public Vector2 hotspotOffset = new Vector2(0.5f, 0.5f);

    [Header("邊界限制")]
    public bool confineCursor = true;
    public int edgePadding = 10;

    [Header("系統游標")]
    public bool hideSystemCursor = true;

    [Header("游標預設顯示")]
    [Tooltip("遊戲啟動時是否顯示，通常設 false，由書本 / 壁畫 / 解謎手動開啟")]
    public bool visibleOnStart = false;

    [Header("搖桿靈敏度")]
    [Tooltip("左搖桿每秒移動的螢幕像素數（書本模式）")]
    public float stickSensitivity = 900f;

    [Header("Debug（開發用）")]
    public bool showHotspotIndicator = false;
    public Color hotspotIndicatorColor = Color.red;
    [Range(4f, 24f)]
    public float hotspotIndicatorSize = 10f;

    /// <summary>虛擬游標目前的螢幕像素座標，供 PencilPickupInteraction 等腳本讀取。</summary>
    public Vector2 ScreenPosition => _virtualPos;

    private Vector2 _virtualPos;
    private bool _cursorVisible;

    private Canvas cursorCanvas;
    private Image cursorImage;
    private RectTransform cursorRect;
    private RectTransform hotspotDotRect;

    // ── 生命週期 ──

    void Awake()
    {
        // Singleton + DontDestroyOnLoad，確保場景切換後仍然存在
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupCursor();
        HideSystemCursor();
        _cursorVisible = visibleOnStart;
        _virtualPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        ApplyCursorImageVisibility();
    }

    void Start() => HideSystemCursor();
    void OnEnable() => HideSystemCursor();

    void OnDisable() => Cursor.visible = true;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Cursor.visible = true;
        if (cursorCanvas != null) Destroy(cursorCanvas.gameObject);
    }

    void Update()
    {
        if (hideSystemCursor && Cursor.visible)
            Cursor.visible = false;

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.magnitude > 0.1f)
                _virtualPos += stick * stickSensitivity * Time.deltaTime;
        }
        else
        {
            _virtualPos = Input.mousePosition;
        }

        if (confineCursor)
        {
            _virtualPos.x = Mathf.Clamp(_virtualPos.x, edgePadding, Screen.width - edgePadding);
            _virtualPos.y = Mathf.Clamp(_virtualPos.y, edgePadding, Screen.height - edgePadding);
        }

        if (cursorRect != null)
            cursorRect.anchoredPosition = _virtualPos;
    }

    // ── 建立 Canvas 與 Image ──

    private void SetupCursor()
    {
        var canvasObj = new GameObject("CursorCanvas");
        cursorCanvas = canvasObj.AddComponent<Canvas>();
        cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cursorCanvas.sortingOrder = 1000;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        canvasObj.AddComponent<GraphicRaycaster>();
        // CursorCanvas 跟著 CursorManager 一起走，不需要另外標記
        canvasObj.transform.SetParent(transform, false);

        var imageObj = new GameObject("Cursor");
        imageObj.transform.SetParent(canvasObj.transform, false);

        cursorImage = imageObj.AddComponent<Image>();
        cursorImage.sprite = cursorSprite;
        cursorImage.raycastTarget = false;

        cursorRect = cursorImage.GetComponent<RectTransform>();
        cursorRect.sizeDelta = new Vector2(cursorSize, cursorSize);
        cursorRect.anchorMin = Vector2.zero;
        cursorRect.anchorMax = Vector2.zero;
        cursorRect.pivot = hotspotOffset;

        if (showHotspotIndicator)
            CreateHotspotIndicator();
    }

    private void CreateHotspotIndicator()
    {
        var dotObj = new GameObject("HotspotIndicator");
        dotObj.transform.SetParent(cursorRect, false);

        var dot = dotObj.AddComponent<Image>();
        dot.color = hotspotIndicatorColor;
        dot.raycastTarget = false;

        hotspotDotRect = dot.GetComponent<RectTransform>();
        hotspotDotRect.sizeDelta = new Vector2(hotspotIndicatorSize, hotspotIndicatorSize);
        hotspotDotRect.pivot = new Vector2(0.5f, 0.5f);
        UpdateHotspotIndicatorPosition(hotspotOffset);
    }

    private void UpdateHotspotIndicatorPosition(Vector2 pivot)
    {
        if (hotspotDotRect == null) return;
        hotspotDotRect.anchorMin = pivot;
        hotspotDotRect.anchorMax = pivot;
        hotspotDotRect.anchoredPosition = Vector2.zero;
    }

    private void HideSystemCursor()
    {
        if (!hideSystemCursor) return;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ApplyCursorImageVisibility()
    {
        if (cursorImage != null)
            cursorImage.gameObject.SetActive(_cursorVisible);
    }

    // ── 公開 API ──

    /// <summary>
    /// 控制游標 Image 顯示／隱藏。
    /// 設為 true 時重置虛擬位置到螢幕中央。
    /// </summary>
    public void SetCursorVisible(bool visible)
    {
        _cursorVisible = visible;
        if (visible)
        {
            _virtualPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (cursorRect != null)
                cursorRect.anchoredPosition = _virtualPos;
        }
        ApplyCursorImageVisibility();
    }
    /// <summary>游標熱點的螢幕座標（補償 pivot 偏移後的真實互動點）。</summary>
    public Vector2 HotspotScreenPosition
    {
        get
        {
            // _virtualPos 是 pivot 左下角；hotspotOffset 是 0~1 的 pivot 比例
            return _virtualPos + new Vector2(hotspotOffset.x * cursorSize,
                                             hotspotOffset.y * cursorSize);
        }
    }

    /// <summary>切換游標 Sprite</summary>
    public void SetCursorSprite(Sprite sprite)
    {
        cursorSprite = sprite;
        if (cursorImage != null) cursorImage.sprite = sprite;
    }

    /// <summary>設定游標等比尺寸</summary>
    public void SetCursorSize(float newSize)
    {
        cursorSize = newSize;
        if (cursorRect != null) cursorRect.sizeDelta = new Vector2(newSize, newSize);
    }
    
    /// <summary>設定游標寬高（分開控制）</summary>
    public void SetCursorSize(Vector2 size)
    {
        cursorSize = size.x;
        if (cursorRect != null) cursorRect.sizeDelta = size;
    }

    /// <summary>設定游標 Pivot 熱點（0~1）</summary>
    public void SetCursorHotspot(Vector2 pivot)
    {
        hotspotOffset = pivot;
        if (cursorRect != null) cursorRect.pivot = pivot;
        UpdateHotspotIndicatorPosition(pivot);
    }

    public void ShowSystemCursor(bool show)
    {
        hideSystemCursor = !show;
        Cursor.visible = show;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) HideSystemCursor();
    }
}
