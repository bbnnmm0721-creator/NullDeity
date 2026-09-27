using UnityEngine;
using UnityEngine.UI;

public class SymbolSlot : MonoBehaviour
{
    [SerializeField] private Image slotImage;
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color filledColor = new Color(1f, 1f, 1f, 0.8f);

    private int slotIndex;
    private DraggableSymbol currentSymbol;
    private int currentSymbolValue;

    public void Initialize(int index)
    {
        slotIndex = index;

        if (slotImage == null)
        {
            slotImage = GetComponent<Image>();
            if (slotImage == null)
            {
                Debug.LogError($"[SymbolSlot] {gameObject.name} 找不到 Image 組件！");
            }
        }

        ClearSlot();
        Debug.Log($"[SymbolSlot] {gameObject.name} 初始化完成，索引: {index}");
    }

    public void PlaceSymbol(DraggableSymbol symbol, int value)
    {
        currentSymbol = symbol;
        currentSymbolValue = value;

        if (slotImage != null)
        {
            slotImage.color = filledColor;
        }

        Debug.Log($"[SymbolSlot] {gameObject.name} 放置符號，值: {value}");
    }

    public void ClearSlot()
    {
        currentSymbol = null;
        currentSymbolValue = 0;

        if (slotImage != null)
        {
            slotImage.color = emptyColor;
        }
    }

    public bool HasSymbol => currentSymbol != null;
    public int CurrentSymbolValue => currentSymbolValue;
    public int SlotIndex => slotIndex;
}
