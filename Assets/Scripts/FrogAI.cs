using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FrogAI2D with local avoidance (separation steering).
/// Attach to your frog prefab. File name: FrogAI2D.cs
/// Spawner must set frog.counterPoint before calling InitializeNew().
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FrogAI : MonoBehaviour
{
    #region Movement & Timing
    [Header("Movement")]
    public float speed = 2f;
    public float arriveThreshold = 0.06f;

    [Header("Order")]
    public float orderTime = 8f;
    public float servedLinger = 0.6f;

    [Header("Optional")]
    public Animator animator;
    #endregion

    #region Avoidance (tweak these)
    [Header("Local Avoidance")]
    [Tooltip("How far to look for neighbors (world units).")]
    public float avoidanceRadius = 0.6f;
    [Tooltip("How strongly to push away from neighbors.")]
    public float avoidanceStrength = 1.2f;
    [Tooltip("How quickly avoidance strength falls off with distance (1 = linear).")]
    public float avoidanceFalloff = 1.0f;
    [Tooltip("Cap on how far the avoidance can offset the target (world units).")]
    public float maxAvoidanceOffset = 0.6f;
    #endregion

    // Scene / runtime refs
    [HideInInspector] public Transform counterPoint; // set by Spawner
    [HideInInspector] public Transform exitPoint;    // optional (or use Exit tag)

    private PathfinderAStar pathfinder;
    private List<Vector3> path = new List<Vector3>();
    private int pathIndex = 0;

    // Seating/queue state
    private Seat assignedSeat = null;
    private int queueIndex = -1;

    // Flags & timers
    private bool reachedCounter = false;
    private bool served = false;
    private float orderTimer = 0f;

    // Static registry of all active frogs for quick neighborhood queries
    private static List<FrogAI> allFrogs = new List<FrogAI>();

    // Reservation & movement
    private Vector3Int reservedCell;          // cell currently reserved by this frog (world->cell)
    private float reserveRetryInterval = 0.25f; // seconds to wait before retrying reservation
    private float reserveRetryTimer = 0f;
    private TilemapGrid gridRef => pathfinder != null ? pathfinder.grid : null;


    void Awake()
    {
        pathfinder = FindObjectOfType<PathfinderAStar>();
        if (pathfinder == null) Debug.LogError("[FrogAI] No PathfinderAStar found in scene.");
    }

    void OnEnable()
    {
        allFrogs.Add(this);
        ResetState();
    }

    void OnDisable()
    {
        // Free reservations
        if (reservedCell != Vector3Int.zero)
        {
            ReservationGrid.Instance?.Release(reservedCell, this);
            reservedCell = Vector3Int.zero;
        }
        ReservationGrid.Instance?.ReleaseAllOwnedBy(this);
        // existing seat freeing...
        if (assignedSeat != null)
        {
            CafeManager.Instance.NotifySeatFreed(assignedSeat);
            assignedSeat = null;
        }
        allFrogs.Remove(this); // if you kept the static list
    }

    void ResetState()
    {
        path.Clear();
        pathIndex = 0;
        reachedCounter = false;
        served = false;
        assignedSeat = null;
        queueIndex = -1;
    }

    public void InitializeNew()
    {
        orderTimer = orderTime;
        served = false;
        reachedCounter = false;
        assignedSeat = null;
        queueIndex = -1;

        if (counterPoint != null) MoveTo(counterPoint.position);
        else Debug.LogWarning($"{name}: counterPoint not set on InitializeNew()", this);
    }

    public void SetQueueIndex(int index)
    {
        queueIndex = index;
        Transform q = CafeManager.Instance.GetQueuePoint(queueIndex);
        if (q != null) MoveTo(q.position);
    }

    public void AssignSeat(Seat seat)
    {
        assignedSeat = seat;

        // If frog was in a queue, attempt curved move around line (fallback to direct)
        if (queueIndex >= 0)
        {
            MoveFromQueueToSeat();
            queueIndex = -1;
        }
        else
        {
            MoveTo(assignedSeat.SeatPoint.position);
        }
    }

    // Helper: two-step move to avoid line (keeps same idea from prior code)
    private void MoveFromQueueToSeat()
    {
        if (assignedSeat == null || pathfinder == null || pathfinder.grid == null)
        {
            if (assignedSeat != null) MoveTo(assignedSeat.SeatPoint.position);
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

        List<Vector3> p1 = pathfinder.FindPath(transform.position, intermediate);
        List<Vector3> p2 = pathfinder.FindPath(intermediate, seatPos);

        if ((p1 == null || p1.Count == 0) || (p2 == null || p2.Count == 0))
        {
            List<Vector3> direct = pathfinder.FindPath(transform.position, seatPos);
            if (direct != null && direct.Count > 0) { path = direct; pathIndex = 0; UpdateAnimatorWalking(true); return; }
            if (p1 != null && p1.Count > 0) { path = p1; pathIndex = 0; UpdateAnimatorWalking(true); return; }
            MoveTo(seatPos); return;
        }

        // combine
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

        path = combined; pathIndex = 0; UpdateAnimatorWalking(true);
    }

    /// <summary>
    /// Normal MoveTo: get a path from pathfinder and start following it.
    /// </summary>
    public void MoveTo(Vector3 worldTarget)
    {
        if (pathfinder == null) pathfinder = FindObjectOfType<PathfinderAStar>();
        if (pathfinder == null || pathfinder.grid == null)
        {
            Debug.LogError($"{name}: Pathfinder or grid missing in MoveTo.", this);
            return;
        }

        var newPath = pathfinder.FindPath(transform.position, worldTarget);
        if (newPath == null || newPath.Count == 0)
        {
            // no path available
            path = new List<Vector3>();
            pathIndex = 0;
            UpdateAnimatorWalking(false);
            return;
        }

        path = newPath;
        pathIndex = 0;
        UpdateAnimatorWalking(true);
    }

    void Update()
    {
        // movement + avoidance only while following a path
        if (path != null && pathIndex < path.Count)
        {
            Vector3 nodeTarget = path[pathIndex];

            // compute the next target cell we will attempt to occupy
            Vector3Int nextCell = gridRef != null ? gridRef.WorldToCell(nodeTarget) : Vector3Int.FloorToInt(nodeTarget);

            // If we don't yet own the reservation for the next cell, try to reserve it
            if (reservedCell != nextCell)
            {
                // release any previous reservation we had (we are heading for a different cell)
                if (reservedCell != Vector3Int.zero)
                {
                    ReservationGrid.Instance?.Release(reservedCell, this);
                    reservedCell = Vector3Int.zero;
                }

                // try to reserve nextCell
                if (ReservationGrid.Instance != null)
                {
                    if (ReservationGrid.Instance.TryReserve(nextCell, this))
                    {
                        // success: mark it
                        reservedCell = nextCell;
                        reserveRetryTimer = 0f;
                    }
                    else
                    {
                        // failed: wait a bit and optionally re-request path later
                        reserveRetryTimer -= Time.deltaTime;
                        if (reserveRetryTimer <= 0f)
                        {
                            reserveRetryTimer = reserveRetryInterval;
                            // if blocked for some retries, optionally re-path to avoid deadlocks
                            // small heuristic: if owner is not moving, try to replan
                            var owner = ReservationGrid.Instance.GetOwner(nextCell);
                            if (owner != null && owner == this) { /* should not happen */ }
                            else
                            {
                                // optional: attempt to replan after a short wait
                                if (Random.value < 0.25f) // occasional replan, spread across frogs
                                    RepathToCurrentGoal();
                            }
                        }
                        // do not move if can't reserve target cell
                        UpdateAnimatorWalking(false);
                        return;
                    }
                }
            }

            // compute avoidance offset as before
            Vector3 avoidOffset = ComputeAvoidanceOffset();
            if (avoidOffset.magnitude > maxAvoidanceOffset) avoidOffset = avoidOffset.normalized * maxAvoidanceOffset;
            Vector3 finalTarget = nodeTarget + avoidOffset;

            // move
            transform.position = Vector3.MoveTowards(transform.position, finalTarget, speed * Time.deltaTime);

            // if close enough to the actual node (not offset), advance and release reservation
            if (Vector3.Distance(transform.position, nodeTarget) <= arriveThreshold)
            {
                // we successfully entered the reserved cell -> release previous cell reservation (if any)
                // release previous cell (the one behind us). Compute previous cell:
                if (pathIndex > 0 && gridRef != null)
                {
                    Vector3Int prevCell = gridRef.WorldToCell(path[pathIndex - 1]);
                    ReservationGrid.Instance?.Release(prevCell, this);
                }

                pathIndex++;
                if (pathIndex >= path.Count) OnReachedPathEnd();
            }
        }
        else
        {
            // not moving: if seated and not served, tick patience
            if (assignedSeat != null && !served)
            {
                orderTimer -= Time.deltaTime;
                if (orderTimer <= 0f) StartCoroutine(LeaveRoutine());
            }
        }
    }

    private void RepathToCurrentGoal()
{
    // Try to recompute a path to the same final destination (last node in path) to avoid static blockages
    if (pathfinder == null || path == null || path.Count == 0) return;
    Vector3 currentGoal = path[path.Count - 1];
    var newPath = pathfinder.FindPath(transform.position, currentGoal);
    if (newPath != null && newPath.Count > 0)
    {
        // release any current reservation (we will re-reserve next cells of new path)
        if (reservedCell != Vector3Int.zero)
        {
            ReservationGrid.Instance?.Release(reservedCell, this);
            reservedCell = Vector3Int.zero;
        }
        path = newPath;
        pathIndex = 0;
        UpdateAnimatorWalking(true);
    }
}


    /// <summary>
    /// Compute a separation vector from nearby frogs (simple weighted sum).
    /// Points away from neighbors; stronger when closer.
    /// </summary>
    Vector3 ComputeAvoidanceOffset()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        float r = avoidanceRadius;

        for (int i = 0; i < allFrogs.Count; i++)
        {
            var other = allFrogs[i];
            if (other == this || other.gameObject == null || !other.gameObject.activeInHierarchy) continue;

            Vector3 toMe = transform.position - other.transform.position;
            float dist = toMe.magnitude;
            if (dist <= 0f || dist > r) continue;

            // repulsion strength (inverse distance with falloff)
            float t = Mathf.Clamp01(1f - (dist / r));       // 1 at 0 dist, 0 at r
            float strength = Mathf.Pow(t, Mathf.Max(1f, avoidanceFalloff)) * avoidanceStrength;

            Vector3 repulseDir = (dist > 0f) ? toMe.normalized : (Random.insideUnitSphere.normalized);
            sum += repulseDir * strength;
            count++;
        }

        if (count == 0) return Vector3.zero;

        // average
        Vector3 avg = sum / Mathf.Max(1, count);
        // since we work in 2D, zero out Z
        avg.z = 0f;
        return avg;
    }

    void OnReachedPathEnd()
    {
        UpdateAnimatorWalking(false);

        // arrival at counter
        if (!reachedCounter && counterPoint != null)
        {
            reachedCounter = true;

            // if you want the "step down then left" behavior we discussed earlier, you can
            // StartCoroutine(StepOffCounterThenSeat(...));
            // otherwise proceed directly to seating logic:
            if (CafeManager.Instance.TryAssignSeat(this, out Seat seat))
            {
                AssignSeat(seat);
            }
            else
            {
                CafeManager.Instance.Enqueue(this);
            }
            return;
        }

        // seated arrival
        if (assignedSeat != null)
        {
            transform.position = assignedSeat.SeatPoint.position;
            StartCoroutine(SeatedRoutine());
            return;
        }
    }

    IEnumerator SeatedRoutine()
    {
        UpdateAnimatorSit(true);
        orderTimer = orderTime;
        while (!served && orderTimer > 0f) yield return null;

        if (served)
        {
            yield return new WaitForSeconds(servedLinger);
            StartCoroutine(LeaveRoutine());
        }
        else
        {
            StartCoroutine(LeaveRoutine());
        }
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

        // path to Exit
        Transform exitT = exitPoint;
        if (exitT == null)
        {
            var go = GameObject.FindWithTag("Exit");
            if (go != null) exitT = go.transform;
        }

        if (exitT != null && pathfinder != null)
        {
            MoveTo(exitT.position);
            while (path != null && pathIndex < path.Count) yield return null;
        }

        gameObject.SetActive(false);
    }

    #region Animator helpers
    void UpdateAnimatorWalking(bool walking) { if (animator != null) animator.SetBool("isWalking", walking); }
    void UpdateAnimatorSit(bool sit) { if (animator != null) animator.SetBool("isSitting", sit); }
    void UpdateAnimatorHappy() { if (animator != null) animator.SetTrigger("isHappy"); }
    #endregion
}
