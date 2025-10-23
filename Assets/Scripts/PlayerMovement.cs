using System.Collections;
using UnityEngine;

/// <summary>
/// Frog-like leap movement:
/// - Grid mode (1 unity unit = 1 tile) by default, but can work free-space by setting gridMode=false
/// - Uses a coroutine to move from start to target with an arc
/// - Checks for obstacles using Physics2D.BoxCast (configurable obstacle layer mask)
/// - Triggers Animator params/triggers: "Leap" (start) and "Land" (end) and sets "Speed" float (optional)
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class FrogLeapController : MonoBehaviour
{
    [Header("Mode")]
    public bool gridMode = true;                 // true -> target snapped to grid (1 unit steps). false -> continuous leaps
    public Vector2Int gridSize = new Vector2Int(1, 1); // if you want leaps of multiple tiles (in grid units)

    [Header("Leap settings")]
    public float leapDistance = 1f;              // in Unity units (if gridMode true keep at 1 or multiples of 1)
    public float leapDuration = 0.18f;           // time to travel from start -> target
    public float cooldown = 0.12f;               // time after landing before next leap allowed
    public float arcHeight = 0.25f;              // height of vertical arc (in local units)

    [Header("Collision")]
    public LayerMask obstacleMask;               // layers considered blocking (Tilemap colliders, walls, etc.)
    public float obstacleCastPadding = 0.02f;    // small padding for boxcast to avoid false positives

    [Header("Input")]
    public bool useRawInput = true;              // use GetAxisRaw or GetAxis for smoothing
    public KeyCode leapKey = KeyCode.Space;      // optionally require a separate key to leap instead of auto on direction

    [Header("References / tuning")]
    public Animator animator;                    // optional, set in inspector
    public Rigidbody2D rb;                       // optional, auto-assigned
    public Collider2D coll;                      // optional, auto-assigned

    // internal
    bool isLeaping = false;
    bool allowInput = true;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (coll == null) coll = GetComponent<Collider2D>();

        // We'll control movement with kinematic style while leaping to avoid physics jitter.
        // If you need physics interactions while leaping, change to Dynamic but handle collisions carefully.
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    void Update()
    {
        if (!allowInput || isLeaping) return;

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (!useRawInput)
            input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        // Normalize so diagonal isn't longer
        if (input.sqrMagnitude > 1f) input.Normalize();

        // require discrete direction for grid mode; use the dominant axis so you leap in cardinal directions
        Vector2 dir = Vector2.zero;
        if (input.sqrMagnitude > 0.01f)
        {
            if (gridMode)
            {
                // pick the dominant axis to get cardinal movement (N/S/E/W)
                if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                    dir = new Vector2(Mathf.Sign(input.x), 0f);
                else
                    dir = new Vector2(0f, Mathf.Sign(input.y));
            }
            else
            {
                dir = input.normalized;
            }
        }

        // Optionally require a separate key press to initiate leap (space or custom)
        bool wantLeap = dir != Vector2.zero && (leapKey == KeyCode.None || Input.GetKeyDown(leapKey) || Input.GetKey(leapKey));

        if (wantLeap)
        {
            Vector2 target = ComputeTarget(dir);
            StartCoroutine(DoLeap(target));
        }
    }

    Vector2 ComputeTarget(Vector2 dir)
    {
        if (gridMode)
        {
            Vector2Int gridDir = new Vector2Int((int)Mathf.Sign(dir.x), (int)Mathf.Sign(dir.y));
            Vector2 delta = new Vector2(gridDir.x * gridSize.x, gridDir.y * gridSize.y) * leapDistance;
            Vector2 candidate = (Vector2)transform.position + delta;

            // Check for obstacles: we cast a box from current center to target center,
            // but simpler: check if target cell overlaps anything blocking.
            // We'll perform a small box cast at the target position using player's collider bounds.
            Bounds b = coll.bounds;
            Vector2 boxSize = new Vector2(b.size.x - obstacleCastPadding, b.size.y - obstacleCastPadding);

            Collider2D hit = Physics2D.OverlapBox(candidate, boxSize, 0f, obstacleMask);
            if (hit != null)
            {
                // blocked. We'll try to step only one unit shorter (if gridSize>1) or bail.
                // Simple strategy: if there is obstacle at target, try immediate neighbour (distance 1) before fully blocked.
                Vector2 alt = (Vector2)transform.position + new Vector2(gridDir.x, gridDir.y) * leapDistance;
                Collider2D hit2 = Physics2D.OverlapBox(alt, boxSize, 0f, obstacleMask);
                if (hit2 == null)
                    return alt;

                // completely blocked; stay in place (target equals current)
                return transform.position;
            }

            return candidate;
        }
        else
        {
            // continuous mode: try to leap by leapDistance in dir, but stop early if obstacle.
            RaycastHit2D hit = Physics2D.BoxCast(transform.position, coll.bounds.size - new Vector3(obstacleCastPadding, obstacleCastPadding), 0f, dir, leapDistance, obstacleMask);
            if (hit.collider != null)
            {
                // land just before obstacle
                float distance = Mathf.Max(0f, hit.distance - 0.01f);
                return (Vector2)transform.position + dir.normalized * distance;
            }
            return (Vector2)transform.position + dir.normalized * leapDistance;
        }
    }

    IEnumerator DoLeap(Vector2 target)
    {
        if (isLeaping) yield break; // safety
        Vector2 start = transform.position;

        // If target equals start -> nothing to do
        if ((target - start).sqrMagnitude < 0.0001f) yield break;

        isLeaping = true;
        allowInput = false;

        // Animator: start leap
        if (animator) animator.SetTrigger("Leap");
        // optional: set Speed param
        if (animator) animator.SetFloat("Speed", 1f);

        float elapsed = 0f;

        // Switch Rigidbody to kinematic behavior for deterministic MovePosition
        // We'll store original bodyType if needed (not necessary if you used Dynamic with gravity=0)
        var prevBodyType = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        while (elapsed < leapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / leapDuration);
            // ease in-out (smoothstep)
            float ease = t * t * (3f - 2f * t);

            // linear interp for x/y
            Vector2 pos = Vector2.Lerp(start, target, ease);

            // vertical arc via sin - adds simple frog hop
            float arc = Mathf.Sin(Mathf.PI * ease) * arcHeight;
            // apply to transform.localPosition.y (so sprite moves visually above base) OR adjust sprite renderer offset
            // We'll move the transform for simplicity, but if you rely on physics collisions while leaping, consider offsetting only the sprite.
            Vector3 pos3 = new Vector3(pos.x, pos.y + arc, transform.position.z);

            rb.MovePosition(pos3); // MovePosition works well with Kinematic rb

            yield return null;
        }

        // ensure final position exactly the target (and arc 0)
        rb.MovePosition(new Vector3(target.x, target.y, transform.position.z));

        // Animator: land
        if (animator) animator.SetTrigger("Land");
        if (animator) animator.SetFloat("Speed", 0f);

        // restore body type
        rb.bodyType = prevBodyType;

        // small landing pause
        yield return new WaitForSeconds(cooldown);

        allowInput = true;
        isLeaping = false;
    }

    // Optional: visualize boxcast area in editor
    void OnDrawGizmosSelected()
    {
        if (coll == null) coll = GetComponent<Collider2D>();
        Gizmos.color = Color.cyan;
        Vector2 size = coll != null ? coll.bounds.size : Vector2.one;
        Vector2 boxSize = size - new Vector2(obstacleCastPadding, obstacleCastPadding);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawWireCube(transform.position, boxSize);
    }
}
