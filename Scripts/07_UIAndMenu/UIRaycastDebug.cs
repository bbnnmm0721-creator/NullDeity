using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastDebug : MonoBehaviour
{
    PointerEventData ped;

    void Awake()
    {
        if (!EventSystem.current) Debug.LogError("【UIRaycastDebug】場景缺 EventSystem！");
        ped = new PointerEventData(EventSystem.current);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ped.position = Input.mousePosition;

            // 1) 全域：把所有 Raycaster 的命中都列出來
            var all = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, all);
            Debug.Log("=== RaycastAll (ALL canvases) ===");
            if (all.Count == 0) Debug.Log("（無命中，可能是沒有 raycastTarget 或被別的 UI 蓋住）");
            foreach (var r in all)
                Debug.Log($"{r.gameObject.name}  (from {r.module})");

            // 2) 逐一列每個 GraphicRaycaster 的命中
            var rays = FindObjectsOfType<GraphicRaycaster>(true);
            foreach (var gr in rays)
            {
                var list = new List<RaycastResult>();
                gr.Raycast(ped, list);

                // 取得這個 Raycaster 所在 Canvas 的名稱
                var c = gr.GetComponent<Canvas>();
                string canvasName = c ? c.name : "(no Canvas)";
                Debug.Log($"--- {gr.name} in Canvas [{canvasName}] --- hits: {list.Count}");

                foreach (var r in list)
                    Debug.Log($"  - {r.gameObject.name}");
            }
        }
    }
}
