using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RedPaperData", menuName = "Book/RedPaper Data")]
public class RedPaperData : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("紙張上顯示的文字（Text A）")]
        public string textA;

        [Tooltip("點擊後中央顯示的句子（Text B），每個元素為一行")]
        [TextArea(2, 5)]
        public string[] linesB;
    }

    public Entry[] entries;

    /// <summary>安全取得指定 index 的資料，超出範圍時回傳 null。</summary>
    public Entry Get(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length) return null;
        return entries[index];
    }
}
