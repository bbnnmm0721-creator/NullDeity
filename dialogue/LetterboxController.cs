using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LetterboxController : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void ShowLetterbox()
    {
        animator.SetTrigger("ShowLetterbox");
    }

    public void HideLetterbox()
    {
        animator.SetTrigger("HideLetterbox");
    }
}

