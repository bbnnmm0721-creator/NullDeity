/* 2. 資料表 (ScriptableObject 方便在 Inspector 配) */
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> items;
    public ItemData Get(int id) => items.Find(i => i.id == id);
    /*
    public ItemData Get(int id)
    {
        var data = items.Find(i => i.id == id);
        if (data == null)
            Debug.LogError($"[ItemDB] id {id} NOT FOUND!");
        else if (data.icon == null)
            Debug.LogError($"[ItemDB] id {id} 找到但 icon 為 null！");
        return data;
    }
    */
}
