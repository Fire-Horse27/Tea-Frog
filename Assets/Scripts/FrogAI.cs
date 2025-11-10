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
    public void AssignSeat(Seat seat)
    {
        assignedSeat = seat;
        MoveTo(assignedSeat.SeatPoint.position);
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
        if (!reachedCounter && counterPoint != null)
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
}
