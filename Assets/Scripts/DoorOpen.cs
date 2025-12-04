using System.Collections;
using UnityEngine;

public class DoorAnimation : MonoBehaviour
{
    [Header("Door Sprites")]
    public Sprite closedSprite;
    public Sprite midOpenSprite;
    public Sprite fullyOpenSprite;

    [Header("Animation Settings")]
    public float animationSpeed = 0.1f; // time between sprite changes

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogError("DoorAnimation requires a SpriteRenderer on the same GameObject.");
        }
        sr.sprite = closedSprite;
    }

    public void OpenDoor()
    {
        StopAllCoroutines();
        StartCoroutine(OpenCoroutine());
    }

    public void CloseDoor()
    {
        StopAllCoroutines();
        StartCoroutine(CloseCoroutine());
    }

    private IEnumerator OpenCoroutine()
    {
        if (midOpenSprite != null)
        {
            sr.sprite = midOpenSprite;
            yield return new WaitForSeconds(animationSpeed);
        }
        if (fullyOpenSprite != null)
        {
            sr.sprite = fullyOpenSprite;
        }
    }

    private IEnumerator CloseCoroutine()
    {
        if (midOpenSprite != null)
        {
            sr.sprite = midOpenSprite;
            yield return new WaitForSeconds(animationSpeed);
        }
        if (closedSprite != null)
        {
            sr.sprite = closedSprite;
        }
    }
}

