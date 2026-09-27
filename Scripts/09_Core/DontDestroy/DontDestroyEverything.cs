using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class DontDestroyEverything : MonoBehaviour
{
    [Header("Scope")]
    [Tooltip("勾選：把整棵 Root 一起常駐；不勾：只保留這個物件")]
    public bool useHierarchyRoot = true;

    [Header("Singleton (防重複)")]
    [Tooltip("勾選：同一 Key 只保留第一個，其餘自動刪除")]
    public bool enforceSingleton = true;
    [Tooltip("相同 Key 視為同一組單例。留空=使用 類型名@Root名")]
    public string key = "";

    [Header("UI 幫手（可關）")]
    [Tooltip("場景切換後若 Canvas 是 Screen Space - Camera，會自動把 worldCamera 指回 Camera.main")]
    public bool autoFixCanvasWorldCamera = true;

    GameObject _target;                     // 實際搬進 DDOL 的目標
    static HashSet<string> _aliveKeys = new HashSet<string>();

    void Awake()
    {
        _target = useHierarchyRoot ? transform.root.gameObject : gameObject;

        string k = string.IsNullOrEmpty(key) ? $"{GetType().FullName}@{_target.name}" : key;

        if (enforceSingleton)
        {
            if (_aliveKeys.Contains(k)) { Destroy(_target); return; } // 保留舊的
            _aliveKeys.Add(k);
        }

        DontDestroyOnLoad(_target);

        if (autoFixCanvasWorldCamera)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            FixCanvasCameras();
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        var tgt = _target ? _target : (useHierarchyRoot ? transform.root?.gameObject : gameObject);
        string k = string.IsNullOrEmpty(key) ? $"{GetType().FullName}@{(tgt ? tgt.name : "")}" : key;
        _aliveKeys.Remove(k);
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => FixCanvasCameras();

    void FixCanvasCameras()
    {
        if (!_target) return;
        foreach (var cv in _target.GetComponentsInChildren<Canvas>(true))
        {
            if (cv.renderMode == RenderMode.ScreenSpaceCamera &&
                (cv.worldCamera == null || !cv.worldCamera.isActiveAndEnabled))
            {
                cv.worldCamera = Camera.main;
            }
        }
    }
}
