using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class StairTriggerForwarder : MonoBehaviour
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
            Debug.LogError("[StairTriggerForwarder] 找不到父物件的 StairSystem！");
        }
        else
        {
            DebugLog($"✅ 找到 StairSystem: {stairSystem.gameObject.name}");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        DebugLog($"🟢 OnTriggerEnter: {other.name} (Tag: {other.tag})");

        if (!other.CompareTag("Player"))
        {
            DebugLog("不是 Player，忽略");
            return;
        }

        if (stairSystem != null)
        {
            DebugLog("✅ 轉發到 StairSystem");
            stairSystem.OnPlayerEnterZone();
        }
    }

    void OnTriggerExit(Collider other)
    {
        DebugLog($"🔴 OnTriggerExit: {other.name}");

        if (!other.CompareTag("Player")) return;

        if (stairSystem != null)
        {
            DebugLog("✅ 轉發到 StairSystem");
            stairSystem.OnPlayerExitZone();
        }
    }

    void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"<color=yellow>[TriggerForwarder]</color> {message}");
    }

    void OnDrawGizmosSelected()
    {
        var collider = GetComponent<BoxCollider>();
        if (collider)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawCube(transform.position + collider.center, collider.size);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + collider.center, collider.size);
        }
    }
}
