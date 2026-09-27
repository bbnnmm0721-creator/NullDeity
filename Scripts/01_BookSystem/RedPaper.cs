using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RedPaper : MonoBehaviour
{
    [SerializeField] private RedPaperData data;
    [SerializeField] private int entryIndex;
    [SerializeField] private BookPageController controller;

    [Tooltip("紙張上顯示 Text A 的 TMP_Text")]
    [SerializeField] private TMP_Text textALabel;

    void Awake()
    {
        var entry = data?.Get(entryIndex);
        if (entry == null)
        {
            Debug.LogWarning($"[RedPaper] {name}：entry index {entryIndex} 不存在");
            return;
        }

        if (textALabel != null)
            textALabel.text = entry.textA;

        var btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        var entry = data?.Get(entryIndex);
        if (entry == null || controller == null) return;
        controller.ShowRedPaperLines(entry.linesB);
    }
}
