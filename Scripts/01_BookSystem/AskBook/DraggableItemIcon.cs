using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableItemIcon : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int ItemID;
    public Sprite IconSprite;
    public Image icon;                // 這格的 Image（要能 raycast）
    public Canvas dragCanvas;         // 幽靈圖要放的 Canvas

    RectTransform ghost;
    CanvasGroup cg;
    ScrollRect parentScroll;          // ★ 新增：父層 ScrollRect

    public void Init(int id, Sprite spr, Canvas canvas)
    {
        ItemID = id;
        IconSprite = spr;
        dragCanvas = canvas;
    }

    void Awake()
    {
        if (!icon) icon = GetComponentInChildren<Image>(true);
        if (icon) icon.raycastTarget = true;

        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();

        parentScroll = GetComponentInParent<ScrollRect>(); // ★ 找到父層 ScrollRect
    }

    public void OnPointerDown(PointerEventData e)
    {
        Debug.Log($"[Drag] PointerDown on {name}  icon={(icon ? icon.name : "<null>")}  sprite={(IconSprite ? IconSprite.name : "<null>")}");
    }

    public void OnBeginDrag(PointerEventData e)
    {
        Debug.Log($"[Drag] Begin item={ItemID} on {name}");
        if (!dragCanvas) { Debug.LogWarning("[Drag] dragCanvas 未指定"); return; }
        if (!IconSprite) { Debug.LogWarning("[Drag] 沒有 IconSprite 無法拖"); return; }

        if (parentScroll) parentScroll.enabled = false; // ★ 暫停 ScrollRect
        cg.blocksRaycasts = false;                      // 自己不要擋 Drop

        // 建幽靈圖
        ghost = new GameObject("ghost", typeof(RectTransform)).GetComponent<RectTransform>();
        ghost.SetParent(dragCanvas.transform, false);
        var img = ghost.gameObject.AddComponent<Image>();
        img.sprite = IconSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        UpdateGhostPos(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (ghost) UpdateGhostPos(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        Debug.Log($"[Drag] End item={ItemID} dropOn={e.pointerCurrentRaycast.gameObject?.name}");
        if (parentScroll) parentScroll.enabled = true; // ★ 恢復 ScrollRect

        cg.blocksRaycasts = true;
        if (ghost) Destroy(ghost.gameObject);
        ghost = null;
    }

    void UpdateGhostPos(PointerEventData e)
    {
        var canvasRT = dragCanvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, e.position, e.pressEventCamera, out var lp);
        ghost.anchoredPosition = lp;
    }
}
