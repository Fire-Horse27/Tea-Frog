using UnityEngine;

/// <summary>
/// Attach to the frog GameObject (same GameObject that has FrogAI and CustomerOrder).
/// Requires the frog to have a Collider2D set as "Is Trigger".
/// Shows the shared E-button when the player enters the frog's trigger,
/// and listens for the E key to attempt a serve / take-order action.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CustomerServeInteract : MonoBehaviour
{
    [Tooltip("Vertical offset (world units) to position the E button above the frog")]
    public float buttonOffsetY = 0.8f;

    [Tooltip("Key used to serve / take order (defaults to E)")]
    public KeyCode serveKey = KeyCode.E;

    // internal
    CustomerOrder customerOrder;
    FrogAI frogAI;

    Transform playerTransform;
    HeldTea playerHeldTea;
    bool playerInside = false;

    void Awake()
    {
        customerOrder = GetComponent<CustomerOrder>();
        frogAI = GetComponent<FrogAI>();

        // ensure collider is trigger
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[CustomerServeInteract] Collider on {name} should be set to 'Is Trigger' for this interaction to work.", this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = true;
        playerTransform = other.transform;
        playerHeldTea = other.GetComponentInChildren<HeldTea>();
        ShowEButton(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = false;
        playerTransform = null;
        playerHeldTea = null;
        ShowEButton(false);
    }

    void Update()
    {
        if (!playerInside) return;
        if (GrabItem.Button == null) return; // no shared button assigned
        if (playerTransform == null) return;

        // keep button positioned above frog (in case frog moves)
        PositionButton();

        // press to interact
        if (Input.GetKeyDown(serveKey))
        {
            AttemptInteraction();
        }
    }

    void ShowEButton(bool show)
    {
        if (GrabItem.Button == null) return;
        GrabItem.Button.gameObject.SetActive(show);
        if (show) PositionButton();
    }

    void PositionButton()
    {
        if (GrabItem.Button == null) return;
        var b = GrabItem.Button;
        b.position = new Vector3(transform.position.x,
                                 transform.position.y + buttonOffsetY,
                                 -1f);
    }

    void AttemptInteraction()
    {
        // If the player isn't holding a drink -> try to take order (if frog at counter)
        if (playerHeldTea == null)
        {
            Debug.Log("[CustomerServeInteract] Player has no HeldTea component.");
            return;
        }

        bool holdingDrink = (playerHeldTea.teaType != null);

        if (!holdingDrink)
        {
            // Use FrogAI's order-taking method (your FrogAI calls it OrderTakenByPlayer())
            frogAI.OrderTakenByPlayer();
            // hide the button because order was taken or no change
            ShowEButton(false);
            return;
        }

        // player has a drink -> compare
        var co = customerOrder;
        if (co == null)
        {
            Debug.LogWarning("[CustomerServeInteract] Missing CustomerOrder on frog.");
            return;
        }

        OrderData playerOrder = playerHeldTea.GetOrderData();
        OrderData needed = co.order;

        Debug.Log($"[CustomerServeInteract] Player offers '{playerOrder}' for frog needing '{needed}'.");

        if (needed.Matches(playerOrder))
        {
            // success: call ForceServeAndLeave if available otherwise Serve()
            var method = frogAI.GetType().GetMethod("ForceServeAndLeave");
            if (method != null) method.Invoke(frogAI, null);
            else frogAI.Serve();

            playerHeldTea.ClearEverything();
            ShowEButton(false);
        }
        else
        {
            // wrong order: you can show feedback here
            Debug.Log("[CustomerServeInteract] Wrong order delivered.");
            // optional: play sound / UI feedback
        }
    }

    void OnDisable()
    {
        // hide shared button if this object disabled while showing
        if (GrabItem.Button != null && GrabItem.Button.gameObject.activeSelf)
        {
            GrabItem.Button.gameObject.SetActive(false);
        }
    }
}
