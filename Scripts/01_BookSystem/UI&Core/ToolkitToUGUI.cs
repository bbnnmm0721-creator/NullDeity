using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class ToolkitChoiceToUGUI : MonoBehaviour
{
    [Header("UGUI 要顯示的頁面")]
    public GameObject page1, page2, page3, page4;

    [Header("ChoiceElement 的 name (UXML)")]
    public string btn1Name = "btn1";
    public string btn2Name = "btn2";
    public string btn3Name = "btn3";
    public string btn4Name = "btn4";

    /*──────── 生命週期 ────────*/

    void OnEnable()          // 每次 SetActive(true) 都會再呼叫一次
    {
        StartCoroutine(DeferredHook());
    }

    IEnumerator DeferredHook()
    {
        // ← 等 1 frame，保證 UIDocument 已把 VisualTree 建好
        yield return null;

        var root = GetComponent<UIDocument>().rootVisualElement;
        Debug.Log($"[ToolkitChoiceToUGUI] OnEnable hook, children={root.childCount}");

        Hook(btn1Name, page1);
        Hook(btn2Name, page2);
        Hook(btn3Name, page3);
        Hook(btn4Name, page4);

        // 每次打開都先顯示 Page1
        SwitchTo(page1);
    }

    /*──────── 綁定 & 切頁 ────────*/

    void Hook(string elementName, GameObject page)
    {
        var el = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>(elementName);
        if (el == null || page == null)
        {
            Debug.LogWarning($"[ToolkitChoiceToUGUI] 找不到 {elementName} 或 Page 未指派");
            return;
        }

        // 先移除舊的（避免重複綁定）
        el.UnregisterCallback<ClickEvent>(_ => SwitchTo(page));
        el.RegisterCallback<ClickEvent>(_ =>
        {
            Debug.Log($"CLICK {elementName} → {page.name}");
            SwitchTo(page);
        });
    }

    public void SwitchToFirstPage() => SwitchTo(page1);

    void SwitchTo(GameObject show)
    {
        if (!show) return;
        Debug.Log($"SwitchTo({show.name})");

        page1.SetActive(show == page1);
        page2.SetActive(show == page2);
        page3.SetActive(show == page3);
        page4.SetActive(show == page4);
    }
}
