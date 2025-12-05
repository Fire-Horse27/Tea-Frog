using UnityEngine;

/// <summary>
/// Attach to the frog GameObject (same GameObject that has FrogAI and CustomerOrder).
/// Requires the frog to have a Collider2D set as "Is Trigger".
/// Shows the shared E-button when the player enters the frog's trigger,
/// and listens for the E key to attempt a serve / take-order action.
/// Integrates with GameEngine: listens for resets and notifies the engine when a customer is served.
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

        // ensure collider is trigger (optional warning)
        var col = GetComponent<Collider2D>();
        // if (col != null && !col.isTrigger) Debug.LogWarning($"[CustomerServeInteract] Collider on {name} should be set to 'Is Trigger'.", this);
    }

    void OnDisable()
    {
        // hide shared button if this object disabled while showing
        if (GrabItem.Button != null && GrabItem.Button.gameObject.activeSelf)
        {
            GrabItem.Button.gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // don't allow interaction if the game run is finished
        if (GameEngine.IsGameOver) return;

        playerInside = true;
        playerTransform = other.transform;
        playerHeldTea = other.GetComponentInChildren<HeldTea>();
        UpdateButtonVisibility();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = false;
        playerTransform = null;
        playerHeldTea = null;
        ShowEButton(false);
    }

    void UpdateButtonVisibility()
    {
        // Only show the button if the frog's order has been taken
        bool shouldShow = playerInside && frogAI.orderTaken && !GameEngine.IsGameOver;

        ShowEButton(shouldShow);
    }

    void Update()
    {
        if (!playerInside) return;
        if (GrabItem.Button == null) return; // no shared button assigned
        if (playerTransform == null) return;
        if (GameEngine.IsGameOver) return; // block interaction if game over

        // keep button positioned above frog (in case frog moves)
        UpdateButtonVisibility();
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
        // hide if game over or engine not started
        if (GameEngine.IsGameOver) show = false;
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
            //Debug.Log("[CustomerServeInteract] Player has no HeldTea component.");
            return;
        }

        // Note: HeldTea.teaType check kept as original; adjust if HeldTea uses different semantics
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
            //Debug.LogWarning("[CustomerServeInteract] Missing CustomerOrder on frog.");
            return;
        }

        OrderData playerOrder = playerHeldTea.GetOrderData();
        OrderData needed = co.order;

        if (needed.Matches(playerOrder))
        {
            // success: serve & make frog leave
            var method = frogAI.GetType().GetMethod("ForceServeAndLeave");
            if (method != null) method.Invoke(frogAI, null);
            else frogAI.Serve();

            // Clear player's held tea
            playerHeldTea.ClearEverything();
            ShowEButton(false);

            // Notify GameEngine that a customer has been served.
            // Use instance if available; fallback to direct call if you made it static.
            if (GameEngine.Instance != null)
                GameEngine.Instance.RegisterCustomerServed();
            else
                GameEngine.Instance.RegisterCustomerServed(); // harmless if method is static; remove if not applicable
        }
        else
        {
            // wrong order: could give feedback here
            // e.g. play fail sound, flash UI, etc.
        }
    }

    // Called when GameEngine requests a reset (end of day / scene reset)
    private void HandleDayStarted(int dayIndex)
    {
        ShowEButton(false);
        playerInside = false;
        playerTransform = null;
        playerHeldTea = null;

        if (GrabItem.Button != null)
            GrabItem.Button.gameObject.SetActive(false);
    }
}
