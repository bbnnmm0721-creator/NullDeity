using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ManualPager : MonoBehaviour
{
    public List<GameObject> subPages;
    public Button prevBtn;
    public Button nextBtn;
    public int startIndex = 0;

    [Header("D-pad 导航")]
    [Tooltip("启用 D-pad 左/右切换 SubPage")]
    public bool enableDpadNavigation = true;
    public float navigationCooldown = 0.2f;

    [Header("翻頁音效")]
    [Tooltip("翻頁時播放的音效")]
    public AudioClip pageTurnClip;
    [Tooltip("留空時自動取 GetComponent<AudioSource>，也可手動指定")]
    public AudioSource audioSource;
    [Range(0f, 1f)]
    public float pageTurnVolume = 1f;

    private int index;
    private PlayerInputActions inputActions;
    private float lastNavigateTime = -999f;

    void Awake()
    {
        if (enableDpadNavigation)
            inputActions = new PlayerInputActions();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (prevBtn) prevBtn.onClick.AddListener(Prev);
        if (nextBtn) nextBtn.onClick.AddListener(Next);
    }

    void OnEnable()
    {
        if (enableDpadNavigation && inputActions != null)
            inputActions.Player.Enable();

        Show(Mathf.Clamp(startIndex, 0, subPages.Count - 1));
    }

    void OnDisable()
    {
        if (enableDpadNavigation && inputActions != null)
            inputActions.Player.Disable();
    }

    void OnDestroy()
    {
        inputActions?.Dispose();
    }

    void Update()
    {
        if (enableDpadNavigation && inputActions != null && gameObject.activeInHierarchy)
            HandleDpadNavigation();
    }

    void HandleDpadNavigation()
    {
        if (Time.time - lastNavigateTime < navigationCooldown) return;

        if (inputActions.Player.NavigateLeft.WasPressedThisFrame())
        {
            lastNavigateTime = Time.time;
            Prev();
        }
        else if (inputActions.Player.NavigateRight.WasPressedThisFrame())
        {
            lastNavigateTime = Time.time;
            Next();
        }
    }

    public void Prev() => Show(index - 1);
    public void Next() => Show(index + 1);

    /// <summary>直接跳至指定 SubPage Index（0-based）。</summary>
    public void GoToPage(int i) => Show(i);

    /// <summary>切換到指定 SubPage，並在頁面實際改變時播放翻頁音效。</summary>
    void Show(int i)
    {
        if (subPages.Count == 0) return;

        int newIndex = Mathf.Clamp(i, 0, subPages.Count - 1);
        bool pageChanged = newIndex != index;

        index = newIndex;

        for (int k = 0; k < subPages.Count; k++)
            if (subPages[k]) subPages[k].SetActive(k == index);

        if (prevBtn) prevBtn.interactable = (index > 0);
        if (nextBtn) nextBtn.interactable = (index < subPages.Count - 1);

        foreach (var binder in subPages[index].GetComponentsInChildren<BookSlotBinder>(true))
            binder.Refresh();

        // 只在頁面確實切換時播放音效（避免 OnEnable 初始化時觸發）
        if (pageChanged)
            PlayPageTurnSound();
    }

    /// <summary>播放翻頁音效。</summary>
    private void PlayPageTurnSound()
    {
        if (pageTurnClip == null) return;

        if (audioSource != null)
            audioSource.PlayOneShot(pageTurnClip, pageTurnVolume);
        else
            AudioSource.PlayClipAtPoint(pageTurnClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, pageTurnVolume);
    }
}
