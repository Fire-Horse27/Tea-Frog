using UnityEngine;

public class GrabItem : MonoBehaviour
{
    [Tooltip("Reference to the HeldTea component on the player or hand object")]
    public HeldTea heldTea;

    [Tooltip("The id of the item to pick up (e.g. 'Green', 'Glass', 'Honey')")]
    public string itemID;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (heldTea == null)
            {
                // Try to get it from the player if not assigned in inspector
                heldTea = other.GetComponentInChildren<HeldTea>();
            }

            if (heldTea != null)
            {
                heldTea.SetHeld(itemID);
            }
            else
            {
                Debug.LogWarning("HeldTea component not found on player!");
            }
        }
    }
}
