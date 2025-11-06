using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GrabItem : MonoBehaviour
{
    [Tooltip("ID passed to HeldTea.SetHeld")]
    public string itemID;

    [Tooltip("y offset in world units for the button above the item")]
    public float promptOffsetY = .5f;

    // Shared references (set by EButtonRegistrar)
    public static Transform Button;
    public static Transform playerTransform;

    Collider2D col;
    HeldTea heldTea;
    public bool showingPrompt;

    void Awake()
    {
        col = GetComponent<Collider2D>();

        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
            heldTea = playerTransform.GetComponentInChildren<HeldTea>();
    }

    void Update()
    {
        bool playerInside = col.OverlapPoint(playerTransform.position);

        if (playerInside)
        {
            showingPrompt = true;
            Button.position = new Vector3(transform.position.x,
                                          transform.position.y + promptOffsetY,
                                          -1);
            Button.gameObject.SetActive(true);
        }
        else if (!playerInside && showingPrompt)
        {
            Button.gameObject.SetActive(false);
            showingPrompt = false;
        }

        if (showingPrompt && heldTea != null && Input.GetKeyDown(KeyCode.E))
        {
            if (itemID == "Hot" && heldTea.cupHeld == "Tea" && heldTea.cupFilled == "")
            {
                var kettle = GetComponent<KettleFuntion>();
                if (kettle != null) kettle.NextSprite();
            }

            if (itemID == "Trashcan")
            {
                heldTea.ClearEverything();
            }
            else
            {
                heldTea.SetHeld(itemID);
            }
        }
    }
}
