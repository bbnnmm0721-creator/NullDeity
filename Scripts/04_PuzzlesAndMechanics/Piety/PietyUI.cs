using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 虔誠值UI顯示器
/// 負責在介面上顯示當前的虔誠值
/// 支援進度條、文字和顏色變化顯示
/// </summary>
public class PietyUI : MonoBehaviour
{
    [Header("UI 元件")]
    [SerializeField] private Slider pietySlider;      // 虔誠值進度條 (可選)
    [SerializeField] private TMP_Text pietyText;      // 虔誠值文字顯示 (可選)
    [SerializeField] private Image pietyFillImage;    // 進度條填充圖片 (用於變色，可選)

    [Header("顏色設定")]
    [SerializeField] private Color highPietyColor = Color.white;   // 高虔誠值時的顏色
    [SerializeField] private Color mediumPietyColor = Color.yellow; // 中等虔誠值時的顏色
    [SerializeField] private Color lowPietyColor = Color.red;       // 低虔誠值時的顏色

    [Header("閾值設定")]
    [SerializeField] private float lowThreshold = 30f;    // 低虔誠值閾值
    [SerializeField] private float highThreshold = 70f;   // 高虔誠值閾值

    void OnEnable()
    {
        // 監聽虔誠值變化事件
        PietyManager.OnPietyChanged += UpdatePietyDisplay;
    }

    void OnDisable()
    {
        // 移除事件監聽
        PietyManager.OnPietyChanged -= UpdatePietyDisplay;
    }

    void Start()
    {
        // 初始化顯示當前虔誠值
        if (PietyManager.Instance != null)
        {
            UpdatePietyDisplay(PietyManager.Instance.CurrentPiety);
        }
    }

    /// <summary>
    /// 更新虔誠值的UI顯示
    /// 這個函數會在虔誠值改變時自動被呼叫
    /// </summary>
    /// <param name="newPiety">新的虔誠值</param>
    void UpdatePietyDisplay(float newPiety)
    {
        // 更新進度條 (如果有設定的話)
        if (pietySlider != null)
        {
            // 將虔誠值轉換為 0-1 的範圍給進度條使用
            pietySlider.value = newPiety / 100f;
        }

        // 更新文字顯示 (如果有設定的話)
        if (pietyText != null)
        {
            // 顯示格式: "虔誠值: 90/100"
            pietyText.text = $"{newPiety:F0}/100";
        }

        // 更新顏色 (如果有設定填充圖片的話)
        if (pietyFillImage != null)
        {
            pietyFillImage.color = GetPietyColor(newPiety);
        }
    }

    /// <summary>
    /// 根據虔誠值決定顯示顏色
    /// 低於30: 紅色警告
    /// 30-70: 黃色注意
    /// 高於70: 白色正常
    /// </summary>
    /// <param name="piety">當前虔誠值</param>
    /// <returns>對應的顏色</returns>
    Color GetPietyColor(float piety)
    {
        if (piety <= lowThreshold)
            return lowPietyColor;      // 低虔誠值 -> 紅色
        else if (piety >= highThreshold)
            return highPietyColor;     // 高虔誠值 -> 白色
        else
            return mediumPietyColor;   // 中等虔誠值 -> 黃色
    }
}
