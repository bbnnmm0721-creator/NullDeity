using UnityEngine;

public class CameraDebugger : MonoBehaviour
{
    [Header("調試資訊")]
    public SideScrollCamera sideScrollCamera;

    void Start()
    {
        if (sideScrollCamera == null)
            sideScrollCamera = GetComponent<SideScrollCamera>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            LogDebugInfo();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            ForceRebind();
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            LogCameraSettings();
        }

        if (Input.GetKeyDown(KeyCode.F4))
        {
            ForceJumpToTarget();
        }
    }

    void LogDebugInfo()
    {
        Debug.Log("=== Camera Debug Info ===");
        Debug.Log($"SideScrollCamera Target: {sideScrollCamera.target}");
        Debug.Log($"PlayerMovement.Instance: {PlayerMovement.Instance}");

        var playerObjects = GameObject.FindGameObjectsWithTag("Player");
        Debug.Log($"Found {playerObjects.Length} objects with Player tag:");
        foreach (var obj in playerObjects)
        {
            Debug.Log($"- {obj.name} at {obj.transform.position}");
        }

        var allCharacters = GameObject.FindObjectsOfType<PlayerMovement>();
        Debug.Log($"Found {allCharacters.Length} PlayerMovement components:");
        foreach (var pm in allCharacters)
        {
            Debug.Log($"- {pm.name} at {pm.transform.position}");
        }
    }

    void LogCameraSettings()
    {
        Debug.Log("=== SideScrollCamera Settings ===");
        Debug.Log($"Target: {(sideScrollCamera.target ? sideScrollCamera.target.name + " at " + sideScrollCamera.target.position : "NULL")}");
        Debug.Log($"Camera Position: {transform.position}");
        Debug.Log($"Offset: {sideScrollCamera.offset}");
        Debug.Log($"Min X: {sideScrollCamera.minX}");
        Debug.Log($"Max X: {sideScrollCamera.maxX}");
        Debug.Log($"Min Y: {sideScrollCamera.minY}");
        Debug.Log($"Max Y: {sideScrollCamera.maxY}");
        Debug.Log($"Min Z: {sideScrollCamera.minZ}");
        Debug.Log($"Max Z: {sideScrollCamera.maxZ}");
        Debug.Log($"Smooth Time: {sideScrollCamera.smoothTime}");
        Debug.Log($"Player Tag: {sideScrollCamera.playerTag}");
        Debug.Log($"Retry Delay: {sideScrollCamera.retryDelay}");
        Debug.Log($"Component Enabled: {sideScrollCamera.enabled}");

        if (sideScrollCamera.target != null)
        {
            Vector3 targetPos = sideScrollCamera.target.position;
            Vector3 desiredPos = new Vector3(
                targetPos.x + sideScrollCamera.offset.x,
                targetPos.y + sideScrollCamera.offset.y,
                targetPos.z + sideScrollCamera.offset.z
            );

            float clampedX = Mathf.Clamp(desiredPos.x, sideScrollCamera.minX, sideScrollCamera.maxX);
            float clampedY = Mathf.Clamp(desiredPos.y, sideScrollCamera.minY, sideScrollCamera.maxY);
            float clampedZ = Mathf.Clamp(desiredPos.z, sideScrollCamera.minZ, sideScrollCamera.maxZ);

            Debug.Log($"Target Position: {targetPos}");
            Debug.Log($"Desired Camera Position: {desiredPos}");
            Debug.Log($"Clamped X: {clampedX} (Original: {desiredPos.x})");
            Debug.Log($"Clamped Y: {clampedY} (Original: {desiredPos.y})");
            Debug.Log($"Clamped Z: {clampedZ} (Original: {desiredPos.z})");

            if (clampedX != desiredPos.x)
            {
                Debug.LogWarning($"⚠️ X 軸被限制！Min: {sideScrollCamera.minX}, Max: {sideScrollCamera.maxX}");
            }
            if (clampedY != desiredPos.y)
            {
                Debug.LogWarning($"⚠️ Y 軸被限制！Min: {sideScrollCamera.minY}, Max: {sideScrollCamera.maxY}");
            }
            if (clampedZ != desiredPos.z)
            {
                Debug.LogWarning($"⚠️ Z 軸被限制！Min: {sideScrollCamera.minZ}, Max: {sideScrollCamera.maxZ}");
            }
        }
    }

    void ForceJumpToTarget()
    {
        if (sideScrollCamera.target != null)
        {
            Vector3 targetPos = sideScrollCamera.target.position;
            Vector3 newPos = new Vector3(
                targetPos.x + sideScrollCamera.offset.x,
                targetPos.y + sideScrollCamera.offset.y,
                targetPos.z + sideScrollCamera.offset.z
            );

            transform.position = newPos;
            Debug.Log($"強制跳轉到目標位置: {newPos}");
        }
        else
        {
            Debug.LogWarning("沒有目標可以跳轉！");
        }
    }

    void ForceRebind()
    {
        if (PlayerMovement.Instance != null)
        {
            sideScrollCamera.target = PlayerMovement.Instance.transform;
            Debug.Log($"強制綁定到 PlayerMovement.Instance: {PlayerMovement.Instance.name}");
        }
        else
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                sideScrollCamera.target = playerObj.transform;
                Debug.Log($"強制綁定到 Player tag 物件: {playerObj.name}");
            }
            else
            {
                Debug.LogWarning("找不到任何可綁定的目標！");
            }
        }
    }

    void OnGUI()
    {
        if (Application.isPlaying)
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 300));
            GUILayout.Label("=== Camera Debug ===");
            GUILayout.Label($"Target: {(sideScrollCamera.target ? sideScrollCamera.target.name : "NULL")}");
            GUILayout.Label($"PlayerMovement.Instance: {(PlayerMovement.Instance ? "Found" : "NULL")}");
            GUILayout.Label($"Camera Position: {transform.position}");
            GUILayout.Label($"Component Enabled: {sideScrollCamera.enabled}");

            if (sideScrollCamera.target != null)
            {
                GUILayout.Label($"Target Position: {sideScrollCamera.target.position}");
                float distance = Vector3.Distance(transform.position, sideScrollCamera.target.position);
                GUILayout.Label($"Distance to Target: {distance:F2}");
            }

            if (GUILayout.Button("Show Debug Info (F1)"))
                LogDebugInfo();

            if (GUILayout.Button("Force Rebind (F2)"))
                ForceRebind();

            if (GUILayout.Button("Check Camera Settings (F3)"))
                LogCameraSettings();

            if (GUILayout.Button("Force Jump to Target (F4)"))
                ForceJumpToTarget();

            GUILayout.EndArea();
        }
    }
}
