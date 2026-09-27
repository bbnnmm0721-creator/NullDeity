using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class PaintingExamineController : MonoBehaviour
{
    [Header("Targets")]
    public Transform painting;

    [Header("Zoom Settings")]
    [Tooltip("0=最遠(看整幅), 1=最近(細部)")]
    [Range(0, 1)] public float zoom = 0f;
    [Tooltip("滾輪縮放速度（鍵鼠）")]
    public float mouseZoomSpeed = 0.15f;
    [Tooltip("右搖桿縮放速度（手柄）")]
    public float gamepadZoomSpeed = 1.5f;
    public float minFOV = 60f;
    public float maxFOV = 20f;

    [Header("Pan Control")]
    [Tooltip("滑鼠右鍵拖拽靈敏度")]
    public float mousePanSensitivity = 2f;
    [Tooltip("D-Pad 平移靈敏度（手柄）")]
    public float dpadPanSensitivity = 3f;
    [Tooltip("拖拽移動範圍限制")]
    public float panLimit = 5f;
    [Tooltip("是否在放大時啟用拖拽")]
    public bool enablePanWhenZoomed = true;
    [Tooltip("啟用拖拽的最小縮放比例")]
    [Range(0, 1)] public float minZoomForPan = 0.3f;

    [Header("Smooth Movement")]
    [Tooltip("相機位置平滑移動時間")]
    public float positionSmoothTime = 0.3f;
    [Tooltip("縮放平滑時間")]
    public float zoomSmoothTime = 0.2f;
    [Tooltip("是否啟用平滑移動")]
    public bool enableSmoothMovement = true;

    [Header("Boundary Constraints")]
    [Tooltip("啟用邊界限制")]
    public bool enableBoundary = true;
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -5f;
    public float maxY = 5f;
    [Tooltip("根據縮放級別動態調整邊界")]
    public bool dynamicBoundary = true;

    [Header("Inspect Gate")]
    [Tooltip("達到此縮放比例，啟用熱區點擊")]
    [Range(0, 1)] public float clearThreshold = 0.7f;
    public LayerMask hotspotMask;
    public UnityEvent onBecameInspectable;

    [Header("Enter/Exit")]
    public CanvasGroup fade;
    public float fadeDuration = 0.35f;

    [Header("Virtual Cursor - Gamepad Only")]
    [Tooltip("虛擬光標 RectTransform（手柄模式）")]
    public RectTransform virtualCursor;
    [Tooltip("左搖桿移動虛擬光標速度")]
    public float cursorSpeed = 800f;
    [Tooltip("虛擬光標移動範圍")]
    public Vector2 cursorBoundsMin = new Vector2(50, 50);
    public Vector2 cursorBoundsMax = new Vector2(-50, -50);

    [Header("Debug")]
    public bool enableDebug = false;

    private Vector3 positionVelocity;
    private Vector3 targetPosition;
    private Vector3 lastMousePosition;
    private Vector3 originalCameraPosition;
    private Vector3 panOffset = Vector3.zero;
    private float targetZoom = 0f;
    private float zoomVelocity = 0f;
    private Vector2 cursorPosition;
    private bool isDragging = false;
    private bool isUsingGamepad = false;
    private PlayerInputActions inputActions;

    Camera cam;
    bool inExamine, canInspect, firedInspectable;

    void Awake()
    {
        cam = GetComponent<Camera>();
        inputActions = new PlayerInputActions();

        if (!painting && enableDebug)
        {
            Debug.LogWarning("[PaintingExamineController] 請指定 painting 目標");
        }

        if (virtualCursor)
        {
            virtualCursor.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Enable();
        }
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }
    }

    public void EnterExamineMode()
    {
        inExamine = true;

        originalCameraPosition = transform.position;
        panOffset = Vector3.zero;
        targetPosition = originalCameraPosition;
        zoom = 0f;
        targetZoom = 0f;

        if (inputActions != null)
        {
            inputActions.Player.Enable();
        }

        DetectInputDevice();
        SetupCursor();

        if (fade) StartCoroutine(FadeTo(1f, fadeDuration));

        if (enableDebug) Debug.Log($"[PaintingExamineController] 進入檢查模式 - 輸入模式: {(isUsingGamepad ? "手柄" : "鍵鼠")}");
    }

    public void ExitExamineMode()
    {
        if (enableDebug) Debug.Log("[PaintingExamineController] ExitExamineMode 被調用");
        ExitExamineModeInternal();
        PaintingManager.Instance?.ExitPaintingMode();
    }

    public void ExitExamineModeWithoutCallback()
    {
        if (enableDebug) Debug.Log("[PaintingExamineController] ExitExamineModeWithoutCallback 被調用");

        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }

        inExamine = false;
        canInspect = false;
        firedInspectable = false;
        zoom = 0f;
        targetZoom = 0f;
        isDragging = false;

        transform.position = originalCameraPosition;
        targetPosition = originalCameraPosition;
        panOffset = Vector3.zero;

        ResetCursor();

        if (fade && gameObject.activeInHierarchy)
        {
            StartCoroutine(FadeTo(0f, fadeDuration));
        }
        else if (fade)
        {
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
            fade.gameObject.SetActive(false);
        }
    }

    private void ExitExamineModeInternal()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }

        inExamine = false;
        canInspect = false;
        firedInspectable = false;
        zoom = 0f;
        targetZoom = 0f;
        isDragging = false;

        transform.position = originalCameraPosition;
        targetPosition = originalCameraPosition;
        panOffset = Vector3.zero;

        ResetCursor();

        if (fade && gameObject.activeInHierarchy)
        {
            StartCoroutine(FadeTo(0f, fadeDuration));
        }
        else if (fade)
        {
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
            fade.gameObject.SetActive(false);
        }
    }

    void DetectInputDevice()
    {
        isUsingGamepad = Gamepad.current != null;
    }

    void SetupCursor()
    {
        if (isUsingGamepad)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            if (virtualCursor)
            {
                virtualCursor.gameObject.SetActive(true);
                cursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                virtualCursor.position = cursorPosition;
            }
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (virtualCursor)
            {
                virtualCursor.gameObject.SetActive(false);
            }
        }
    }

    void ResetCursor()
    {
        if (virtualCursor)
        {
            virtualCursor.gameObject.SetActive(false);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    System.Collections.IEnumerator FadeTo(float target, float dur)
    {
        float t = 0, start = fade.alpha;
        fade.blocksRaycasts = target > 0.5f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        fade.alpha = target;
    }

    void Update()
    {
        if (!inExamine) return;

        DetectInputDeviceSwitch();

        HandleZoomInput();
        HandlePanInput();

        if (isUsingGamepad)
        {
            HandleVirtualCursor();
        }

        UpdateCamera();

        bool nowInspectable = zoom >= clearThreshold;
        if (nowInspectable && !firedInspectable)
        {
            firedInspectable = true;
            onBecameInspectable?.Invoke();
        }
        canInspect = nowInspectable;

        bool clickHotspot = false;

        if (isUsingGamepad)
        {
            if (inputActions != null && inputActions.Player.Interact.WasPressedThisFrame())
            {
                clickHotspot = true;
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0) && !isDragging)
            {
                clickHotspot = true;
            }
        }

        if (canInspect && clickHotspot)
        {
            TryClickHotspot();
        }

        bool exitPressed = Input.GetKeyDown(KeyCode.Escape);

        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            exitPressed = true;
        }

        if (exitPressed)
        {
            ExitExamineMode();
        }
    }

    void DetectInputDeviceSwitch()
    {
        bool wasUsingGamepad = isUsingGamepad;

        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            isUsingGamepad = true;
        }
        else if (Mouse.current != null && (Mouse.current.delta.ReadValue().magnitude > 0.1f || Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame))
        {
            isUsingGamepad = false;
        }

        if (wasUsingGamepad != isUsingGamepad)
        {
            SetupCursor();
            if (enableDebug) Debug.Log($"[PaintingExamineController] 切換輸入模式: {(isUsingGamepad ? "手柄" : "鍵鼠")}");
        }
    }

    void HandleZoomInput()
    {
        float mouseWheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(mouseWheel) > 0.001f)
        {
            targetZoom = Mathf.Clamp01(targetZoom + mouseWheel * mouseZoomSpeed);
        }

        if (Gamepad.current != null)
        {
            float rightStickY = Gamepad.current.rightStick.y.ReadValue();

            if (Mathf.Abs(rightStickY) > 0.1f)
            {
                targetZoom += rightStickY * gamepadZoomSpeed * Time.deltaTime;
                targetZoom = Mathf.Clamp01(targetZoom);
            }
        }

        zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, zoomSmoothTime);
    }

    void HandlePanInput()
    {
        if (zoom < minZoomForPan || !enablePanWhenZoomed)
        {
            if (!isDragging && panOffset.magnitude > 0.01f)
            {
                panOffset = Vector3.Lerp(panOffset, Vector3.zero, Time.deltaTime * 5f);
            }
            return;
        }

        HandleMousePan();
        HandleDpadPan();
    }

    void HandleMousePan()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if (isDragging && Input.GetMouseButton(1))
        {
            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
            Vector3 worldDelta = cam.ScreenToWorldPoint(new Vector3(mouseDelta.x, mouseDelta.y, cam.nearClipPlane + 1f))
                                - cam.ScreenToWorldPoint(new Vector3(0, 0, cam.nearClipPlane + 1f));

            float currentSensitivity = mousePanSensitivity * Mathf.Lerp(0.5f, 1.5f, zoom);
            Vector3 panDelta = -worldDelta * currentSensitivity;
            panDelta.z = 0;

            panOffset += panDelta;
            panOffset = Vector3.ClampMagnitude(panOffset, panLimit);
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isDragging = false;
        }
    }

    void HandleDpadPan()
    {
        if (inputActions == null) return;

        Vector2 dpadInput = Vector2.zero;
        dpadInput.x = inputActions.Player.NavigateRight.ReadValue<float>() - inputActions.Player.NavigateLeft.ReadValue<float>();
        dpadInput.y = inputActions.Player.NavigateUp.ReadValue<float>() - inputActions.Player.NavigateDown.ReadValue<float>();

        if (dpadInput.magnitude > 0.1f)
        {
            Vector3 worldRight = cam.transform.right;
            Vector3 worldUp = cam.transform.up;
            worldRight.z = 0;
            worldUp.z = 0;
            worldRight.Normalize();
            worldUp.Normalize();

            Vector3 panDelta = (worldRight * dpadInput.x + worldUp * dpadInput.y) * dpadPanSensitivity * Time.deltaTime;
            panOffset += panDelta;
            panOffset = Vector3.ClampMagnitude(panOffset, panLimit);
        }
    }

    void HandleVirtualCursor()
    {
        if (!virtualCursor || !isUsingGamepad) return;

        Vector2 leftStick = Vector2.zero;

        if (inputActions != null)
        {
            leftStick = inputActions.Player.Move.ReadValue<Vector2>();
        }

        if (leftStick.magnitude > 0.1f)
        {
            cursorPosition += leftStick * cursorSpeed * Time.deltaTime;

            cursorPosition.x = Mathf.Clamp(cursorPosition.x, cursorBoundsMin.x, Screen.width + cursorBoundsMax.x);
            cursorPosition.y = Mathf.Clamp(cursorPosition.y, cursorBoundsMin.y, Screen.height + cursorBoundsMax.y);

            virtualCursor.position = cursorPosition;
        }
    }

    void UpdateCamera()
    {
        cam.fieldOfView = Mathf.Lerp(minFOV, maxFOV, Ease(zoom));

        CalculateTargetPosition();
        UpdateCameraPosition();
    }

    void CalculateTargetPosition()
    {
        Vector3 basePosition = originalCameraPosition;

        if (zoom >= minZoomForPan && enablePanWhenZoomed)
        {
            float zoomScale = Mathf.Lerp(1f, 0.5f, zoom);
            Vector3 scaledPanOffset = panOffset * zoomScale;
            targetPosition = basePosition + scaledPanOffset;
        }
        else
        {
            targetPosition = basePosition;
        }

        if (enableBoundary)
        {
            ApplyBoundaryConstraints();
        }
    }

    void ApplyBoundaryConstraints()
    {
        float currentMinX = minX;
        float currentMaxX = maxX;
        float currentMinY = minY;
        float currentMaxY = maxY;

        if (dynamicBoundary)
        {
            float boundaryScale = Mathf.Lerp(0.5f, 1.5f, zoom);
            currentMinX *= boundaryScale;
            currentMaxX *= boundaryScale;
            currentMinY *= boundaryScale;
            currentMaxY *= boundaryScale;
        }

        targetPosition.x = Mathf.Clamp(targetPosition.x,
            originalCameraPosition.x + currentMinX,
            originalCameraPosition.x + currentMaxX);

        targetPosition.y = Mathf.Clamp(targetPosition.y,
            originalCameraPosition.y + currentMinY,
            originalCameraPosition.y + currentMaxY);
    }

    void UpdateCameraPosition()
    {
        if (enableSmoothMovement && positionSmoothTime > 0f)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref positionVelocity,
                positionSmoothTime);
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    void TryClickHotspot()
    {
        Vector3 screenPoint = (isUsingGamepad && virtualCursor) ? (Vector3)cursorPosition : Input.mousePosition;
        Ray ray = cam.ScreenPointToRay(screenPoint);
        Debug.DrawRay(ray.origin, ray.direction * 200f, Color.red, 2f);

        RaycastHit[] hits = Physics.RaycastAll(ray, 200f, hotspotMask, QueryTriggerInteraction.Collide);

        foreach (var hit in hits)
        {
            var hs = hit.collider.GetComponent<PaintingHotspot>();
            if (hs)
            {
                if (enableDebug) Debug.Log($"點擊熱點: {hs.clueId}");
                hs.InvokePicked();
                return;
            }
        }

        if (enableDebug) Debug.Log("[PaintingExamineController] 未找到熱點");
    }

    static float Ease(float x) => Mathf.SmoothStep(0f, 1f, x);

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Dispose();
        }
    }
}
