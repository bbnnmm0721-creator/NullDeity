using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TransDropTarget : MonoBehaviour, IDropHandler
{
    public BookTabs page = BookTabs.Page4;
    public BookPageController controller;   // 指到 page4 的控制器
    Image img;

    void Awake()
    {
        img = GetComponent<Image>();
        if (!img) img = gameObject.AddComponent<Image>();
        img.raycastTarget = true;           // 要吃 Drop
    }

    public void OnDrop(PointerEventData eventData)
    {
        var go = eventData.pointerDrag;
        var drag = go ? go.GetComponent<DraggableItemIcon>() : null;

        if (!drag)
        {
            Debug.LogWarning("[Drop] 沒有 DraggableItemIcon，Drop 失敗");
            return;
        }

        Debug.Log($"[Drop] 收到 item={drag.ItemID} from {go.name} → 塞到 {name}");

        img.sprite = drag.IconSprite;
        img.enabled = (img.sprite != null);
        img.preserveAspect = true;
        transform.SetAsLastSibling();       // 保證在背景上面

        if (controller) controller.ShowRightInfo(drag.ItemID);
        else Debug.LogWarning("[Drop] 沒指定 BookPageController，無法更新右側文字");
    }
}
