using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SecondFloorDetector : MonoBehaviour
{
    StairSystem stairSystem;
    public bool enableDebugLogs = true;

    void Start()
    {
        var collider = GetComponent<BoxCollider>();
        collider.isTrigger = true;

        stairSystem = GetComponentInParent<StairSystem>();

        if (stairSystem == null)
        {
            Debug.LogError("[SecondFloorDetector] 找不到父物件的 StairSystem！");
        }
        else
        {
            DebugLog($"✅ 找到 StairSystem: {stairSystem.gameObject.name}");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        DebugLog($"🟦 OnTriggerEnter: {other.name} (Tag: {other.tag})");

        if (!other.CompareTag("Player"))
        {
            DebugLog("不是 Player，忽略");
            return;
        }

        if (stairSystem != null)
        {
            DebugLog("✅ 轉發到 StairSystem (進入二樓)");
            stairSystem.OnPlayerEnterSecondFloor();
        }
    }

    void OnTriggerExit(Collider other)
    {
        DebugLog($"🟦 OnTriggerExit: {other.name}");

        if (!other.CompareTag("Player")) return;

        if (stairSystem != null)
        {
            DebugLog("✅ 轉發到 StairSystem (離開二樓)");
            stairSystem.OnPlayerExitSecondFloor();
        }
    }

    void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"<color=blue>[SecondFloorDetector]</color> {message}");
    }

    void OnDrawGizmosSelected()
    {
        var collider = GetComponent<BoxCollider>();
        if (collider)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Gizmos.DrawCube(transform.position + collider.center, collider.size);

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position + collider.center, collider.size);
        }
    }
}
