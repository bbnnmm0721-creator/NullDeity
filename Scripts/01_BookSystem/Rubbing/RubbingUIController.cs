using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 拓印彈窗 UI 的核心控制器。Singleton + DontDestroyOnLoad，放在 persistent scene。
/// 負責顯示紙張面板、切換筆游標、像素拓印邏輯、震動回饋、完成收錄。
/// 開啟時自動鎖定主角移動與場景互動，並隱藏指定 HUD，關閉後自動還原。
/// </summary>
public class RubbingUIController : MonoBehaviour
{
    public static RubbingUIController Instance { get; private set; }

    /// <summary>拓印完成並收錄時觸發（帶 itemId）。RubbingInteractionSpot 訂閱以停用自身。</summary>
    public static event Action<int> OnRubbingCompleted;

    /// <summary>玩家按 B / Escape 取消時觸發（帶 itemId）。RubbingInteractionSpot 訂閱以恢復提示。</summary>
    public static event Action<int> OnRubbingCancelled;

    [Header("UI 元件")]
    [Tooltip("整個拓印 UI 的根 GameObject，開啟 / 關閉用")]
    public GameObject panel;

    [Tooltip("紙張 RawImage，拓印結果顯示在此")]
    public RawImage paperRawImage;

    [Tooltip("完成提示 GameObject（拓印達標後顯示，提示玩家按 A 收起）")]
    public GameObject completeHint;

    [Header("開啟時隱藏的 HUD")]
    [Tooltip("拓印 UI 開啟期間要隱藏的 GameObject（書、虔誠值條等），關閉後自動恢復")]
    public GameObject[] hideWhenOpen;

    [Header("筆游標")]
    public Sprite pencilCursorSprite;
    public Vector2 pencilCursorSize = new Vector2(64f, 128f);
    public Vector2 pencilHotspot = new Vector2(0.1f, 0.9f);

    [Header("拓印設定")]
    public Color pencilColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);

    [Range(1f, 150f)]
    public float brushRadius = 20f;

    [Range(0f, 1f)]
    [Tooltip("覆蓋率達此值時判定完成（0 ~ 1）")]
    public float completionThreshold = 0.85f;

    [Tooltip("筆刷紋路 Texture（需開 Read/Write），留空則用純圓形筆刷")]
    public Texture2D pencilBrushTexture;

    public float brushTiling = 3f;

    [Header("震動")]
    [Range(0f, 1f)] public float rumbleLow = 0f;
    [Range(0f, 1f)] public float rumbleHigh = 0.25f;

    // 游標備份
    private Sprite _originalCursorSprite;
    private Vector2 _originalCursorSize;
    private Vector2 _originalCursorHotspot;

    // 狀態
    private int _currentItemId;
    private bool _isOpen;
    private bool _isCompleted;
    private bool _paintedThisFrame;

    // 拓印紋理
    private Texture2D _revealTexture;
    private Color[] _revealPixels;
    private Color[] _sourcePixels;
    private Color[] _brushPixels;
    private int _srcW, _srcH, _brushW, _brushH;
    private int _totalPaintablePixels;
    private int _paintedPixelCount;
    private Camera _canvasCamera;

    private PlayerInputActions _inputActions;

    private const float AlphaThreshold = 0.01f;
    private const string LogTag = "[RubbingUI]";

    // ── 生命週期 ──

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _inputActions = new PlayerInputActions();

        if (panel) panel.SetActive(false);
        if (completeHint) completeHint.SetActive(false);
    }

    void OnEnable() => _inputActions.Player.Enable();
    void OnDisable() => _inputActions.Player.Disable();

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _inputActions?.Dispose();
        CleanupTexture();
    }

    void Update()
    {
        if (!_isOpen) return;

        var gamepad = Gamepad.current;

        // B / Escape：取消拓印，不收錄，解鎖主角
        bool cancel = gamepad != null
            ? gamepad.buttonEast.wasPressedThisFrame
            : Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (cancel && !_isCompleted)
        {
            Close(collected: false);
            return;
        }

        // 完成狀態下等待 A / 滑鼠左鍵確認 → 收錄並關閉
        if (_isCompleted)
        {
            bool confirm = gamepad != null
                ? gamepad.buttonSouth.wasPressedThisFrame
                : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (confirm) Close(collected: true);
            return;
        }

        // 拓印中：每幀 Paint
        _paintedThisFrame = false;
        var cursor = SoftwareCursorConfined.Instance;
        if (cursor != null) Paint(cursor.ScreenPosition);

        // 只在畫開新像素時才震動，且完成後不再震動
        if (!_isCompleted)
            SetRumble(_paintedThisFrame ? rumbleLow : 0f, _paintedThisFrame ? rumbleHigh : 0f);
    }

    // ── 公開 API ──

    /// <summary>開啟拓印 UI，鎖定主角，隱藏 HUD，載入指定 itemId 的底圖並顯示面板。</summary>
    public void Open(int itemId)
    {
        if (_isOpen) return;

        var item = InventoryManager.Inst?.database?.Get(itemId);
        if (item == null)
        {
            Debug.LogWarning($"{LogTag} Open: 找不到 itemId={itemId}");
            return;
        }

        if (item.rubbingSourceTexture == null)
        {
            Debug.LogWarning($"{LogTag} Open: itemId={itemId} 的 rubbingSourceTexture 為 null");
            return;
        }

        _currentItemId = itemId;
        _isOpen = true;
        _isCompleted = false;

        DialogueInputPriority.SetRubbingActive(true);

        BackupCursor();
        ApplyPencilCursor();
        InitReveal(item.rubbingSourceTexture);

        if (completeHint) completeHint.SetActive(false);
        if (panel) panel.SetActive(true);

        SetHudVisible(false);

        Debug.Log($"{LogTag} 已開啟，itemId={itemId}");
    }

    // ── 關閉 ──

    void Close(bool collected)
    {
        if (!_isOpen) return;

        _isOpen = false;

        DialogueInputPriority.SetRubbingActive(false);

        SetRumble(0f, 0f);
        RestoreCursor();

        SoftwareCursorConfined.Instance?.SetCursorVisible(false);

        if (panel) panel.SetActive(false);
        if (completeHint) completeHint.SetActive(false);

        SetHudVisible(true);

        if (collected && InventoryManager.Inst != null && !InventoryManager.Inst.Owns(_currentItemId))
        {
            InventoryManager.Inst.CollectRubbingSymbol(_currentItemId);
            OnRubbingCompleted?.Invoke(_currentItemId);
            Debug.Log($"{LogTag} 收錄 itemId={_currentItemId}");
        }
        else if (!collected)
        {
            OnRubbingCancelled?.Invoke(_currentItemId);
            Debug.Log($"{LogTag} 取消，itemId={_currentItemId}");
        }

        CleanupTexture();
    }

    // ── HUD 顯示控制 ──

    /// <summary>統一控制 hideWhenOpen 陣列中所有 HUD 的顯示狀態。</summary>
    void SetHudVisible(bool visible)
    {
        foreach (var hud in hideWhenOpen)
            if (hud) hud.SetActive(visible);
    }

    // ── 游標 ──

    void BackupCursor()
    {
        var c = SoftwareCursorConfined.Instance;
        if (c == null) return;
        _originalCursorSprite = c.cursorSprite;
        _originalCursorSize = new Vector2(c.cursorSize, c.cursorSize);
        _originalCursorHotspot = c.hotspotOffset;
    }

    void ApplyPencilCursor()
    {
        var c = SoftwareCursorConfined.Instance;
        if (c == null) return;
        if (pencilCursorSprite) c.SetCursorSprite(pencilCursorSprite);
        c.SetCursorSize(pencilCursorSize);
        c.SetCursorHotspot(pencilHotspot);
        c.SetCursorVisible(true);
    }

    void RestoreCursor()
    {
        var c = SoftwareCursorConfined.Instance;
        if (c == null) return;
        c.SetCursorSprite(_originalCursorSprite);
        c.SetCursorSize(_originalCursorSize);
        c.SetCursorHotspot(_originalCursorHotspot);
    }

    // ── 震動 ──

    void SetRumble(float low, float high)
    {
        Gamepad.current?.SetMotorSpeeds(low, high);
    }

    // ── 拓印邏輯 ──

    void InitReveal(Texture2D sourceTex)
    {
        CleanupTexture();

        try { _sourcePixels = sourceTex.GetPixels(); }
        catch (Exception e)
        {
            Debug.LogWarning($"{LogTag} InitReveal 失敗，請在 Texture Import Settings 開啟 Read/Write Enabled。({e.Message})");
            return;
        }

        _srcW = sourceTex.width;
        _srcH = sourceTex.height;

        _totalPaintablePixels = 0;
        foreach (var p in _sourcePixels)
            if (p.a > AlphaThreshold) _totalPaintablePixels++;
        _paintedPixelCount = 0;

        if (pencilBrushTexture != null)
        {
            try
            {
                _brushPixels = pencilBrushTexture.GetPixels();
                _brushW = pencilBrushTexture.width;
                _brushH = pencilBrushTexture.height;
            }
            catch { Debug.LogWarning($"{LogTag} pencilBrushTexture 需開啟 Read/Write！"); }
        }

        _revealTexture = new Texture2D(_srcW, _srcH, TextureFormat.RGBA32, false);
        _revealPixels = new Color[_srcW * _srcH];
        for (int i = 0; i < _revealPixels.Length; i++) _revealPixels[i] = Color.clear;
        _revealTexture.SetPixels(_revealPixels);
        _revealTexture.Apply();

        if (paperRawImage)
        {
            paperRawImage.texture = _revealTexture;
            _canvasCamera = paperRawImage.GetComponentInParent<Canvas>()?.worldCamera;
        }

        Debug.Log($"{LogTag} InitReveal 完成，可繪製像素={_totalPaintablePixels}");
    }

    void Paint(Vector2 screenPos)
    {
        if (_revealTexture == null || paperRawImage == null) return;

        var rect = paperRawImage.rectTransform;
        bool hit = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect, screenPos, _canvasCamera, out var local);
        if (!hit) return;

        var uv = new Vector2(
            local.x / rect.rect.width + 0.5f,
            local.y / rect.rect.height + 0.5f);
        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return;

        int cx = Mathf.RoundToInt(uv.x * _srcW);
        int cy = Mathf.RoundToInt(uv.y * _srcH);
        int r = Mathf.RoundToInt(brushRadius);
        int rSq = r * r;
        bool dirty = false;

        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > rSq) continue;

                int px = Mathf.Clamp(cx + dx, 0, _srcW - 1);
                int py = Mathf.Clamp(cy + dy, 0, _srcH - 1);
                int idx = py * _srcW + px;

                if (_sourcePixels[idx].a <= AlphaThreshold) continue;

                bool isNewPixel = _revealPixels[idx].a <= AlphaThreshold;

                float grain = 1f;
                if (_brushPixels != null)
                {
                    int bx = Mathf.Abs(Mathf.RoundToInt(px * brushTiling)) % _brushW;
                    int by = Mathf.Abs(Mathf.RoundToInt(py * brushTiling)) % _brushH;
                    grain = _brushPixels[by * _brushW + bx].r;
                }

                _revealPixels[idx] = new Color(
                    pencilColor.r * grain,
                    pencilColor.g * grain,
                    pencilColor.b * grain,
                    pencilColor.a);

                if (isNewPixel)
                {
                    _paintedPixelCount++;
                    _paintedThisFrame = true;
                }
                dirty = true;
            }
        }

        if (!dirty) return;

        _revealTexture.SetPixels(_revealPixels);
        _revealTexture.Apply();

        float progress = _totalPaintablePixels > 0
            ? (float)_paintedPixelCount / _totalPaintablePixels : 0f;

        if (!_isCompleted && progress >= completionThreshold)
        {
            _isCompleted = true;
            SetRumble(0f, 0f);
            if (completeHint) completeHint.SetActive(true);
            Debug.Log($"{LogTag} 拓印完成！覆蓋率 {progress:P0}，等待玩家按 A 收起");
        }
    }

    void CleanupTexture()
    {
        if (_revealTexture) { Destroy(_revealTexture); _revealTexture = null; }
        _revealPixels = null;
        _sourcePixels = null;
        _brushPixels = null;
    }
}
