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
    public bool receivedOrder = false;    // has the tea been delievered
    private bool playerInRange = false;

    [Header("Optional")]
    public Animator animator;             // assign if you have animations

    [HideInInspector] public Transform counterPoint;
    private PathfinderAStar pathfinder;

    private List<Vector3> path = new List<Vector3>();
    private int pathIndex = 0;

    private Seat assignedSeat = null;
    private int queueIndex = -1;

    private bool reachedCounter = false;
    private bool served = false;
    private float orderTimer = 0f;

    // ----------------------------
    // Shared queue (global for all frogs)
    // ----------------------------
    private static List<FrogAI> sharedQueue = new List<FrogAI>();

    private const float fallbackLateralSpacing = 0.35f;
    private const float fallbackForwardStep = 0.6f;

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

        sharedQueue.Remove(frog);
        frog.queueIndex = -1;

        UpdateQueuePositions();
    }

    private static void UpdateQueuePositions()
    {
        for (int i = 0; i < sharedQueue.Count; i++)
        {
            FrogAI f = sharedQueue[i];
            if (f == null) continue;
            f.queueIndex = i;

            Transform q = null;
            if (CafeManager.Instance != null)
                q = CafeManager.Instance.GetQueuePoint(i);

            if (q != null)
            {
                f.MoveTo(q.position);
            }
            else
            {
                Vector3 fallback = ComputeFallbackQueuePosition(f, i);
                f.MoveTo(fallback);
            }
        }
    }

    private Transform GetQueueTransformForIndex(int idx)
    {
        if (CafeManager.Instance == null) return null;
        return CafeManager.Instance.GetQueuePoint(idx);
    }

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
    }

    public void InitializeNew()
    {
        orderTimer = orderTime;
        served = false;
        reachedCounter = false;
        assignedSeat = null;
        queueIndex = -1;
        orderTaken = false;
        receivedOrder = false;

        if (counterPoint != null)
        {
            MoveTo(counterPoint.position);
        }
    }

    public void SetQueueIndex(int index)
    {
        queueIndex = index;
        Transform q = GetQueueTransformForIndex(queueIndex);
        if (q != null) MoveTo(q.position);
        else MoveTo(ComputeFallbackQueuePosition(this, queueIndex));
    }

    public void AssignSeat(Seat seat)
    {
        RemoveFromQueue(this);

        assignedSeat = seat;

        if (queueIndex >= 0)
        {
            MoveFromQueueToSeat();
        }
        else
        {
            if (assignedSeat != null)
                MoveTo(assignedSeat.SeatPoint.position);
        }
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

        List<Vector3> pathToIntermediate = null;
        List<Vector3> pathIntermediateToSeat = null;

        if (pathfinder == null)
        {
            pathfinder = FindObjectOfType<PathfinderAStar>();
            if (pathfinder == null)
            {
                MoveTo(seatPos);
                return;
            }
        }
        if (pathfinder.grid == null)
        {
            MoveTo(seatPos);
            return;
        }

        pathToIntermediate = pathfinder.FindPath(transform.position, intermediate);
        pathIntermediateToSeat = pathfinder.FindPath(intermediate, seatPos);

        if ((pathToIntermediate == null || pathToIntermediate.Count == 0) ||
            (pathIntermediateToSeat == null || pathIntermediateToSeat.Count == 0))
        {
            List<Vector3> direct = pathfinder.FindPath(transform.position, seatPos);
            if (direct != null && direct.Count > 0)
            {
                ClampPathToPlane(direct);
                path = direct;
                pathIndex = 0;
                UpdateAnimatorWalking(true);
                return;
            }
            else
            {
                if (pathToIntermediate != null && pathToIntermediate.Count > 0)
                {
                    ClampPathToPlane(pathToIntermediate);
                    path = pathToIntermediate;
                    pathIndex = 0;
                    UpdateAnimatorWalking(true);
                    return;
                }

                MoveTo(seatPos);
                return;
            }
        }

        List<Vector3> combined = new List<Vector3>(pathToIntermediate);
        if (pathIntermediateToSeat.Count > 0)
        {
            Vector3 firstOfSecond = pathIntermediateToSeat[0];
            if (combined.Count > 0 && Vector3.Distance(combined[combined.Count - 1], firstOfSecond) < 0.01f)
            {
                for (int i = 1; i < pathIntermediateToSeat.Count; i++) combined.Add(pathIntermediateToSeat[i]);
            }
            else
            {
                combined.AddRange(pathIntermediateToSeat);
            }
        }

        ClampPathToPlane(combined);

        path = combined;
        pathIndex = 0;
        UpdateAnimatorWalking(true);

        queueIndex = -1;
    }

    public void MoveTo(Vector3 worldTarget)
    {
        if (pathfinder == null) return;
        List<Vector3> raw = pathfinder.FindPath(transform.position, worldTarget);
        if (raw != null)
        {
            ClampPathToPlane(raw);
            path = raw;
            pathIndex = 0;
        }
        else
        {
            path = new List<Vector3> { new Vector3(worldTarget.x, worldTarget.y, transform.position.z) };
            pathIndex = 0;
        }
    }

    void Update()
    {
        // Direct "E" input on the frog is no longer used to mark orders taken.
        // Only the Cashregister should call TakeOrderByPlayer() and that method
        // itself enforces that the frog is the front-of-line and physically at the counter.

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
                if (orderTimer <= 0f)
                {
                    StartCoroutine(LeaveRoutine());
                }
            }
            else if (served)
            {
                StartCoroutine(LeaveRoutine());
            }
        }
    }

    void OnReachedPathEnd()
    {
        UpdateAnimatorWalking(false);

        // If we haven't reached counter yet, treat this as arrival to counter
        if (!reachedCounter && counterPoint != null)
        {
            reachedCounter = true;

            // If someone else is already occupying the counter (front of shared queue),
            // and it's not us, join the queue instead of staying at the counter.
            FrogAI currentFront = GetFrontOfQueue();
            if (currentFront != null && currentFront != this && currentFront.IsAtCounter())
            {
                // Move into the shared queue (will update everyone positions).
                JoinQueue();
                return;
            }

            // If order has already been taken (player interacted while we were approaching)
            if (orderTaken)
            {
                if (CafeManager.Instance.TryAssignSeat(this, out Seat seat))
                {
                    AssignSeat(seat);
                }
                else
                {
                    JoinQueue();
                }
            }
            else
            {
                // If no order yet and no one else is occupying the counter, we should become the front-of-line.
                // Ensure we're at front of shared queue so Cashregister logic recognizes us.
                if (!sharedQueue.Contains(this))
                {
                    sharedQueue.Insert(0, this);
                    UpdateQueuePositions();
                }
                // Remain at counter waiting for player to take order.
            }
            return;
        }

        if (assignedSeat != null)
        {
            Vector3 seat = assignedSeat.SeatPoint.position;
            transform.position = new Vector3(seat.x, seat.y, transform.position.z);
            StartCoroutine(SeatedRoutine());
            return;
        }
    }

    IEnumerator SeatedRoutine()
    {
        UpdateAnimatorSit(true);

        orderTimer = orderTime;
        while (!served && orderTimer > 0f)
            yield return null;

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

        RemoveFromQueue(this);

        GameObject exitGO = GameObject.FindWithTag("Exit");
        if (exitGO != null && pathfinder != null)
        {
            MoveTo(exitGO.transform.position);
            while (path != null && pathIndex < path.Count)
                yield return null;
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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log(sharedQueue.Count);
            
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
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

    /// <summary>
    /// Remove and return the front frog from the shared queue (or null if none).
    /// Caller becomes responsible for assigning seat, etc.
    /// </summary>
    public static FrogAI PopFrontOfQueue()
    {
        if (sharedQueue == null || sharedQueue.Count == 0) return null;
        FrogAI f = sharedQueue[0];
        sharedQueue.RemoveAt(0);
        if (f != null) f.queueIndex = -1;
        UpdateQueuePositions();
        return f;
    }

    public bool IsAtCounter(float tolerance = 0.08f)
    {
        if (counterPoint == null) return false;
        float d = Vector2.Distance(new Vector2(transform.position.x, transform.position.y),
                                   new Vector2(counterPoint.position.x, counterPoint.position.y));
        return d <= tolerance;
    }

    public void TakeOrderByPlayer()
    {
        // Prevent orders unless this frog is physically the front-of-line and at the counter.
        if (orderTaken) return;

        FrogAI front = GetFrontOfQueue();
        if (front != this)
        {
            Debug.LogWarning("[FrogAI] TakeOrderByPlayer rejected: frog is not front of queue.");
            return;
        }

        if (!IsAtCounter())
        {
            Debug.LogWarning("[FrogAI] TakeOrderByPlayer rejected: frog is not at the counter.");
            return;
        }

        // Mark order taken and immediately attempt to seat.
        orderTaken = true;

        // Ensure this frog is front in shared queue (defensive).
        if (!sharedQueue.Contains(this))
        {
            sharedQueue.Insert(0, this);
            UpdateQueuePositions();
        }
        else
        {
            if (sharedQueue.Count == 0 || sharedQueue[0] != this)
            {
                sharedQueue.Remove(this);
                sharedQueue.Insert(0, this);
                UpdateQueuePositions();
            }
        }

        if (CafeManager.Instance != null && CafeManager.Instance.TryAssignSeat(this, out Seat seat))
        {
            AssignSeat(seat);
        }
        else
        {
            // No seat free -> stay/queue (already front)
        }
    }
}
