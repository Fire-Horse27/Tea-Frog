using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerServe : MonoBehaviour
{
    public KeyCode serveKey = KeyCode.E;
    public float serveRadius = 1.6f;
    public LayerMask frogLayer = ~0; // set to frog layer(s) in inspector to narrow physics query

    private HeldTea heldTea;

    void Awake()
    {
        heldTea = GetComponentInChildren<HeldTea>();
        if (heldTea == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) heldTea = p.GetComponentInChildren<HeldTea>();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(serveKey))
        {
            TryServeNearest();
        }
    }

    void TryServeNearest()
    {
        if (heldTea == null)
        {
            Debug.LogWarning("[PlayerServe] HeldTea component not found on player.");
            return;
        }

        // Are we actually holding a drink?
        bool holdingDrink = (heldTea.teaType != null);

        // Use physics overlap to gather nearby frog candidates (use frogLayer if set)
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, serveRadius, frogLayer);
        FrogAI[] candidates = hits
            .Select(h => h.GetComponent<FrogAI>())
            .Where(f => f != null)
            .ToArray();

        // fallback to global scan if none found (handy for testing)
        if (candidates.Length == 0)
        {
            candidates = GameObject.FindObjectsOfType<FrogAI>();
        }

        // filter out inactive and frogs parented to player
        candidates = candidates
            .Where(f => f != null && f.gameObject.activeInHierarchy)
            .Where(f => !IsFrogPartOfPlayer(f))
            .ToArray();

        if (candidates.Length == 0)
        {
            return;
        }

        // pick best candidate: prefer counter frogs, otherwise nearest within radius
        FrogAI best = null;
        float bestDist = float.MaxValue;

        foreach (var f in candidates)
        {
            if (f == null) continue;
            if (!f.IsAtCounter()) continue;
            float d = Vector3.Distance(transform.position, f.transform.position);
            if (d < bestDist)
            {
                best = f;
                bestDist = d;
            }
        }

        if (best == null)
        {
            bestDist = float.MaxValue;
            foreach (var f in candidates)
            {
                if (f == null) continue;
                float d = Vector3.Distance(transform.position, f.transform.position);
                if (d <= serveRadius && d < bestDist)
                {
                    best = f;
                    bestDist = d;
                }
            }
        }

        if (best == null) return;

        Debug.Log($"[PlayerServe] Selected frog: {best.name} (dist {Vector3.Distance(transform.position, best.transform.position):F2}). HoldingDrink={holdingDrink}");

        // If player isn't holding a drink: attempt to take order (calls FrogAI.OrderTakenByPlayer())
        if (!holdingDrink)
        {
            // Prefer calling the explicit method name your FrogAI defines
            best.OrderTakenByPlayer();
            return;
        }

        // Player is holding a drink -> compare orders
        var co = best.GetComponent<CustomerOrder>();
        if (co == null)
        {
            Debug.LogWarning("[PlayerServe] Target frog has no CustomerOrder component.");
            return;
        }

        OrderData playerOrder = heldTea.GetOrderData();
        OrderData needed = co.order;

        Debug.Log($"[PlayerServe] Comparing: Frog needs '{needed}', Player offers '{playerOrder}'.");

        bool match = needed.Matches(playerOrder);

        if (match)
        {
            // correct: try ForceServeAndLeave if present, otherwise Serve()
            var method = best.GetType().GetMethod("ForceServeAndLeave");
            if (method != null)
            {
                method.Invoke(best, null);
            }
            else
            {
                best.Serve();
            }

            heldTea.ClearEverything();
            Debug.Log($"[PlayerServe] Served {best.name} successfully.");
        }
        else
        {
            Debug.Log($"[PlayerServe] Wrong order for {best.name}.");
            // TODO: show UI feedback / play sound, etc.
        }
    }

    bool IsFrogPartOfPlayer(FrogAI f)
    {
        if (f == null) return false;
        if (f.gameObject == this.gameObject) return true;
        if (f.transform.IsChildOf(this.transform)) return true;
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null && f.transform.IsChildOf(p.transform)) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, serveRadius);
    }
}
