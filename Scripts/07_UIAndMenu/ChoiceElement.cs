using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChoiceElement : VisualElement
{
    /* 讓它能在 UXML 裡直接 `<ChoiceElement name="btn1"/>` */
    public new class UxmlFactory : UxmlFactory<ChoiceElement> { }

    /** ▶ 讓外部能訂閱點擊 */
    public event System.Action clicked;

    public ChoiceElement()
    {
#if UNITY_EDITOR   // 只在 Editor 用 AssetDatabase，Build 時請改成預載入 Reference
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                     "Assets/Resource/Book/Card.uxml");
        var root = tree.Instantiate();   // 把 Card.uxml 貼進來
        hierarchy.Add(root);
#else
        // build 時可改成從 Addressables / Resources 等載入
#endif

        /* 讓整塊元件可點；你也可以只取 root 內某個子結點再註冊 */
        this.RegisterCallback<ClickEvent>(_ => clicked?.Invoke());
    }
}
