using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CustomerTriggerAvoidance : MonoBehaviour
{
    [Tooltip("World units for one tile step (set to your tile cell size).")]
    public float tileStep = 1f;

    [Tooltip("How often (seconds) to retry when blocked.")]
    public float retryInterval = 0.25f;

    // optional integrations
    private FrogAI frogAI;
    private PathfinderAStar pathfinder;
    private ReservationGrid reservationGrid;

    // runtime
    private Coroutine avoidCoroutine;

    void Awake()
    {
        frogAI = GetComponent<FrogAI>();
        pathfinder = FindObjectOfType<PathfinderAStar>();
        reservationGrid = ReservationGrid.Instance; // may be null
    }

    // Trigger-only handlers
    void OnTriggerEnter2D(Collider2D other) => HandleTriggerStart(other);
    void OnTriggerStay2D(Collider2D other) => HandleTriggerStay(other);
    void OnTriggerExit2D(Collider2D other) => HandleTriggerEnd(other);

    private void HandleTriggerStart(Collider2D other)
    {
        if (!IsOtherCustomer(other)) return;
        if (avoidCoroutine == null)
            avoidCoroutine = StartCoroutine(AvoidAndRetryRoutine(other));
    }

    private void HandleTriggerStay(Collider2D other)
    {
        if (!IsOtherCustomer(other)) return;
        if (avoidCoroutine == null)
            avoidCoroutine = StartCoroutine(AvoidAndRetryRoutine(other));
    }

    private void HandleTriggerEnd(Collider2D other)
    {
        if (!IsOtherCustomer(other)) return;
        if (avoidCoroutine != null)
        {
            StopCoroutine(avoidCoroutine);
            avoidCoroutine = null;
        }
    }

    private bool IsOtherCustomer(Collider2D other)
    {
        if (other == null) return false;
        if (other.gameObject == this.gameObject) return false;
        return other.CompareTag("Customer");
    }

    private IEnumerator AvoidAndRetryRoutine(Collider2D other)
    {
        Vector3 otherLastPos = other.transform.position;

        while (true)
        {
            if (other == null || other.gameObject == null) break;

            // Compute offset direction (one of the four cardinal directions)
            Vector3 offset = ComputeAvoidOffset(other.transform.position, transform.position);
            Vector3 desiredTarget = transform.position + offset * tileStep;

            // Try reserving via ReservationGrid if available
            bool reservedOk = true;
            Vector3Int targetCell = Vector3Int.zero;
            if (reservationGrid != null && pathfinder != null && pathfinder.grid != null)
            {
                targetCell = pathfinder.grid.WorldToCell(desiredTarget);
                reservedOk = reservationGrid.TryReserve(targetCell, frogAI ?? GetComponent<FrogAI>());
                if (!reservedOk)
                {
                    // If owner is the collider we touched, wait for it to move
                    var owner = reservationGrid.GetOwner(targetCell);
                    if (owner != null && owner.gameObject == other.gameObject)
                    {
                        // Wait until other moves or reservation is freed
                        float waited = 0f;
                        while (other != null && other.transform.position == otherLastPos && reservationGrid.IsReserved(targetCell))
                        {
                            yield return new WaitForSeconds(0.05f);
                            waited += 0.05f;
                            if (waited > 2f) break; // fallback after timeout
                        }
                        otherLastPos = other.transform.position;
                        yield return null;
                        continue;
                    }
                    else
                    {
                        // someone else reserved it — retry later
                        yield return new WaitForSeconds(retryInterval);
                        continue;
                    }
                }
            }
            else
            {
                // No reservation system: ensure no customer already near the target
                Collider2D[] hits = Physics2D.OverlapCircleAll(desiredTarget, tileStep * 0.45f);
                bool occupied = false;
                foreach (var h in hits)
                {
                    if (h.gameObject != this.gameObject && h.CompareTag("Customer")) { occupied = true; break; }
                }
                if (occupied)
                {
                    // wait until other moves
                    float waited = 0f;
                    while (other != null && other.transform.position == otherLastPos)
                    {
                        hits = Physics2D.OverlapCircleAll(desiredTarget, tileStep * 0.45f);
                        bool occ2 = false;
                        foreach (var h in hits) if (h.gameObject != this.gameObject && h.CompareTag("Customer")) { occ2 = true; break; }
                        if (!occ2) break;
                        yield return new WaitForSeconds(0.05f);
                        waited += 0.05f;
                        if (waited > 2f) break;
                    }
                    otherLastPos = other.transform.position;
                    yield return null;
                    continue;
                }
            }

            // Initiate movement to desiredTarget
            if (frogAI != null)
            {
                frogAI.MoveTo(desiredTarget);
            }
            else if (pathfinder != null)
            {
                var p = pathfinder.FindPath(transform.position, desiredTarget);
                if (p != null && p.Count > 0)
                {
                    transform.position = p[0];
                }
                else
                {
                    yield return new WaitForSeconds(retryInterval);
                    continue;
                }
            }
            else
            {
                transform.position = desiredTarget;
            }

            // exit once move commanded
            avoidCoroutine = null;
            yield break;
        }

        avoidCoroutine = null;
    }

    private Vector3 ComputeAvoidOffset(Vector3 otherPos, Vector3 myPos)
    {
        float dx = myPos.x - otherPos.x;
        float dy = myPos.y - otherPos.y;

        if (Mathf.Abs(dx) > Mathf.Abs(dy))
        {
            if (dx < 0f) return Vector3.up;    // I'm left -> go up
            else return Vector3.down;          // I'm right -> go down
        }
        else
        {
            if (dy > 0f) return Vector3.right; // I'm above -> go right
            else return Vector3.left;          // I'm below -> go left
        }
    }

    // Debug: draw the candidate target when actively avoiding
    void OnDrawGizmosSelected()
    {
        if (avoidCoroutine != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, tileStep * 0.6f);
        }
    }
}
