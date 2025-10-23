using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input (assign in inspector)")]
    public InputActionReference moveAction; // Value / Vector2 (WASD composite)

    [Header("Leap")]
    public float leapDistance = 1f;
    public float leapDuration = 0.18f;
    public float cooldown = 0.12f;

    [Header("Collision")]
    public LayerMask obstacleMask;
    public float obstacleCastPadding = 0.02f;

    [Header("References (assign in inspector)")]
    public Animator animator;
    public Rigidbody2D rb;
    public Collider2D coll;

    bool isLeaping;
    Vector2 heldCardinal = Vector2.zero;

    void OnEnable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.started += OnMoveStarted;
            moveAction.action.canceled += OnMoveCanceled;
            moveAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.started -= OnMoveStarted;
            moveAction.action.canceled -= OnMoveCanceled;
            moveAction.action.Disable();
        }
    }

    void Awake()
    {
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void OnMoveStarted(InputAction.CallbackContext ctx)
    {
        if (isLeaping) return;

        Vector2 raw = ctx.ReadValue<Vector2>();
        Vector2 card = GetCardinalDirection(raw, 0.5f);
        if (card == Vector2.zero) return;

        heldCardinal = card;
        Vector2 target = ComputeTarget(card);
        StartCoroutine(DoFlatLeap(target));
    }

    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        heldCardinal = Vector2.zero;
    }

    static Vector2 GetCardinalDirection(Vector2 inVec, float threshold)
    {
        if (inVec.sqrMagnitude < (threshold * threshold)) return Vector2.zero;

        if (Mathf.Abs(inVec.x) > Mathf.Abs(inVec.y))
            return inVec.x > 0f ? Vector2.right : Vector2.left;
        else
            return inVec.y > 0f ? Vector2.up : Vector2.down;
    }

    Vector2 ComputeTarget(Vector2 dir)
    {
        Vector2 origin = rb.position;
        Vector2 size = new Vector2(
            coll.bounds.size.x - obstacleCastPadding,
            coll.bounds.size.y - obstacleCastPadding
        );

        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, dir, leapDistance, obstacleMask);
        if (hit.collider != null)
        {
            float distance = Mathf.Max(0f, hit.distance - 0.01f);
            return origin + dir * distance;
        }

        return origin + dir * leapDistance;
    }

    IEnumerator DoFlatLeap(Vector2 target)
    {
        if (isLeaping) yield break;
        Vector2 start = rb.position;
        if ((target - start).sqrMagnitude < 0.0001f) yield break;

        isLeaping = true;

        if (animator != null)
        {
            animator.SetTrigger("Leap");
            animator.SetFloat("Speed", 1f);
        }

        float elapsed = 0f;
        var prevBody = rb.bodyType;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero; // ? modern API

        while (elapsed < leapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / leapDuration);
            float ease = t * t * (3f - 2f * t); // smoothstep

            Vector2 pos = Vector2.Lerp(start, target, ease);
            rb.MovePosition(pos); // ? correct positional API
            yield return null;
        }

        rb.MovePosition(target);

        if (animator != null)
        {
            animator.SetTrigger("Land");
            animator.SetFloat("Speed", 0f);
        }

        rb.bodyType = prevBody;
        yield return new WaitForSeconds(cooldown);

        heldCardinal = Vector2.zero;
        isLeaping = false;
    }

    void OnDrawGizmosSelected()
    {
        if (coll == null) return;
        Vector2 size2 = new Vector2(
            coll.bounds.size.x - obstacleCastPadding,
            coll.bounds.size.y - obstacleCastPadding
        );
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(size2.x, size2.y, 0f));
    }
}
