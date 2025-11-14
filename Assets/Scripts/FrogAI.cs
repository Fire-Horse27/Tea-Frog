using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FrogAI2D: 2D customer behaviour for tilemap-based pathfinding.
/// Attach to the Frog/Customer prefab. File name: FrogAI2D.cs
/// Spawner must set frog.counterPoint before calling InitializeNew().
/// Expects a PathfinderAStar component in scene and a CafeManager2D for seat logic.
/// </summary>
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
    public bool receivedOrder = false;    // has the tea been delievered
    private bool playerInRange = false;

    [Header("Optional")]
    public Animator animator;             // assign if you have animations

    // Runtime / scene refs (Spawner should set counterPoint)
    [HideInInspector] public Transform counterPoint;
    private PathfinderAStar pathfinder;

    // Path-following state
    private List<Vector3> path = new List<Vector3>();
    private int pathIndex = 0;

    // Seating/queue state
    private Seat assignedSeat = null;
    private int queueIndex = -1;

    // Flags & timers
    private bool reachedCounter = false;
    private bool served = false;
    private float orderTimer = 0f;

    void Awake()
    {
        pathfinder = FindObjectOfType<PathfinderAStar>();
        if (pathfinder == null)
            Debug.LogError("[FrogAI2D] No PathfinderAStar found in the scene.");
    }

    void OnEnable()
    {
        // clear runtime state (Spawner will call InitializeNew)
        path.Clear();
        pathIndex = 0;
        reachedCounter = false;
        served = false;
        assignedSeat = null;
        queueIndex = -1;
    }

    /// <summary>
    /// Call this immediately after the spawner activates the frog.
    /// Spawner should set counterPoint beforehand: frog.counterPoint = counterTransform;
    /// </summary>
    public void InitializeNew()
    {
        orderTimer = orderTime;
        served = false;
        reachedCounter = false;
        assignedSeat = null;
        queueIndex = -1;

        if (counterPoint != null)
        {
            MoveTo(counterPoint.position);
            Debug.Log("Is trying to move to the counter");
        }
        else
            Debug.LogWarning("[FrogAI2D] InitializeNew called but counterPoint is null.");
    }

    /// <summary>
    /// Called by CafeManager2D to give this frog a queue index.
    /// Moves to the queue point automatically.
    /// </summary>
    public void SetQueueIndex(int index)
    {
        queueIndex = index;
        Transform q = CafeManager.Instance.GetQueuePoint(queueIndex);
        if (q != null) MoveTo(q.position);
    }

    /// <summary>
    /// Called by CafeManager2D to assign a seat.
    /// </summary>
    //public void AssignSeat(Seat seat)
    //{
    //    assignedSeat = seat;
    //    MoveTo(assignedSeat.SeatPoint.position);
    //}

    // Replace the existing AssignSeat method with this:
    public void AssignSeat(Seat seat)
    {
        assignedSeat = seat;

        // If we were in the queue, move out of the line first using an intermediate waypoint
        if (queueIndex >= 0)
        {
            MoveFromQueueToSeat();
        }
        else
        {
            // not in queue — move directly
            MoveTo(assignedSeat.SeatPoint.position);
        }
    }

    /// <summary>
    /// When a frog is in the queue and gets a seat, compute a small two-step path:
    /// 1) an intermediate waypoint in front of the frog (toward counter/seat) with a perpendicular offset
    /// 2) then the seat position
    /// This nudges the frog around the line so it doesn't collide with waiting frogs.
    /// </summary>
    private void MoveFromQueueToSeat()
    {
        if (assignedSeat == null || pathfinder == null)
        {
            // fallback
            MoveTo(assignedSeat != null ? assignedSeat.SeatPoint.position : transform.position);
            return;
        }

        // Direction from frog to the seat (or counter if seat is far)
        Vector3 seatPos = assignedSeat.SeatPoint.position;
        Vector3 dirToSeat = (seatPos - transform.position).normalized;
        if (dirToSeat.sqrMagnitude < 0.0001f) dirToSeat = Vector3.up; // fallback

        // Step forward distance (how far to move out of the queue before turning)
        float forwardStep = 0.6f; // tweak: how many world units to step toward the counter
        Vector3 forwardPoint = transform.position + dirToSeat * forwardStep;

        // Perpendicular offset to skirt the line — scale by queueIndex so those further back take wider path
        // Use a perpendicular in X/Y plane: Vector3(-y, x, 0) is one perpendicular of (x,y,0).
        Vector3 perp = new Vector3(-dirToSeat.y, dirToSeat.x, 0f).normalized;
        float lateralSpacing = 0.35f; // tweak spacing between frogs
                                      // Alternate side depending on whether index is odd/even to avoid everyone going same side
        float sideMultiplier = (queueIndex % 2 == 0) ? 1f : -1f;
        float lateralOffset = lateralSpacing * Mathf.Ceil(queueIndex / 2f) * sideMultiplier;

        Vector3 intermediate = forwardPoint + perp * lateralOffset;

        // Get paths for the two legs
        List<Vector3> pathToIntermediate = null;
        List<Vector3> pathIntermediateToSeat = null;

        // Defensive: ensure tilemap grid etc are available
        if (pathfinder == null)
        {
            pathfinder = FindObjectOfType<PathfinderAStar>();
            if (pathfinder == null)
            {
                // fallback to direct move
                MoveTo(seatPos);
                return;
            }
        }
        if (pathfinder.grid == null)
        {
            // fallback
            MoveTo(seatPos);
            return;
        }

        // Request the two partial paths
        pathToIntermediate = pathfinder.FindPath(transform.position, intermediate);
        pathIntermediateToSeat = pathfinder.FindPath(intermediate, seatPos);

        // If either is empty, try a direct path to the seat
        if ((pathToIntermediate == null || pathToIntermediate.Count == 0) ||
            (pathIntermediateToSeat == null || pathIntermediateToSeat.Count == 0))
        {
            // try direct seat path
            List<Vector3> direct = pathfinder.FindPath(transform.position, seatPos);
            if (direct != null && direct.Count > 0)
            {
                path = direct;
                pathIndex = 0;
                UpdateAnimatorWalking(true);
                return;
            }
            else
            {
                // last resort: just try moving to intermediate (even if partial)
                if (pathToIntermediate != null && pathToIntermediate.Count > 0)
                {
                    path = pathToIntermediate;
                    pathIndex = 0;
                    UpdateAnimatorWalking(true);
                    return;
                }

                // nothing workable — bail
                Debug.LogWarning($"{name}: MoveFromQueueToSeat couldn't build a valid path; defaulting to seat pos", this);
                MoveTo(seatPos);
                return;
            }
        }

        // Combine the two paths but avoid duplicating the intermediate node
        List<Vector3> combined = new List<Vector3>(pathToIntermediate);
        // skip the first node of the second path if it's the same as the last node of first
        if (pathIntermediateToSeat.Count > 0)
        {
            Vector3 firstOfSecond = pathIntermediateToSeat[0];
            if (combined.Count > 0 && Vector3.Distance(combined[combined.Count - 1], firstOfSecond) < 0.01f)
            {
                // append second path skipping first element
                for (int i = 1; i < pathIntermediateToSeat.Count; i++) combined.Add(pathIntermediateToSeat[i]);
            }
            else
            {
                // append whole second path
                combined.AddRange(pathIntermediateToSeat);
            }
        }

        // Set the frog's path to the combined path
        path = combined;
        pathIndex = 0;
        UpdateAnimatorWalking(true);

        // Clear queue index now that frog is leaving the line
        queueIndex = -1;
    }


    /// <summary>
    /// Request a path from current position to target world position.
    /// </summary>
    public void MoveTo(Vector3 worldTarget)
    {
        if (pathfinder == null) return;
        path = pathfinder.FindPath(transform.position, worldTarget);
        pathIndex = 0;
        //UpdateAnimatorWalking(path != null && path.Count > 0);
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            orderTaken = true;
            Debug.Log($"{name} has been served!");
        }

        // Follow path if one exists
        if (path != null && pathIndex < path.Count)
        {
            Vector3 target = path[pathIndex];
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, target) <= arriveThreshold)
            {
                pathIndex++;
                if (pathIndex >= path.Count) OnReachedPathEnd();
            }
        }
        else
        {
            // If seated and not served, reduce patience
            if (assignedSeat != null && !served)
            {
                orderTimer -= Time.deltaTime;
                if (orderTimer <= 0f)
                {
                    // leave unsatisfied
                    StartCoroutine(LeaveRoutine());
                }
            }
        }
    }

    /// <summary>
    /// Called when the frog reaches the final point of its current path.
    /// Handles arriving at counter, queue spot, or seat.
    /// </summary>
    void OnReachedPathEnd()
    {
        UpdateAnimatorWalking(false);

        // If we haven't reached counter yet, treat this as arrival to counter
        if (!reachedCounter && counterPoint != null && orderTaken)
        {
            // mark we reached counter and request seat or queue
            reachedCounter = true;

            if (CafeManager.Instance.TryAssignSeat(this, out Seat seat))
            {
                AssignSeat(seat);
            }
            else
            {
                CafeManager.Instance.Enqueue(this); // will call SetQueueIndex on us
            }
            return;
        }

        // If we have an assigned seat and reached it -> snap and sit
        if (assignedSeat != null)
        {
            transform.position = assignedSeat.SeatPoint.position; // precise snap
            StartCoroutine(SeatedRoutine());
            return;
        }

        // Otherwise we're at a queue point; just wait until CafeManager assigns a seat
    }

    IEnumerator SeatedRoutine()
    {
        UpdateAnimatorSit(true);

        orderTimer = orderTime;
        while (!served && orderTimer > 0f)
            yield return null;

        if (served)
        {
            // linger a little when served
            yield return new WaitForSeconds(servedLinger);
            StartCoroutine(LeaveRoutine());
        }
        else
        {
            // impatient leave
            StartCoroutine(LeaveRoutine());
        }
    }

    /// <summary>
    /// Called externally (player/server) to mark this frog as served.
    /// </summary>
    public void Serve()
    {
        if (assignedSeat == null) return;
        served = true;
        UpdateAnimatorHappy();
        // The seat is freed by leaving routine so next can be seated
    }

    IEnumerator LeaveRoutine()
    {
        UpdateAnimatorSit(false);
        UpdateAnimatorWalking(true);

        // Free the seat immediately so next frog can be seated
        if (assignedSeat != null)
        {
            CafeManager.Instance.NotifySeatFreed(assignedSeat);
            assignedSeat = null;
        }

        // Path to Exit object (tagged "Exit")
        GameObject exitGO = GameObject.FindWithTag("Exit");
        if (exitGO != null && pathfinder != null)
        {
            MoveTo(exitGO.transform.position);
            // wait until path completed (Update will drive movement)
            while (path != null && pathIndex < path.Count)
                yield return null;
        }

        // deactivate (return to pool)
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // safety: if deactivated unexpectedly, free seat
        if (assignedSeat != null)
        {
            CafeManager.Instance.NotifySeatFreed(assignedSeat);
            assignedSeat = null;
        }

        orderTaken = false;

    }

    #region Animator helpers
    void UpdateAnimatorWalking(bool walking)
    {
        if (animator == null) return;
        animator.SetBool("isWalking", walking);
    }
    void UpdateAnimatorSit(bool sit)
    {
        if (animator == null) return;
        animator.SetBool("isSitting", sit);
    }
    void UpdateAnimatorHappy()
    {
        if (animator == null) return;
        animator.SetTrigger("isHappy");
    }
    #endregion

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log($"Player entered {name}'s trigger.");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            Debug.Log($"Player left {name}'s trigger.");
        }
    }

}
