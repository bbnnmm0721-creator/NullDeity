using PixelCrushers.DialogueSystem.SequencerCommands;
using UnityEngine;

/// <summary>
/// 用法：ScreenFade(stay, 6, #FFFFFF) → 淡到指定顏色並停留
///       ScreenFade(unstay, 3)         → 從目前顏色淡回透明
/// </summary>
public class SequencerCommandScreenFade : SequencerCommand
{
    void Awake()
    {
        string mode = GetParameter(0);
        float duration = GetParameterAsFloat(1, 1f);
        string hexColor = GetParameter(2);

        if (mode == "stay")
        {
            Color color = Color.white;
            if (!string.IsNullOrEmpty(hexColor))
                ColorUtility.TryParseHtmlString(hexColor, out color);
            GlobalSceneTransition.FadeToColor(color, duration);
        }
        else if (mode == "unstay")
        {
            GlobalSceneTransition.FadeFromColor(duration);
        }

        Stop();
    }
}

/// <summary>
/// 用法：GoToScene(Chapter1-1, Default) → 切換場景並保持黑幕
/// </summary>
public class SequencerCommandGoToScene : SequencerCommand
{
    void Awake()
    {
        string scene = GetParameter(0);
        string spawnId = GetParameter(1);

        if (string.IsNullOrEmpty(spawnId)) spawnId = "Default";

        if (!string.IsNullOrEmpty(scene))
            GlobalSceneTransition.GoStayBlack(scene, spawnId);
        else
            Debug.LogWarning("[GoToScene] 沒有指定場景名稱");

        Stop();
    }
}
