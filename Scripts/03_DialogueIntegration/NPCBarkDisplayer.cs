using PixelCrushers.DialogueSystem;
using UnityEngine;

public class NPCBarkDisplayer : MonoBehaviour
{
    void OnBarkLine(Subtitle subtitle)
    {
        if (ScreenSpaceBarkUI.Instance == null) return;
        ScreenSpaceBarkUI.Instance.Bark(subtitle);
    }
}
