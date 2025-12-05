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

    [Header("Sprite Cycle")]
    public Sprite idleSprite;
    public Sprite jumpSprite;
    public Sprite fallSprite;

    [Header("Directional Sprites")]
    public Sprite[] rightSprites;   // [idle, jump, fall]
    public Sprite[] leftSprites;    // [idle, jump, fall]
    public Sprite[] upSprites;      // [idle, jump, fall]
    public Sprite[] downSprites;    // [idle, jump, fall]

    [Header("Sprite Cycle Settings")]
    public float spriteCycleSpeed = 0.2f; // seconds per sprite

    private SpriteRenderer sr; // reference to SpriteRenderer

    [HideInInspector] public Transform counterPoint;
    private PathfinderAStar pathfinder;

    private List<Vector3> path = new List<Vector3>();
    private int pathIndex = 0;

    private Seat assignedSeat = null;
    public int queueIndex = -1;

    private bool reachedCounter = false;
    private bool served = false;
    private float orderTimer = 0f;

    private int lastDirection = 0; // 0 = left, 1 = right
    private int seatNumber = 1; 

    // Shared queue
    private static List<FrogAI> sharedQueue = new List<FrogAI>();

    private const float fallbackLateralSpacing = 0.35f;
    private const float fallbackForwardStep = 0.6f;

    // Sprite cycling
    private Vector3 lastPosition;
    private float spriteTimer = 0f;
    private int spriteIndex = 0;

    void Awake()
    {
        pathfinder = FindObjectOfType<PathfinderAStar>();
        sr = GetComponent<SpriteRenderer>();
        lastPosition = transform.position;
    }

    void OnEnable()
    {
        GameEngine.OnDayStarted += HandleDayStarted;
        path.Clear();
        pathIndex = 0;
        reachedCounter = false;
        served = false;
        assignedSeat = null;
        queueIndex = -1;

        orderTaken = false;
        receivedOrder = false;
        orderTimer = orderTime;
        lastPosition = transform.position;
        spriteTimer = 0f;
        spriteIndex = 0;
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

    private void HandleDayStarted(int dayIndex)
    {
        RemoveFromQueue(this); // safe no-op if not queued
        gameObject.SetActive(true);
        InitializeNew();
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

        if (CafeManager.Instance != null && frog.queueIndex >= 0)
            CafeManager.Instance.FreeQueueSpot(frog.queueIndex, frog);

        sharedQueue.Remove(frog);
        frog.queueIndex = -1;
        UpdateQueuePositions();
    }

    private static void UpdateQueuePositions()
    {
        if (sharedQueue == null) return;

        Transform[] queuePoints = (CafeManager.Instance != null) ? CafeManager.Instance.queuePoints : null;
        int spotCount = (queuePoints != null) ? queuePoints.Length : 0;

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

        for (int q = 0; q < sharedQueue.Count; q++)
        {
            var frog = sharedQueue[q];
            if (frog == null) continue;

            int desiredSpot = Mathf.Clamp(spotCount - 1 - q, 0, spotCount - 1);
            int chosenSpot = -1;

            if (CafeManager.Instance != null && frog.queueIndex >= 0 && frog.queueIndex != desiredSpot)
            {
                CafeManager.Instance.FreeQueueSpot(frog.queueIndex, frog);
            }

            if (CafeManager.Instance != null)
            {
                var occupant = CafeManager.Instance.queueOccupant[desiredSpot];
                if (occupant == null || occupant == frog)
                {
                    if (CafeManager.Instance.TryReserveQueueSpot(desiredSpot, frog))
                    {
                        chosenSpot = desiredSpot;
                    }
                }

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
                chosenSpot = desiredSpot;
            }

            if (chosenSpot == -1)
            {
                chosenSpot = desiredSpot;
            }

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

    public static FrogAI PopFrontOfQueue()
    {
        if (sharedQueue == null || sharedQueue.Count == 0) return null;
        FrogAI f = sharedQueue[0];

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

        seatNumber = assignedSeat.seatNumber;
        Debug.Log(seatNumber);

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
            if (direct != null && direct.Count > 0) { ClampPathToPlane(direct); path = direct; pathIndex = 0; return; }
            if (p1 != null && p1.Count > 0) { ClampPathToPlane(p1); path = p1; pathIndex = 0; return; }
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
        }
        else
        {
            path = new List<Vector3> { new Vector3(worldTarget.x, worldTarget.y, transform.position.z) };
            pathIndex = 0;
        }
    }

    void Update()
    {
        if (path != null && pathIndex < path.Count)
        {
            Vector3 target = path[pathIndex];
            Vector3 delta = target - transform.position;

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            // Determine movement direction
            float deltaX = transform.position.x - lastPosition.x;
            float deltaY = transform.position.y - lastPosition.y;

            Sprite[] currentSprites = null;

            if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY))
            {
                if (deltaX > 0.001f) { currentSprites = rightSprites; sr.flipX = true; lastDirection = 0; }
                else if (deltaX < -0.001f) { currentSprites = leftSprites; sr.flipX = false; lastDirection = 1; }
            }
            else if (Mathf.Abs(deltaY) > Mathf.Abs(deltaX))
            {
                if (deltaY > 0.001f) currentSprites = upSprites;
                else if (deltaY < -0.001f) currentSprites = downSprites;
            }

            // Cycle sprite
            if (currentSprites != null && currentSprites.Length == 3)
            {
                spriteTimer += Time.deltaTime;
                if (spriteTimer >= spriteCycleSpeed)
                {
                    spriteTimer = 0f;
                    spriteIndex = (spriteIndex + 1) % 3;
                }
                sr.sprite = currentSprites[spriteIndex];
            }

            lastPosition = transform.position;

            if (Vector3.Distance(new Vector2(transform.position.x, transform.position.y),
                                 new Vector2(target.x, target.y)) <= arriveThreshold)
            {
                pathIndex++;
                if (pathIndex >= path.Count)
                {
                    if (leftSprites != null && lastDirection == 0) sr.sprite = leftSprites[0];
                    if (rightSprites != null && lastDirection == 1) sr.sprite = rightSprites[0];
                    if (rightSprites != null && seatNumber % 2 == 1) sr.sprite = rightSprites[0];
                        OnReachedPathEnd();
                }
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



    void OnReachedPathEnd()
    {
        if (!reachedCounter && counterPoint != null)
        {
            if (IsAtCounter())
            {
                reachedCounter = true;
                FrogAI currentFront = GetFrontOfQueue();
                if (currentFront != null && currentFront != this && currentFront.IsAtCounter())
                {
                    JoinQueue();
                    return;
                }
                if (!sharedQueue.Contains(this))
                {
                    sharedQueue.Insert(0, this);
                    UpdateQueuePositions();
                }
                return;
            }
            else
            {
                if (queueIndex >= 0 && CafeManager.Instance != null)
                {
                    CafeManager.Instance.ConfirmQueueSpot(queueIndex, this);
                }
            }
        }

        if (assignedSeat != null)
        {
            Vector3 seat = assignedSeat.SeatPoint.position;
            transform.position = new Vector3(seat.x, seat.y, transform.position.z);
            StartCoroutine(SeatedRoutine());
            return;
        }
    }

    public void OrderTakenByPlayer()
    {
        if (!IsAtCounter()) return;
        FrogAI currentFront = GetFrontOfQueue();
        if (currentFront != null && currentFront != this && currentFront.IsAtCounter()) return;

        orderTaken = true;

        if (sharedQueue.Contains(this))
        {
            if (GetFrontOfQueue() == this) PopFrontOfQueue();
            else RemoveFromQueue(this);
        }

        if (CafeManager.Instance != null && CafeManager.Instance.TryAssignSeat(this, out Seat seat))
        {
            AssignSeat(seat);
        }
        else
        {
            JoinQueue();
        }
    }

    IEnumerator SeatedRoutine()
    {
        orderTimer = orderTime;
        while (!served && orderTimer > 0f) yield return null;
        if (served) { yield return new WaitForSeconds(servedLinger); StartCoroutine(LeaveRoutine()); }
        else StartCoroutine(LeaveRoutine());
    }

    public void Serve()
    {
        served = true;
    }

    IEnumerator LeaveRoutine()
    {
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

        // If the customer is leaving without having been served, end the day
        // (same effect as the timer running out). Guard against duplicate calls
        // if the game is already over.
        if (!served && !GameEngine.IsGameOver)
        {
            if (GameEngine.Instance != null)
                GameEngine.Instance.EndRunFailure("You (Ran out of time, Lost a customer)");
            else
                Debug.LogWarning("[FrogAI] GameEngine instance not found — cannot force end run.", this);
        }

        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        GameEngine.OnDayStarted -= HandleDayStarted;
        if (assignedSeat != null)
        {
            CafeManager.Instance.NotifySeatFreed(assignedSeat);
            assignedSeat = null;
        }

        RemoveFromQueue(this);

        orderTaken = false;
        receivedOrder = false;
    }

    #region Helpers & Anim

    void ClampPathToPlane(List<Vector3> p)
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

    public void ForceServeAndLeave()
    {
        served = true;

        if (assignedSeat != null)
        {
            if (CafeManager.Instance != null)
                CafeManager.Instance.NotifySeatFreed(assignedSeat);
            assignedSeat = null;
        }

        StopAllCoroutines();
        StartCoroutine(LeaveRoutine());
    }
}
#endregion