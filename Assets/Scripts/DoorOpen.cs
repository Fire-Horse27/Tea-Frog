using UnityEngine;

public class DoorOpen : MonoBehaviour
{
    public string triggerTag = "Player";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(triggerTag))
        {
            SetChildrenVisible(false);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(triggerTag))
        {
            SetChildrenVisible(true);
        }
    }

    private void SetChildrenVisible(bool state)
    {
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            sr.enabled = state;
        }
    }
}

