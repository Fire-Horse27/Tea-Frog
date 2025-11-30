using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GrabItem : MonoBehaviour
{
    [Tooltip("ID passed to HeldTea - can be: Glass, Tea, Ice, Honey, Milk, Hot, Water, or a tea color name (Red/Green/Black/Blue)")]
    public string itemID;

    [Tooltip("y offset in world units for the button above the item")]
    public float promptOffsetY = .5f;

    // Shared references (set by EButtonRegistrar)
    public static Transform Button;
    public static Transform playerTransform;

    Collider2D col;
    HeldTea heldTea;
    public KettleFunction kettle;
    public bool showingPrompt;

    // sensible defaults when emulating Hot/Water behavior
    public TeaType defaultHotTea = TeaType.Black;
    public TeaType defaultIcedTea = TeaType.Green;

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
            if (Button != null)
            {
                Button.position = new Vector3(transform.position.x,
                                              transform.position.y + promptOffsetY,
                                              -1);
                Button.gameObject.SetActive(true);
            }
        }
        else if (!playerInside && showingPrompt)
        {
            Button.gameObject.SetActive(false);
            showingPrompt = false;
        }

        if (showingPrompt && heldTea != null && Input.GetKeyDown(KeyCode.E))
        {
            HandleUse();
        }
    }

    void HandleUse()
    {
        if (itemID == "Hot" && heldTea.cupHeld == CupType.Tea && heldTea.teaType == TeaType.Empty)
        {
            var kettle = GetComponent<KettleFunction>();
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
