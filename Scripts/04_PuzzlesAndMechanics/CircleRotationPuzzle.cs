using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class CircleRotationPuzzle : MonoBehaviour
{
    [System.Serializable]
    public class CircleRing
    {
        [Header("圆环设置")]
        public Transform ringTransform;

        [Header("自定义旋转中心")]
        [Tooltip("如果设置，将围绕这个点旋转；留空则围绕物体自身中心")]
        public Transform customPivot;

        [Header("旋转轴")]
        [Tooltip("围绕哪个轴旋转（通常是 Y 轴）")]
        public RotationAxis rotationAxis = RotationAxis.Y;

        [Header("旋转限制")]
        [Tooltip("是否启用旋转限制（限制为±360度）")]
        public bool enableRotationLimit = true;

        [Header("初始状态")]
        [Tooltip("初始偏移角度（手动设置打乱值，0 = 不打乱）")]
        [Range(-360f, 360f)]
        public float initialOffset = 0f;

        [HideInInspector]
        public float recordedInitialAngle = 0f;

        [Header("正确角度")]
        [Tooltip("正确的旋转角度（相对于打乱后位置的偏移）")]
        public float correctAngle = 0f;

        [Header("容错范围")]
        [Tooltip("允许的误差角度")]
        [Range(0f, 45f)]
        public float tolerance = 5f;

        [Header("旋转设置")]
        [Tooltip("每次旋转的角度")]
        public float rotationStep = 30f;

        [Tooltip("旋转速度")]
        public float rotationSpeed = 5f;

        [HideInInspector]
        public float currentAngle = 0f;

        [HideInInspector]
        public float targetAngle = 0f;

        [HideInInspector]
        public bool isRotating;

        [HideInInspector]
        public Vector3 initialLocalPosition;

        [HideInInspector]
        public Quaternion initialLocalRotation;

        public float GetMinAngle()
        {
            return enableRotationLimit ? -360f : float.MinValue;
        }

        public float GetMaxAngle()
        {
            return enableRotationLimit ? 360f : float.MaxValue;
        }
    }

    public enum RotationAxis { X, Y, Z }

    [Header("圆环配置")]
    [Tooltip("从外到内的圆环列表（1-6 个）")]
    public List<CircleRing> rings = new List<CircleRing>();

    [Header("输入设置 - New Input System")]
    [Tooltip("使用 New Input System（推荐）")]
    public bool useNewInputSystem = true;

    [Header("输入设置 - Legacy（仅在未启用 New Input System 时使用）")]
    [Tooltip("选择下一个圆环的按键")]
    public KeyCode nextRingKey = KeyCode.Tab;

    [Tooltip("选择上一个圆环的按键")]
    public KeyCode previousRingKey = KeyCode.LeftShift;

    [Tooltip("顺时针旋转按键")]
    public KeyCode rotateClockwiseKey = KeyCode.D;

    [Tooltip("逆时针旋转按键")]
    public KeyCode rotateCounterClockwiseKey = KeyCode.A;

    [Header("初始化设置")]
    [Tooltip("开始时是否应用初始偏移")]
    public bool applyInitialOffsetOnStart = true;

    [Header("视觉反馈（Brightness 亮度）")]
    [Tooltip("未选中时的亮度")]
    [Range(0f, 5f)]
    public float normalBrightness = 1f;

    [Tooltip("选中时的亮度")]
    [Range(0f, 5f)]
    public float selectedBrightness = 2f;

    [Tooltip("正确时的亮度")]
    [Range(0f, 5f)]
    public float correctBrightness = 1.5f;

    [Tooltip("材质的 Brightness 属性名")]
    public string brightnessPropertyName = "_Brightness";

    [Tooltip("是否在解谜未激活时也显示正确状态")]
    public bool showCorrectWhenInactive = true;

    [Header("持久化设置")]
    [Tooltip("解谜完成后是否禁用整个 GameObject")]
    public bool disableOnComplete = true;

    [Tooltip("禁用前的延迟时间（秒）")]
    [Range(0f, 10f)]
    public float disableDelay = 3f;

    [Tooltip("唯一标识符（用于保存状态）")]
    public string puzzleID = "CircleRotationPuzzle_SecondFloor";

    [Header("解谜完成事件")]
    public UnityEvent onPuzzleSolved;

    [Header("调试")]
    public bool enableDebugLog = true;

    private int currentRingIndex = 0;
    private bool puzzleSolved = false;
    private bool isActive = false;
    private Dictionary<Transform, Material> originalMaterials = new Dictionary<Transform, Material>();
    private Dictionary<Transform, float> originalBrightness = new Dictionary<Transform, float>();
    private PlayerInputActions inputActions;

    void Awake()
    {
        if (useNewInputSystem)
        {
            inputActions = new PlayerInputActions();
        }
    }

    void OnEnable()
    {
        if (useNewInputSystem && inputActions != null)
        {
            inputActions.Player.Enable();
        }
    }

    void OnDisable()
    {
        if (useNewInputSystem && inputActions != null)
        {
            inputActions.Player.Disable();
        }
    }

    void Start()
    {
        if (IsAlreadySolved())
        {
            Log("✅ 解谜已在之前完成，跳过初始化");
            if (disableOnComplete)
            {
                gameObject.SetActive(false);
            }
            return;
        }

        if (rings.Count == 0)
        {
            Debug.LogError("[CircleRotationPuzzle] 没有配置任何圆环！");
            return;
        }

        foreach (var ring in rings)
        {
            if (ring.ringTransform != null)
            {
                ring.initialLocalPosition = ring.ringTransform.localPosition;
                ring.initialLocalRotation = ring.ringTransform.localRotation;

                float sceneAngle = GetSceneAngle(ring);

                if (applyInitialOffsetOnStart && ring.initialOffset != 0f)
                {
                    ring.recordedInitialAngle = sceneAngle + ring.initialOffset;
                    ring.currentAngle = ring.initialOffset;
                    ring.targetAngle = ring.initialOffset;

                    ApplyRotation(ring);

                    Log($"圆环初始化: 场景角度={sceneAngle:F1}°, 初始偏移={ring.initialOffset:F1}°, 记录角度={ring.recordedInitialAngle:F1}°");
                }
                else
                {
                    ring.recordedInitialAngle = sceneAngle;
                    ring.currentAngle = 0f;
                    ring.targetAngle = 0f;

                    Log($"圆环初始化: 场景角度={sceneAngle:F1}°（无偏移）");
                }

                Log($"  → 限制范围=[{ring.GetMinAngle():F1}° ~ {ring.GetMaxAngle():F1}°]");

                var renderer = ring.ringTransform.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    originalMaterials[ring.ringTransform] = new Material(renderer.material);

                    if (renderer.material.HasProperty(brightnessPropertyName))
                    {
                        originalBrightness[ring.ringTransform] = renderer.material.GetFloat(brightnessPropertyName);
                    }
                }
            }
        }

        UpdateVisuals();

        Log($"✅ 解谜初始化完成，共 {rings.Count} 个圆环");
    }

    float GetSceneAngle(CircleRing ring)
    {
        if (ring.ringTransform == null) return 0f;

        Vector3 euler = ring.ringTransform.eulerAngles;

        switch (ring.rotationAxis)
        {
            case RotationAxis.X:
                return NormalizeAngleSigned(euler.x);
            case RotationAxis.Y:
                return NormalizeAngleSigned(euler.y);
            case RotationAxis.Z:
                return NormalizeAngleSigned(euler.z);
            default:
                return 0f;
        }
    }

    void Update()
    {
        if (!isActive || puzzleSolved) return;

        if (useNewInputSystem && inputActions != null)
        {
            if (inputActions.Player.NavigateUp.WasPressedThisFrame())
            {
                SelectPreviousRing();
            }
            else if (inputActions.Player.NavigateDown.WasPressedThisFrame())
            {
                SelectNextRing();
            }

            if (inputActions.Player.NavigateLeft.WasPressedThisFrame())
            {
                RotateCurrentRing(true);
            }
            else if (inputActions.Player.NavigateRight.WasPressedThisFrame())
            {
                RotateCurrentRing(false);
            }
        }
        else
        {
            if (Input.GetKeyDown(nextRingKey))
            {
                SelectNextRing();
            }
            else if (Input.GetKeyDown(previousRingKey))
            {
                SelectPreviousRing();
            }

            if (Input.GetKeyDown(rotateClockwiseKey))
            {
                RotateCurrentRing(false);
            }
            else if (Input.GetKeyDown(rotateCounterClockwiseKey))
            {
                RotateCurrentRing(true);
            }
        }

        UpdateRotations();
        CheckPuzzleSolved();
    }

    public void ActivatePuzzle()
    {
        isActive = true;
        puzzleSolved = false;
        UpdateVisuals();
        Log("🎮 解谜已激活");
    }

    public void DeactivatePuzzle()
    {
        isActive = false;
        UpdateVisuals();
        Log("⏸️ 解谜已停用");
    }

    void SelectNextRing()
    {
        currentRingIndex = (currentRingIndex + 1) % rings.Count;
        UpdateVisuals();
        Log($"选择圆环: {currentRingIndex + 1}/{rings.Count} (D-pad 下)");
    }

    void SelectPreviousRing()
    {
        currentRingIndex--;
        if (currentRingIndex < 0) currentRingIndex = rings.Count - 1;
        UpdateVisuals();
        Log($"选择圆环: {currentRingIndex + 1}/{rings.Count} (D-pad 上)");
    }

    void RotateCurrentRing(bool counterClockwise)
    {
        if (currentRingIndex < 0 || currentRingIndex >= rings.Count) return;

        var ring = rings[currentRingIndex];
        if (ring.isRotating) return;

        float rotationAmount = ring.rotationStep * (counterClockwise ? 1 : -1);
        float newTargetAngle = ring.targetAngle + rotationAmount;

        if (ring.enableRotationLimit)
        {
            if (newTargetAngle < ring.GetMinAngle())
            {
                Log($"⚠️ 圆环 {currentRingIndex + 1} 已到达逆时针旋转限制 ({ring.GetMinAngle():F1}°)");
                return;
            }

            if (newTargetAngle > ring.GetMaxAngle())
            {
                Log($"⚠️ 圆环 {currentRingIndex + 1} 已到达顺时针旋转限制 ({ring.GetMaxAngle():F1}°)");
                return;
            }
        }

        ring.targetAngle = newTargetAngle;
        ring.isRotating = true;

        string direction = counterClockwise ? "逆时针 (D-pad 左)" : "顺时针 (D-pad 右)";
        Log($"旋转圆环 {currentRingIndex + 1} {direction} {ring.rotationStep}°，当前偏移: {ring.targetAngle:F1}°");
    }

    void UpdateRotations()
    {
        foreach (var ring in rings)
        {
            if (!ring.isRotating || ring.ringTransform == null) continue;

            float angleDelta = Mathf.DeltaAngle(ring.currentAngle, ring.targetAngle);

            if (Mathf.Abs(angleDelta) < 0.1f)
            {
                ring.currentAngle = ring.targetAngle;
                ApplyRotation(ring);
                ring.isRotating = false;
                UpdateVisuals();
            }
            else
            {
                ring.currentAngle += angleDelta * Time.deltaTime * ring.rotationSpeed;
                ApplyRotation(ring);
            }
        }
    }

    void ApplyRotation(CircleRing ring)
    {
        if (ring.ringTransform == null) return;

        ring.ringTransform.localPosition = ring.initialLocalPosition;
        ring.ringTransform.localRotation = ring.initialLocalRotation;

        Vector3 localAxis = GetLocalRotationAxis(ring);

        if (ring.customPivot != null)
        {
            Vector3 worldAxis = ring.ringTransform.TransformDirection(localAxis);
            ring.ringTransform.RotateAround(ring.customPivot.position, worldAxis, ring.currentAngle);
        }
        else
        {
            ring.ringTransform.Rotate(localAxis, ring.currentAngle, Space.Self);
        }
    }

    Vector3 GetLocalRotationAxis(CircleRing ring)
    {
        switch (ring.rotationAxis)
        {
            case RotationAxis.X:
                return Vector3.right;
            case RotationAxis.Y:
                return Vector3.up;
            case RotationAxis.Z:
                return Vector3.forward;
            default:
                return Vector3.up;
        }
    }

    void CheckPuzzleSolved()
    {
        if (puzzleSolved) return;

        bool allCorrect = true;

        foreach (var ring in rings)
        {
            if (!IsRingCorrect(ring))
            {
                allCorrect = false;
                break;
            }
        }

        if (allCorrect)
        {
            puzzleSolved = true;
            SavePuzzleState();
            Log("🎉 解谜完成！");
            onPuzzleSolved?.Invoke();

            if (disableOnComplete)
            {
                StartCoroutine(DisableAfterDelay());
            }
        }
    }

    bool IsRingCorrect(CircleRing ring)
    {
        if (ring.ringTransform == null) return false;

        float difference = Mathf.Abs(Mathf.DeltaAngle(ring.currentAngle, ring.correctAngle));

        return difference <= ring.tolerance;
    }

    void UpdateVisuals()
    {
        for (int i = 0; i < rings.Count; i++)
        {
            var ring = rings[i];
            if (ring.ringTransform == null) continue;

            var renderer = ring.ringTransform.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.material == null) continue;

            if (!renderer.material.HasProperty(brightnessPropertyName)) continue;

            float targetBrightness = normalBrightness;

            if (i == currentRingIndex && isActive)
            {
                targetBrightness = selectedBrightness;
            }
            else if (IsRingCorrect(ring))
            {
                if (isActive || showCorrectWhenInactive)
                {
                    targetBrightness = correctBrightness;
                }
            }

            renderer.material.SetFloat(brightnessPropertyName, targetBrightness);
        }
    }

    bool IsAlreadySolved()
    {
        return PlayerPrefs.GetInt(puzzleID + "_Solved", 0) == 1;
    }

    void SavePuzzleState()
    {
        PlayerPrefs.SetInt(puzzleID + "_Solved", 1);
        PlayerPrefs.Save();
        Log($"💾 解谜状态已保存: {puzzleID}");
    }

    IEnumerator DisableAfterDelay()
    {
        Log($"⏱️ 将在 {disableDelay} 秒后禁用解谜 GameObject");
        yield return new WaitForSeconds(disableDelay);
        Log("🔒 禁用解谜 GameObject");
        gameObject.SetActive(false);
    }

    [ContextMenu("重置解谜状态（删除存档）")]
    public void ResetPuzzleState()
    {
        PlayerPrefs.DeleteKey(puzzleID + "_Solved");
        PlayerPrefs.Save();
        Log($"🗑️ 解谜状态已重置: {puzzleID}");
    }

    float NormalizeAngle(float angle)
    {
        angle = angle % 360f;
        if (angle < 0) angle += 360f;
        return angle;
    }

    float NormalizeAngleSigned(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    void Log(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"<color=orange>[CircleRotationPuzzle]</color> {message}");
        }
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Dispose();
        }

        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null)
            {
                var renderer = kvp.Key.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = kvp.Value;
                }
            }
        }

        foreach (var kvp in originalBrightness)
        {
            if (kvp.Key != null)
            {
                var renderer = kvp.Key.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    if (renderer.material.HasProperty(brightnessPropertyName))
                    {
                        renderer.material.SetFloat(brightnessPropertyName, kvp.Value);
                    }
                }
            }
        }
    }

    [ContextMenu("重置到初始偏移")]
    public void ResetToInitialOffset()
    {
        foreach (var ring in rings)
        {
            if (ring.ringTransform != null)
            {
                ring.currentAngle = ring.initialOffset;
                ring.targetAngle = ring.initialOffset;
                ApplyRotation(ring);
            }
        }

        puzzleSolved = false;
        UpdateVisuals();
        Log("🔄 已重置到初始偏移位置");
    }

    [ContextMenu("解决谜题")]
    public void SolvePuzzle()
    {
        foreach (var ring in rings)
        {
            if (ring.ringTransform != null)
            {
                ring.currentAngle = ring.correctAngle;
                ring.targetAngle = ring.correctAngle;
                ApplyRotation(ring);
            }
        }

        CheckPuzzleSolved();
        UpdateVisuals();
    }

    void OnDrawGizmos()
    {
        if (rings == null || rings.Count == 0) return;

        for (int i = 0; i < rings.Count; i++)
        {
            var ring = rings[i];
            if (ring.ringTransform == null) continue;

            if (ring.customPivot != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(ring.customPivot.position, 0.1f);
                Gizmos.DrawLine(ring.customPivot.position, ring.ringTransform.position);
            }

#if UNITY_EDITOR
            Vector3 labelPos = ring.ringTransform.position + Vector3.up * 0.5f;
            string status = IsRingCorrect(ring) ? "✓" : "✗";
            string limitInfo = ring.enableRotationLimit ?
                $"\n限制: [{ring.GetMinAngle():F0}° ~ {ring.GetMaxAngle():F0}°]" : "";

            UnityEditor.Handles.Label(labelPos,
                $"圆环 {i + 1} {status}\n" +
                $"初始偏移: {ring.initialOffset:F0}°\n" +
                $"当前偏移: {ring.currentAngle:F0}°\n" +
                $"正确偏移: {ring.correctAngle:F0}°" +
                limitInfo);
#endif
        }
    }
}
