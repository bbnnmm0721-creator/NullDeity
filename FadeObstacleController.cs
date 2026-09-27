using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects Foreground-layer renderers occluding the player via SphereCast,
/// then positions FadeZone sphere(s) at each hit point to drive stencil-based transparency.
/// </summary>
[DefaultExecutionOrder(1000)]
public class FadeObstacleController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    /// <summary>The FadeZone child GameObject that writes the stencil mask.</summary>
    public GameObject fadeZonePrefab;

    [Header("Occlusion Detection")]
    /// <summary>Sphere cast radius used to detect occluding objects between the camera and the player.</summary>
    [Min(0.01f)] public float castRadius = 0.35f;
    /// <summary>Layers considered for occlusion detection. Should only include Foreground.</summary>
    public LayerMask occlusionLayerMask;

    [Header("Fade Zone")]
    /// <summary>World-space diameter of the FadeZone sphere placed at each hit point.</summary>
    [Min(0.1f)] public float fadeZoneSize = 2.5f;

#if UNITY_EDITOR
    [Header("Debug")]
    public bool showDebugRay = true;

    Vector3 _dbgCam;
    Vector3 _dbgPlayer;
    int _dbgHitCount;
    bool _dbgReady;
    readonly Vector3[] _dbgHitPoints = new Vector3[32];
#endif

    // Pre-allocated cast buffer ¡X avoids per-frame allocation
    readonly RaycastHit[] hitBuffer = new RaycastHit[32];

    // Pool of FadeZone instances ¡X one per simultaneous occluder
    readonly List<GameObject> fadeZonePool = new();

    /* ------------------------------------------------------------------ */

    void Awake()
    {
        // Auto-exclude Player layer so the character never detects itself
        int playerLayer = LayerMask.NameToLayer("Player");
        if (occlusionLayerMask == 0)
            occlusionLayerMask = Physics.DefaultRaycastLayers;
        if (playerLayer >= 0)
            occlusionLayerMask &= ~(1 << playerLayer);

        // Seed the pool with the FadeZone that already exists in the scene
        if (fadeZonePrefab != null)
        {
            fadeZonePool.Add(fadeZonePrefab);
            fadeZonePrefab.SetActive(false);
        }
    }

    /* ------------------------------------------------------------------ */

    void LateUpdate()
    {
        if (!player) return;

        Camera cam = Camera.main;
        if (!cam) return;

        Vector3 camPos = cam.transform.position;
        Vector3 playerPos = player.position;
        Vector3 toPlayer = playerPos - camPos;
        float distance = toPlayer.magnitude;
        if (distance < 0.001f) return;

        Vector3 direction = toPlayer / distance;

        // SphereCast from camera toward player ¡X only hits Foreground layer
        int hitCount = Physics.SphereCastNonAlloc(
            camPos, castRadius, direction, hitBuffer, distance, occlusionLayerMask);

        // Grow the pool if more occluders are detected than available FadeZone instances
        while (fadeZonePool.Count < hitCount)
        {
            var clone = Instantiate(fadeZonePool[0], transform);
            clone.SetActive(false);
            fadeZonePool.Add(clone);
        }

        // Deactivate all FadeZone instances first
        foreach (var fz in fadeZonePool)
            fz.SetActive(false);

        // Place one FadeZone at each hit point with a fixed configurable size
        for (int i = 0; i < hitCount; i++)
        {
            // Skip hits without a Renderer (e.g., invisible colliders)
            if (hitBuffer[i].collider.GetComponent<Renderer>() == null) continue;

            GameObject fz = fadeZonePool[i];
            fz.transform.position = hitBuffer[i].point;          // exact surface hit point
            fz.transform.localScale = Vector3.one * fadeZoneSize;  // fixed sphere size
            fz.SetActive(true);

#if UNITY_EDITOR
            if (showDebugRay && i < _dbgHitPoints.Length)
                _dbgHitPoints[i] = hitBuffer[i].point;
#endif
        }

#if UNITY_EDITOR
        if (showDebugRay)
        {
            _dbgCam = camPos;
            _dbgPlayer = playerPos;
            _dbgHitCount = hitCount;
            _dbgReady = true;
            Debug.DrawLine(camPos, playerPos, hitCount > 0 ? Color.yellow : Color.green);
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>Visualizes the SphereCast ray and FadeZone placement in the Scene view.</summary>
    void OnDrawGizmos()
    {
        if (!showDebugRay || !_dbgReady) return;

        // Main ray
        Gizmos.color = _dbgHitCount > 0 ? Color.yellow : Color.green;
        Gizmos.DrawLine(_dbgCam, _dbgPlayer);

        // Camera-side sphere showing cast radius
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawSphere(_dbgCam, castRadius);

        // Sphere at each hit point ¡X matches the FadeZone size placed at runtime
        for (int i = 0; i < _dbgHitCount; i++)
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.35f);
            Gizmos.DrawSphere(_dbgHitPoints[i], fadeZoneSize * 0.5f);

            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.Label(_dbgHitPoints[i] + Vector3.up * (fadeZoneSize * 0.5f + 0.1f),
                                      hitBuffer[i].collider.name);
        }
    }
#endif
}
