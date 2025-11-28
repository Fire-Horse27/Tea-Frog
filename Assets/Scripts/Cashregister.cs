using UnityEngine;
using static UnityEditor.Progress;

[RequireComponent(typeof(Collider2D))]
public class Cashregister : MonoBehaviour
{
    [Tooltip("y offset in world units for the button above the item")]
    public float promptOffsetY = .5f;

    // Shared references (set by EButtonRegistrar)
    public static Transform Button;
    public static Transform playerTransform;

    public Collider2D col;
    public bool showingPrompt;

    private AudioSource audioSource;

    // How close to the counter we consider "at the counter" when asking frogs
    public float registerCounterTolerance = 0.08f;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();

        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
    }

    void Update()
    {
        bool playerInside = col.OverlapPoint(playerTransform.position);

        // Find the front-of-queue frog (FrogAI provides a static helper).
        FrogAI frontFrog = FrogAI.GetFrontOfQueue();

        // Determine whether the register should show the prompt:
        // - Only show the "take order" prompt when player is inside AND
        //   there is a front frog AND that front frog is at the counter AND hasn't had order taken.
        bool frontEligible = false;
        if (frontFrog != null)
        {
            // Use frog's IsAtCounter to decide if it's physically at the counter
            frontEligible = frontFrog.IsAtCounter(registerCounterTolerance) && !frontFrog.orderTaken;
        }

        // Show prompt when player inside and (a) frontEligible OR (b) fallback legacy behavior (item interactions)
        if (playerInside && frontEligible)
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

        // Handle E press:
        if (showingPrompt && Input.GetKeyDown(KeyCode.E))
        {
            // If front customer is eligible, take their order (highest priority).
            if (frontEligible && frontFrog != null)
            {
                frontFrog.OrderTakenByPlayer();
                showingPrompt = false;
                Button.gameObject.SetActive(false);
                audioSource.PlayOneShot(audioSource.clip);
                return;
            }
        }
    }
}
