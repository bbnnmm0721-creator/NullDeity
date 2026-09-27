using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ZoomZoneTrigger : MonoBehaviour
{
    [SerializeField] bool zoomOutOnEnter = true;
    [SerializeField] bool zoomInOnExit = true;

    void OnTriggerEnter(Collider other)
    {
        if (!other.GetComponent<CharacterController>()) return; // ¥u¦³ª±®a
        if (zoomOutOnEnter) SideScrollCamera.RequestZoom(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.GetComponent<CharacterController>()) return;
        if (zoomInOnExit) SideScrollCamera.RequestZoom(false);
    }
}
