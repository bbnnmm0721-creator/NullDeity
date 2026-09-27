using UnityEngine;
using UnityEngine.UIElements;

public class UITKKeepAlive : MonoBehaviour
{
    void Awake()
    {
        // ① 整棵 UIRoot 常駐
        DontDestroyOnLoad(transform.root.gameObject);

        // ② 保險：root 拉滿
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;
        root.StretchToParentSize();
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;

        // ③ 保險：若 PanelSettings 遺失，自己補一份
        if (doc.panelSettings == null)
        {
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            //ps.clearColor = Color.clear;
            ps.scaleMode = PanelScaleMode.ConstantPixelSize;
            doc.panelSettings = ps;
        }
    }
}
