using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FrogAI : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 2f;
    public float arriveThreshold = 0.06f;

    [Header("Order")]
    public float orderTime = 8f;          // time before impatient leave
    public float servedLinger = 0.6f;     // time to stay after being served
    public bool orderTaken = false;       // has the player acknowledged the order
    public bool receivedOrder = false;    // has the tea been delivered

    [Header("Optional")]
    public Animator animator;

    [HideInInspector] public Transform counterPoint;
    private PathfinderAStar pathfinder;

    private List<Vector3> path = new List<Vector3>();
    private int pathIndex = 0;

    private Seat assignedSeat = null;
    public int queueIndex = -1;

    private bool reachedCounter = false;
    private bool served = false;
    private float orderTimer = 0f;

    // Shared queue
    private static List<FrogAI> sharedQueue = new List<FrogAI>();

    private const float fallbackLateralSpacing = 0.35f;
    private const float fallbackForwardStep = 0.6f;

    void Awake()
    {
        pathfinder = FindObjectOfType<PathfinderAStar>();
    }

    void OnEnable()
    {
        path.Clear();
        pathIndex = 0;
        reachedCounter = false;
        served = false;
        assignedSeat = null;
        queueIndex = -1;

        // Ensure order state is reset when the frog (re)appears at the door/spawn.
        orderTaken = false;
        receivedOrder = false;
        orderTimer = orderTime;
    }

    /// <summary>
    /// Called by spawner: place frog into the queue (do not go to counter directly).
    /// </summary>
    public void InitializeNew()
    {
        orderTimer = orderTime;
        served = false;
        reachedCounter = false;
        assignedSeat = null;
        queueIndex = -1;
        orderTaken = false;
        receivedOrder = false;

        if (CafeManager.Instance != null)
        {
            CafeManager.Instance.Enqueue(this);
        }
        else
        {
            if (counterPoint != null)
                MoveTo(counterPoint.position);
        }
    }

    #region Queue management (sharedQueue)

    public void JoinQueue()
    {
        if (sharedQueue.Contains(this)) return;
        sharedQueue.Add(this);
        UpdateQueuePositions();
    }

    public static void RemoveFromQueue(FrogAI frog)
    {
        if (frog == null) return;
        if (!sharedQueue.Contains(frog)) return;

        // free reserved spot in manager if any
        if (CafeManager.Instance != null && frog.queueIndex >= 0)
            CafeManager.Instance.FreeQueueSpot(frog.queueIndex, frog);

        sharedQueue.Remove(frog);
        frog.queueIndex = -1;
        UpdateQueuePositions();
    }

    /// <summary>
    /// Assign frogs to queue spots in queue order and reserve spots immediately in CafeManager.
    /// This implementation ties each frog's queueIndex to its index in sharedQueue:
    /// sharedQueue[0] => front of queue; mapping to the front-most queuePoint.
    /// It deterministically attempts to reserve the matching spot in the CafeManager;
    /// if reservation fails, it searches for an alternate free spot, otherwise falls back.
    /// </summary>
    private static void UpdateQueuePositions()
    {
        if (sharedQueue == null) return;

        Transform[] queuePoints = (CafeManager.Instance != null) ? CafeManager.Instance.queuePoints : null;
        int spotCount = (queuePoints != null) ? queuePoints.Length : 0;

        // Fallback if no queuePoints: map index -> fallback position
        if (spotCount == 0)
        {
            for (int i = 0; i < sharedQueue.Count; i++)
            {
                var f = sharedQueue[i];
                if (f == null) continue;
                f.queueIndex = i;
                f.MoveTo(ComputeFallbackQueuePosition(f, i));
            }
            return;
        }

        // For each frog in queue order, compute the desired spot index.
        // Convention preserved: the "front" of queue maps to the last queue point (spotCount - 1).
        for (int q = 0; q < sharedQueue.Count; q++)
        {
            var frog = sharedQueue[q];
            if (frog == null) continue;

            int desiredSpot = Mathf.Clamp(spotCount - 1 - q, 0, spotCount - 1);
            int chosenSpot = -1;

            // If this frog previously reserved a different spot, free it first (so reservations stay accurate).
            if (CafeManager.Instance != null && frog.queueIndex >= 0 && frog.queueIndex != desiredSpot)
            {
                CafeManager.Instance.FreeQueueSpot(frog.queueIndex, frog);
            }

            // Try to reserve the desired spot (or reuse if already ours).
            if (CafeManager.Instance != null)
            {
                // If the spot is already occupied by someone else, we'll search for next best below.
                var occupant = CafeManager.Instance.queueOccupant[desiredSpot];
                if (occupant == null || occupant == frog)
                {
                    // TryReserve is idempotent and will succeed if the spot is free or already ours.
                    if (CafeManager.Instance.TryReserveQueueSpot(desiredSpot, frog))
                    {
                        chosenSpot = desiredSpot;
                    }
                }

                // If we couldn't reserve desiredSpot, find the nearest available spot searching downward.
                if (chosenSpot == -1)
                {
                    for (int s = desiredSpot; s >= 0; s--)
                    {
                        var occ = CafeManager.Instance.queueOccupant[s];
                        if (occ == null || occ == frog)
                        {
                            if (CafeManager.Instance.TryReserveQueueSpot(s, frog))
                            {
                                chosenSpot = s;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                // No CafeManager: just use desired spot index for movement mapping
                chosenSpot = desiredSpot;
            }

            // If still no chosen spot (all reservations failed), fall back to desiredSpot for movement but do not reserve.
            if (chosenSpot == -1)
            {
                chosenSpot = desiredSpot;
            }

            // Assign and instruct movement
            frog.queueIndex = chosenSpot;
            if (chosenSpot >= 0 && chosenSpot < spotCount)
            {
                frog.MoveTo(queuePoints[chosenSpot].position);
            }
            else
            {
                frog.MoveTo(ComputeFallbackQueuePosition(frog, q));
            }
        }
    }

    /// <summary>
    /// Pops front frog and updates positions.
    /// </summary>
    public static FrogAI PopFrontOfQueue()
    {
        if (sharedQueue == null || sharedQueue.Count == 0) return null;
        FrogAI f = sharedQueue[0];

        // free its reserved spot in manager before removing
        if (CafeManager.Instance != null && f != null && f.queueIndex >= 0)
        {
            CafeManager.Instance.FreeQueueSpot(f.queueIndex, f);
        }

        sharedQueue.RemoveAt(0);
        if (f != null) f.queueIndex = -1;
        UpdateQueuePositions();
        return f;
    }

    #endregion

    #region Movement & seating

    public void SetQueueIndex(int index)
    {
        queueIndex = index;
        Transform q = (CafeManager.Instance != null) ? CafeManager.Instance.GetQueuePoint(index) : null;
        if (q != null) MoveTo(q.position);
        else MoveTo(ComputeFallbackQueuePosition(this, index));
    }

    public void AssignSeat(Seat seat)
    {
        // free any reservation & remove from queue
        RemoveFromQueue(this);

        assignedSeat = seat;

        if (queueIndex >= 0)
            MoveFromQueueToSeat();
        else if (assignedSeat != null)
            MoveTo(assignedSeat.SeatPoint.position);
    }

    private void MoveFromQueueToSeat()
    {
        if (assignedSeat == null || pathfinder == null)
        {
            MoveTo(assignedSeat != null ? assignedSeat.SeatPoint.position : transform.position);
            return;
        }

        Vector3 seatPos = assignedSeat.SeatPoint.position;
        Vector3 dirToSeat = (seatPos - transform.position).normalized;
        if (dirToSeat.sqrMagnitude < 0.0001f) dirToSeat = Vector3.up;

        float forwardStep = 0.6f;
        Vector3 forwardPoint = transform.position + dirToSeat * forwardStep;

        Vector3 perp = new Vector3(-dirToSeat.y, dirToSeat.x, 0f).normalized;
        float lateralSpacing = 0.35f;
        float sideMultiplier = (queueIndex % 2 == 0) ? 1f : -1f;
        float lateralOffset = lateralSpacing * Mathf.Ceil(queueIndex / 2f) * sideMultiplier;

        Vector3 intermediate = forwardPoint + perp * lateralOffset;

        if (pathfinder == null) pathfinder = FindObjectOfType<PathfinderAStar>();
        if (pathfinder == null || pathfinder.grid == null)
        {
            MoveTo(seatPos);
            return;
        }

        List<Vector3> p1 = pathfinder.FindPath(transform.position, intermediate);
        List<Vector3> p2 = pathfinder.FindPath(intermediate, seatPos);

        if ((p1 == null || p1.Count == 0) || (p2 == null || p2.Count == 0))
        {
            List<Vector3> direct = pathfinder.FindPath(transform.position, seatPos);
            if (direct != null && direct.Count > 0) { ClampPathToPlane(direct); path = direct; pathIndex = 0; UpdateAnimatorWalking(true); return; }
            if (p1 != null && p1.Count > 0) { ClampPathToPlane(p1); path = p1; pathIndex = 0; UpdateAnimatorWalking(true); return; }
            MoveTo(seatPos); return;
        }

        List<Vector3> combined = new List<Vector3>(p1);
        if (p2.Count > 0)
        {
            Vector3 firstOfSecond = p2[0];
            if (combined.Count > 0 && Vector3.Distance(combined[combined.Count - 1], firstOfSecond) < 0.01f)
            {
                for (int i = 1; i < p2.Count; i++) combined.Add(p2[i]);
            }
            else combined.AddRange(p2);
        }

        ClampPathToPlane(combined);
        path = combined;
        pathIndex = 0;
        UpdateAnimatorWalking(true);
        queueIndex = -1;
    }

    public void MoveTo(Vector3 worldTarget)
    {
        if (pathfinder == null) pathfinder = FindObjectOfType<PathfinderAStar>();
        if (pathfinder == null) return;
        List<Vector3> raw = pathfinder.FindPath(transform.position, worldTarget);
        if (raw != null && raw.Count > 0)
        {
            ClampPathToPlane(raw);
            path = raw;
            pathIndex = 0;
            UpdateAnimatorWalking(true);
        }
        else
        {
            path = new List<Vector3> { new Vector3(worldTarget.x, worldTarget.y, transform.position.z) };
            pathIndex = 0;
            UpdateAnimatorWalking(true);
        }
    }

    void Update()
    {
        if (path != null && pathIndex < path.Count)
        {
            Vector3 target = path[pathIndex];
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z);

            if (Vector3.Distance(new Vector2(transform.position.x, transform.position.y),
                                 new Vector2(target.x, target.y)) <= arriveThreshold)
            {
                pathIndex++;
                if (pathIndex >= path.Count) OnReachedPathEnd();
            }
        }
        else
        {
            if (assignedSeat != null && !served)
            {
                orderTimer -= Time.deltaTime;
                if (orderTimer <= 0f) StartCoroutine(LeaveRoutine());
            }
            else if (served)
            {
                StartCoroutine(LeaveRoutine());
            }
        }
    }

    // Replace the existing OnReachedPathEnd() with this:
    void OnReachedPathEnd()
    {
        UpdateAnimatorWalking(false);

        // If we haven't reached counter yet, treat this as arrival to counter
        if (!reachedCounter && counterPoint != null)
        {
            // Only set reached if we are physically at the counterPoint
            if (IsAtCounter())
            {
                reachedCounter = true;

                // If someone else is already occupying the counter (front of shared queue),
                // and it's not us, join the queue instead of staying at the counter.
                FrogAI currentFront = GetFrontOfQueue();
                if (currentFront != null && currentFront != this && currentFront.IsAtCounter())
                {
                    JoinQueue();
                    return;
                }

                // NOTE: Do NOT auto-update path/assign seat here based on orderTaken.
                // The seat assignment will happen only when OrderTakenByPlayer() is called.
                // If order hasn't been taken, become front of the queue and wait.
                if (!sharedQueue.Contains(this))
                {
                    sharedQueue.Insert(0, this);
                    UpdateQueuePositions();
                }
                // Remain at counter waiting for the player's interaction.
                return;
            }
            else
            {
                // If this path ended at a queue point and we are at our queueIndex, confirm reservation
                if (queueIndex >= 0 && CafeManager.Instance != null)
                {
                    CafeManager.Instance.ConfirmQueueSpot(queueIndex, this);
                }
            }
        }

        // If reached a seat
        if (assignedSeat != null)
        {
            Vector3 seat = assignedSeat.SeatPoint.position;
            transform.position = new Vector3(seat.x, seat.y, transform.position.z);
            StartCoroutine(SeatedRoutine());
            return;
        }
    }

    /// <summary>
    /// Called when the player attempts to take this frog's order.
    /// Only proceeds if the frog is physically at the counter. If not at the
    /// counter, nothing happens (order is not marked taken).
    /// </summary>
    public void OrderTakenByPlayer()
    {
        // Only allow taking the order if the frog is actually at the counter.
        if (!IsAtCounter())
        {
            // Safety: do nothing if not at counter. Cashregister normally
            // prevents calling this when not at counter, but enforce here too.
            return;
        }

        // If someone else is front and at counter (not this frog), don't steal the turn.
        FrogAI currentFront = GetFrontOfQueue();
        if (currentFront != null && currentFront != this && currentFront.IsAtCounter())
        {
            // Not our turn — do nothing.
            return;
        }

        // Mark that this frog's order has been taken.
        orderTaken = true;

        //yield return new WaitForSeconds(2);

        // Remove reservations / queue entry if any.
        if (sharedQueue.Contains(this))
        {
            if (GetFrontOfQueue() == this) PopFrontOfQueue();
            else RemoveFromQueue(this);
        }

        // Try to assign a seat immediately.
        if (CafeManager.Instance != null && CafeManager.Instance.TryAssignSeat(this, out Seat seat))
        {
            AssignSeat(seat);
        }
        else
        {
            // If no seat available, keep expected behavior (join queue).
            JoinQueue();
        }
    }

    IEnumerator SeatedRoutine()
    {
        UpdateAnimatorSit(true);
        orderTimer = orderTime;
        while (!served && orderTimer > 0f) yield return null;
        if (served) { yield return new WaitForSeconds(servedLinger); StartCoroutine(LeaveRoutine()); }
        else StartCoroutine(LeaveRoutine());
    }

    public void Serve()
    {
        if (assignedSeat == null) return;
        served = true;
        UpdateAnimatorHappy();
    }

    IEnumerator LeaveRoutine()
    {
        UpdateAnimatorSit(false);
        UpdateAnimatorWalking(true);

        if (assignedSeat != null)
        {
            CafeManager.Instance.NotifySeatFreed(assignedSeat);
            assignedSeat = null;
        }

        RemoveFromQueue(this);

        GameObject exitGO = GameObject.FindWithTag("Exit");
        if (exitGO != null && pathfinder != null)
        {
            MoveTo(exitGO.transform.position);
            while (path != null && pathIndex < path.Count) yield return null;
        }

        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (assignedSeat != null)
        {
            CafeManager.Instance.NotifySeatFreed(assignedSeat);
            assignedSeat = null;
        }

        RemoveFromQueue(this);

        orderTaken = false;
        receivedOrder = false;
    }

    #endregion

    #region Helpers & Anim

    void UpdateAnimatorWalking(bool walking) { if (animator != null) animator.SetBool("isWalking", walking); }
    void UpdateAnimatorSit(bool sit) { if (animator != null) animator.SetBool("isSitting", sit); }
    void UpdateAnimatorHappy() { if (animator != null) animator.SetTrigger("isHappy"); }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // handled by Cashregister/Player interaction; not used here directly
        }
    }

    private void ClampPathToPlane(List<Vector3> p)
    {
        if (p == null) return;
        float z = transform.position.z;
        for (int i = 0; i < p.Count; i++)
        {
            Vector3 v = p[i];
            p[i] = new Vector3(v.x, v.y, z);
        }
    }

    public static FrogAI GetFrontOfQueue()
    {
        if (sharedQueue == null || sharedQueue.Count == 0) return null;
        return sharedQueue[0];
    }

    public bool IsAtCounter(float tolerance = 0.08f)
    {
        if (counterPoint == null) return false;
        float d = Vector2.Distance(new Vector2(transform.position.x, transform.position.y),
                                   new Vector2(counterPoint.position.x, counterPoint.position.y));
        return d <= tolerance;
    }

    #endregion

    #region Fallback queue pos

    private static Vector3 ComputeFallbackQueuePosition(FrogAI f, int idx)
    {
        if (f == null) return Vector3.zero;

        Vector3 basePos = f.transform.position;
        if (CafeManager.Instance != null)
        {
            Transform origin = CafeManager.Instance.GetQueuePoint(0);
            if (origin != null) basePos = origin.position;
        }
        if (f.counterPoint != null) basePos = f.counterPoint.position;

        Vector3 dirToCounter = Vector3.up;
        if (f.counterPoint != null)
        {
            dirToCounter = (f.counterPoint.position - basePos).normalized;
            if (dirToCounter.sqrMagnitude < 0.0001f) dirToCounter = Vector3.up;
        }

        Vector3 perp = new Vector3(-dirToCounter.y, dirToCounter.x, 0f).normalized;
        if (perp.sqrMagnitude < 0.0001f) perp = Vector3.right;

        float sideMultiplier = (idx % 2 == 0) ? 1f : -1f;
        float pairIndex = Mathf.Ceil((idx + 1) / 2f);

        float lateralOffset = fallbackLateralSpacing * pairIndex * sideMultiplier;
        float forwardOffset = -fallbackForwardStep * pairIndex;

        Vector3 result = basePos + perp * lateralOffset + dirToCounter * forwardOffset;
        result.z = f.transform.position.z;

        return result;
    }

    #endregion

    // Force the frog to be marked served and start leaving immediately.
    // This covers the case where Serve() might require an assignedSeat; this will work either way.
    public void ForceServeAndLeave()
    {
        // mark served and play happy animation
        served = true;
        UpdateAnimatorHappy();

        // If the frog has a reserved seat, notify CafeManager that the seat is free now
        // (this avoids seat getting stuck). Then clear the assignedSeat reference.
        if (assignedSeat != null)
        {
            if (CafeManager.Instance != null)
                CafeManager.Instance.NotifySeatFreed(assignedSeat);
            assignedSeat = null;
        }

        // stop any existing coroutines for movement/waiting to avoid conflicting state
        StopAllCoroutines();

        // Start the leaving sequence (uses same LeaveRoutine already in FrogAI)
        StartCoroutine(LeaveRoutine());
    }


}
