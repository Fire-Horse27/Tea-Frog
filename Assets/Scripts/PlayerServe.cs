using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerServe : MonoBehaviour
{
    public KeyCode serveKey = KeyCode.E;
    public float serveRadius = 1.6f;
    public LayerMask frogLayer = ~0; // optional: set a layer mask for frogs

    private HeldTea heldTea;

    void Awake()
    {
        // try to find HeldTea on player
        heldTea = GetComponentInChildren<HeldTea>();
        //if (heldTea == null) Debug.LogWarning("PlayerServe: HeldTea not found on player children.");
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
        if (heldTea == null) return;

        OrderData playerOrder = heldTea.GetOrderData();

        // find all FrogAI in scene and pick nearest that is at counter (or within radius)
        FrogAI[] frogs = GameObject.FindObjectsOfType<FrogAI>();
        FrogAI best = null;
        float bestDist = float.MaxValue;

        foreach (var f in frogs)
        {
            if (f == null) continue;

            // only consider frogs that are at the counter (waiting to be served) OR very near player
            bool candidate = f.IsAtCounter() || Vector3.Distance(transform.position, f.transform.position) <= serveRadius;
            if (!candidate) continue;

            float d = Vector3.Distance(transform.position, f.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = f;
            }
        }

        if (best == null)
        {
            //Debug.Log("No frog to serve nearby.");
            return;
        }

        // get the frog's order (requires CustomerOrder component)
        var co = best.GetComponent<CustomerOrder>();
        if (co == null)
        {
            //Debug.LogWarning("Target frog has no CustomerOrder component.");
            return;
        }

        OrderData needed = co.order;

        // Compare
        bool match = needed.Matches(playerOrder);

        if (match)
        {
            //Debug.Log($"Served {best.name} successfully! Order: {needed}");
            // mark frog as served (this assumes FrogAI has Serve())
            best.Serve();

            // small cleanup: clear player's held tea
            heldTea.ClearEverything();

            // optionally give the frog its seat here or let CafeManager handle it via TakeOrderByPlayer flow
            // If your flow is: player presses E -> frog.TakeOrderByPlayer() -> assignment, call that:
            // best.TakeOrderByPlayer(); // only if implemented/desired

        }
        else
        {
            //Debug.Log($"Wrong order for {best.name}. Needed: {needed}, Player offered: {playerOrder}");
            // optional: show UI feedback or negative reaction
        }
    }
}
