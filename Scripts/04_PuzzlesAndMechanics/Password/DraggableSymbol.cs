using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DraggableSymbol : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image symbolImage;

    private int symbolValue;
    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private SymbolPuzzleManager puzzleManager;
    private SymbolSlot currentSlot;
    private bool isDraggable = true;
    private Vector2 offset;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvas = GetComponentInParent<Canvas>();
        originalPosition = rectTransform.anchoredPosition;
    }

    public void Initialize(int value, Sprite sprite, SymbolPuzzleManager manager)
    {
        symbolValue = value;
        if (symbolImage != null)
        {
            symbolImage.sprite = sprite;
        }
        else
        {
            Debug.LogError($"[DraggableSymbol] {gameObject.name} 的 Symbol Image 未设置！");
        }
        puzzleManager = manager;

        Debug.Log($"[DraggableSymbol] {gameObject.name} 初始化完成 - 值: {value}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isDraggable) return;

        if (currentSlot != null)
        {
            currentSlot.ClearSlot();
            currentSlot = null;
        }

        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;

        RectTransform parentRect = rectTransform.parent as RectTransform;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        offset = rectTransform.anchoredPosition - localPoint;

        Debug.Log($"[DraggableSymbol] 開始拖拽 {gameObject.name}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggable) return;

        RectTransform parentRect = rectTransform.parent as RectTransform;
        Vector2 localPoint;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint))
        {
            rectTransform.anchoredPosition = localPoint + offset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggable) return;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (puzzleManager == null)
        {
            Debug.LogError("[DraggableSymbol] PuzzleManager 是 null！無法處理拖拽");
            ResetPosition();
            return;
        }

        SymbolSlot nearestSlot = puzzleManager.FindNearestSlot(rectTransform.position);

        if (nearestSlot != null && !nearestSlot.HasSymbol)
        {
            Debug.Log($"[DraggableSymbol] 放置到槽位: {nearestSlot.name}");
            PlaceInSlot(nearestSlot);
        }
        else
        {
            Debug.Log($"[DraggableSymbol] 沒有合適的槽位，返回原位");
            ResetPosition();
        }
    }

    private void PlaceInSlot(SymbolSlot slot)
    {
        currentSlot = slot;
        slot.PlaceSymbol(this, symbolValue);
        rectTransform.position = slot.transform.position;
    }

    public void ResetPosition()
    {
        if (currentSlot != null)
        {
            currentSlot.ClearSlot();
            currentSlot = null;
        }

        rectTransform.anchoredPosition = originalPosition;
    }

    public void SetInteractable(bool interactable)
    {
        isDraggable = interactable;
        canvasGroup.alpha = interactable ? 1f : 0.5f;
    }

    public int SymbolValue => symbolValue;
}
