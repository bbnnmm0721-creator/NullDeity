using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
public class PencilPickupInteraction : MonoBehaviour
{
    [Header("游標設定")]
    public Sprite pencilCursorSprite;
    public Vector2 pencilCursorSize = new Vector2(64f, 128f);
    public Vector2 pencilHotspot = new Vector2(0.1f, 0.9f);

    [Header("顯示")]
    [Tooltip("RawImage，Texture 請指定顯示用的 .png（需開啟 Read/Write Enabled）")]
    public RawImage revealRawImage;
    [Tooltip("文字圖，第一次塗色就顯示，會疊在 RawImage 上面")]
    public Image textImage;
    [Tooltip("自訂筆刷紋理（需開啟 Read/Write Enabled）")]
    public Texture2D pencilBrushTexture;
    public Color pencilColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
    public float brushRadius = 20f;
    public float brushTiling = 3f;
    [Tooltip("塗抹比例超過此值時自動完成（0~1）")]
    public float completionThreshold = 0.9f;

    [Header("顯示用子頁面（值以外塗抹，RB 按鈕在限制頁面以外不可使用）")]
    public ManualPager manualPager;
    public int allowedSubPageIndex = 0;

    [Header("顯示期間隱藏的其他物件（Pen 以外）")]
    [Tooltip("persistent scene 中要隱藏的物件：EventBookPanel 上的 Image 等 GameObject、DecorateDown、DecorateUp（含 Pen）...")]
    public GameObject[] hideWhilePainting;

    [Header("顯示震動")]
    [Tooltip("低頻震動大小（0~1），沒有點擊時使用，建議設 0")]
    public float rumbleLow = 0f;
    [Tooltip("高頻震動大小（0~1），有塗抹時發生，建議設 0.2~0.35")]
    public float rumbleHigh = 0.25f;

    [Header("Scrollbar 捲動靈敏度（搖桿 / 滑鼠滾輪）")]
    [Tooltip("游標懸停在 ScrollRect 上時，右搖桿或滑鼠滾輪的捲動倍率")]
    public float scrollSensitivity = 400f;

    // ── 私有狀態 ──────────────────────────────────────────────

    private SoftwareCursorConfined softwareCursor;
    private Sprite originalCursorSprite;
    private Vector2 originalHotspot;
    private Vector2 originalCursorSize;

    private Image pencilImage;
    private bool isPencilHeld;
    private bool hasDrawn;
    private bool isCompleted;
    private bool _paintedThisFrame;

    private Texture2D revealTexture;
    private Color[] revealPixels;
    private Color[] sourcePixels;
    private Color[] brushPixels;
    private int srcW, srcH, brushW, brushH;
    private int totalPaintablePixels;
    private int paintedPixelCount;

    private Camera canvasCamera;

    // ── 懸停追蹤（修正黏住問題）─────────────────────────────
    private GameObject _lastHoveredObject;

    private const float AlphaThreshold = 0.01f;
    private const string LogTag = "[PencilPickup]";

    // ── Unity 生命週期 ────────────────────────────────────────

    void Awake()
    {
        pencilImage = GetComponent<Image>();
        softwareCursor = SoftwareCursorConfined.Instance;

        if (softwareCursor == null)
            Debug.LogWarning($"{LogTag} SoftwareCursorConfined.Instance is null in Awake!");
    }

    void OnEnable()
    {
        if (softwareCursor == null)
            softwareCursor = SoftwareCursorConfined.Instance;

        if (!isPencilHeld && softwareCursor != null)
        {
            originalCursorSprite = softwareCursor.cursorSprite;
            originalHotspot = softwareCursor.hotspotOffset;
            originalCursorSize = new Vector2(softwareCursor.cursorSize, softwareCursor.cursorSize);
            Debug.Log($"{LogTag} OnEnable: cursor backed up → {originalCursorSprite?.name}");
        }

        if (isPencilHeld)
        {
            Debug.Log($"{LogTag} OnEnable: forcing pencil back");
            RestoreCursorAppearance();
            softwareCursor?.SetCursorVisible(false);
            isPencilHeld = false;
        }

        StopRumble();
    }

    void OnDisable()
    {
        ClearHoverState();
        StopRumble();
    }

    void OnDestroy()
    {
        if (revealTexture != null) Destroy(revealTexture);
        StopRumble();
    }

    void Start()
    {
        RefreshCanvasCamera();
        InitReveal();
        SetAlpha(textImage, 0f);
    }

    void Update()
    {
        var gamepad = Gamepad.current;

        // RB / F 鍵：切換鉛筆
        bool penTogglePressed = gamepad != null
            ? gamepad.rightShoulder.wasPressedThisFrame
            : Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;

        if (penTogglePressed)
        {
            if (isPencilHeld) PutPencilBack();
            else PickUpPencil();
        }

        if (isPencilHeld)
        {
            // ★ 每幀更新懸停狀態，確保 Button 的 Enter/Exit 正確派送
            UpdateHoverState();

            // A / 滑鼠左鍵：點擊 UI
            bool clickPressed = gamepad != null
                ? gamepad.buttonSouth.wasPressedThisFrame
                : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (clickPressed)
                TryClickAtCursorPosition();

            // 右搖桿 / 滑鼠滾輪：捲動 ScrollRect
            Vector2 scrollInput = Vector2.zero;
            if (gamepad != null)
                scrollInput = gamepad.rightStick.ReadValue();
            else if (Mouse.current != null)
                scrollInput = Mouse.current.scroll.ReadValue() * 0.01f;

            if (scrollInput.sqrMagnitude > 0.01f)
                TryScrollAtCursorPosition(scrollInput);
        }

        // 繪製
        _paintedThisFrame = false;
        if (isPencilHeld && !isCompleted && IsOnAllowedSubPage() && softwareCursor != null)
            Paint(softwareCursor.ScreenPosition);

        // 震動
        if (isPencilHeld)
            SetRumble(_paintedThisFrame ? rumbleLow : 0f, _paintedThisFrame ? rumbleHigh : 0f);
    }

    // ── 鉛筆控制 ─────────────────────────────────────────────

    private void PickUpPencil()
    {
        if (softwareCursor == null) return;

        SetAlpha(pencilImage, 0f);
        isPencilHeld = true;
        ShowDecorations(true);

        if (pencilCursorSprite != null) softwareCursor.SetCursorSprite(pencilCursorSprite);
        softwareCursor.SetCursorSize(pencilCursorSize);
        softwareCursor.SetCursorHotspot(pencilHotspot);
        softwareCursor.SetCursorVisible(true);
    }

    /// <summary>放回鉛筆，還原游標狀態。</summary>
    public void PutPencilBack()
    {
        SetAlpha(pencilImage, 1f);
        isPencilHeld = false;
        ShowDecorations(false);
        RestoreCursorAppearance();
        softwareCursor?.SetCursorVisible(false);

        // ★ 收筆時清除懸停與選取狀態，避免殘留 highlight
        ClearHoverState();
        EventSystem.current?.SetSelectedGameObject(null);

        StopRumble();
    }

    private void RestoreCursorAppearance()
    {
        if (softwareCursor == null) return;
        softwareCursor.SetCursorSprite(originalCursorSprite);
        softwareCursor.SetCursorSize(originalCursorSize);
        softwareCursor.SetCursorHotspot(originalHotspot);
    }

    // ── 控制器 ───────────────────────────────────────────────

    private void SetRumble(float low, float high)
    {
        var gamepad = Gamepad.current;
        if (gamepad == null) return;
        gamepad.SetMotorSpeeds(low, high);
    }

    private void StopRumble() => SetRumble(0f, 0f);

    // ── 懸停管理 ─────────────────────────────────────────────

    /// <summary>
    /// 每幀追蹤游標下的 UI 物件，正確派送 pointerEnter / pointerExit，
    /// 防止 Button 的 hover 狀態殘留（黏住問題）。
    /// </summary>
    private void UpdateHoverState()
    {
        if (softwareCursor == null) return;
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        var pointerData = new PointerEventData(eventSystem)
        {
            position = softwareCursor.ScreenPosition
        };

        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        GameObject hitObject = results.Count > 0 ? results[0].gameObject : null;

        if (_lastHoveredObject == hitObject) return;

        // 對離開的物件送 Exit
        if (_lastHoveredObject != null)
            ExecuteEvents.ExecuteHierarchy(
                _lastHoveredObject, pointerData, ExecuteEvents.pointerExitHandler);

        _lastHoveredObject = hitObject;

        // 對新進入的物件送 Enter
        if (hitObject != null)
            ExecuteEvents.ExecuteHierarchy(
                hitObject, pointerData, ExecuteEvents.pointerEnterHandler);
    }

    /// <summary>強制清除目前懸停狀態（收筆、OnDisable 時呼叫）。</summary>
    private void ClearHoverState()
    {
        if (_lastHoveredObject == null) return;
        var eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            var exitData = new PointerEventData(eventSystem);
            ExecuteEvents.ExecuteHierarchy(
                _lastHoveredObject, exitData, ExecuteEvents.pointerExitHandler);
        }
        _lastHoveredObject = null;
    }

    // ── 點擊 / 捲動 ──────────────────────────────────────────

    /// <summary>
    /// 在游標位置模擬完整的 UI 點擊流程（Down → Up → Click）。
    /// 只要 raycast 打到任何 UI 物件就視為命中，不會穿透到背景。
    /// </summary>
    private void TryClickAtCursorPosition()
    {
        if (softwareCursor == null) return;
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        var pointerData = new PointerEventData(eventSystem)
        {
            position = softwareCursor.ScreenPosition,
            button = PointerEventData.InputButton.Left
        };

        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        if (results.Count == 0) return;

        pointerData.pointerPressRaycast = results[0];
        pointerData.eligibleForClick = true;

        // Down：沿 hierarchy 往上找第一個 IPointerDownHandler
        var pressTarget = ExecuteEvents.ExecuteHierarchy(
            results[0].gameObject, pointerData, ExecuteEvents.pointerDownHandler);
        pointerData.pointerPress = pressTarget;

        // pressTarget 為 null 代表點到無互動的空白區域，直接放棄
        if (pressTarget == null) return;

        // ★ Up / Click 精確打到 pressTarget（不用 ExecuteHierarchy，避免往上爬誤觸父物件）
        ExecuteEvents.Execute(pressTarget, pointerData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(pressTarget, pointerData, ExecuteEvents.pointerClickHandler);

        // ★ 點擊後立即清除 selected，避免 Button 殘留 highlight 黏住
        eventSystem.SetSelectedGameObject(null);
    }

    /// <summary>在游標位置發送捲動事件，驅動 ScrollRect。</summary>
    private void TryScrollAtCursorPosition(Vector2 scrollDelta)
    {
        if (softwareCursor == null) return;
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        var pointerData = new PointerEventData(eventSystem)
        {
            position = softwareCursor.ScreenPosition,
            scrollDelta = scrollDelta * scrollSensitivity
        };

        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (ExecuteEvents.ExecuteHierarchy(
                    result.gameObject, pointerData, ExecuteEvents.scrollHandler))
                break;
        }
    }

    // ── 子頁面 / 裝飾 ────────────────────────────────────────

    private bool IsOnAllowedSubPage()
    {
        if (manualPager == null) return true;
        if (allowedSubPageIndex < 0 || allowedSubPageIndex >= manualPager.subPages.Count) return false;
        var page = manualPager.subPages[allowedSubPageIndex];
        return page != null && page.activeInHierarchy;
    }

    /// <summary>顯示或隱藏裝飾物件。</summary>
    public void ShowDecorations(bool visible)
    {
        if (hideWhilePainting == null) return;
        foreach (var go in hideWhilePainting)
            if (go != null) go.SetActive(visible);
    }

    // ── Canvas 相機 ───────────────────────────────────────────

    private void RefreshCanvasCamera()
    {
        if (revealRawImage != null)
            canvasCamera = revealRawImage.GetComponentInParent<Canvas>()?.worldCamera;
    }

    // ── 塗抹初始化 ───────────────────────────────────────────

    private void InitReveal()
    {
        if (revealRawImage == null) { Debug.LogWarning($"{LogTag} InitReveal: revealRawImage is null!"); return; }

        var sourceTex = revealRawImage.texture as Texture2D;
        if (sourceTex == null) { Debug.LogWarning($"{LogTag} InitReveal: texture null or not Texture2D!"); return; }

        try { sourcePixels = sourceTex.GetPixels(); }
        catch (Exception e) { Debug.LogWarning($"{LogTag} InitReveal: enable Read/Write! ({e.Message})"); return; }

        srcW = sourceTex.width;
        srcH = sourceTex.height;

        totalPaintablePixels = 0;
        foreach (var p in sourcePixels)
            if (p.a > AlphaThreshold) totalPaintablePixels++;

        paintedPixelCount = 0;

        if (pencilBrushTexture != null)
        {
            try
            {
                brushPixels = pencilBrushTexture.GetPixels();
                brushW = pencilBrushTexture.width;
                brushH = pencilBrushTexture.height;
            }
            catch { Debug.LogWarning($"{LogTag} InitReveal: brushTexture needs Read/Write!"); }
        }

        revealTexture = new Texture2D(srcW, srcH, TextureFormat.RGBA32, false);
        revealPixels = new Color[srcW * srcH];

        for (int i = 0; i < revealPixels.Length; i++) revealPixels[i] = Color.clear;

        revealTexture.SetPixels(revealPixels);
        revealTexture.Apply();
        revealRawImage.texture = revealTexture;

        Debug.Log($"{LogTag} InitReveal: done, paintable={totalPaintablePixels}");
    }

    // ── 塗抹繪製 ─────────────────────────────────────────────

    private void Paint(Vector2 screenPos)
    {
        if (revealTexture == null) return;

        var rect = revealRawImage.rectTransform;
        bool hit = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect, screenPos, canvasCamera, out var local);

        if (!hit) return;

        var uv = new Vector2(
            local.x / rect.rect.width + 0.5f,
            local.y / rect.rect.height + 0.5f);

        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return;

        int cx = Mathf.RoundToInt(uv.x * srcW);
        int cy = Mathf.RoundToInt(uv.y * srcH);
        int r = Mathf.RoundToInt(brushRadius);
        int rSq = r * r;

        bool dirty = false;

        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > rSq) continue;

                int px = Mathf.Clamp(cx + dx, 0, srcW - 1);
                int py = Mathf.Clamp(cy + dy, 0, srcH - 1);
                int idx = py * srcW + px;

                if (sourcePixels[idx].a <= AlphaThreshold) continue;

                bool isNewPixel = revealPixels[idx].a <= AlphaThreshold;

                float grain = 1f;
                if (brushPixels != null)
                {
                    int bx = Mathf.Abs(Mathf.RoundToInt(px * brushTiling)) % brushW;
                    int by = Mathf.Abs(Mathf.RoundToInt(py * brushTiling)) % brushH;
                    grain = brushPixels[by * brushW + bx].r;
                }

                revealPixels[idx] = new Color(
                    pencilColor.r * grain,
                    pencilColor.g * grain,
                    pencilColor.b * grain,
                    pencilColor.a);

                dirty = true;

                if (isNewPixel)
                {
                    paintedPixelCount++;
                    _paintedThisFrame = true;
                }
            }
        }

        if (!dirty) return;

        if (!hasDrawn) { hasDrawn = true; SetAlpha(textImage, 1f); }

        revealTexture.SetPixels(revealPixels);
        revealTexture.Apply();

        float progress = totalPaintablePixels > 0
            ? (float)paintedPixelCount / totalPaintablePixels
            : 0f;

        if (!isCompleted && progress >= completionThreshold)
        {
            isCompleted = true;
            PutPencilBack();
        }
    }

    // ── 公開重置 ─────────────────────────────────────────────

    /// <summary>清除塗抹進度，重新開始。</summary>
    public void ResetReveal()
    {
        if (revealTexture == null) return;

        for (int i = 0; i < revealPixels.Length; i++) revealPixels[i] = Color.clear;

        revealTexture.SetPixels(revealPixels);
        revealTexture.Apply();

        paintedPixelCount = 0;
        hasDrawn = false;
        isCompleted = false;

        SetAlpha(textImage, 0f);
    }

    // ── 工具 ─────────────────────────────────────────────────

    private static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        var c = g.color; c.a = a; g.color = c;
    }
}
