using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input (assign in inspector)")]
    public InputActionReference moveAction;

    [Header("Small Leap")]
    public float smallLeapDistance = 0.6f;
    public float smallLeapDuration = 0.14f;
    public float smallCooldown = 0.1f;

    [Header("Large Leap")]
    public float largeLeapDistance = 1.2f;
    public float largeLeapDuration = 0.12f;
    public float largeCooldown = 0.1f;

    [Header("Timing")]
    [Tooltip("How long you must hold (seconds) before subsequent leaps become large leaps.")]
    public float chargeTime = 0.20f;
    [Tooltip("Small grace window (seconds) to allow a near-simultaneous second keypress to form a diagonal.")]
    public float initialGraceWindow = 0.06f;

    [Header("Collision")]
    public LayerMask obstacleMask;
    public float obstacleCastPadding = 0.01f;

    [Header("Movement fallback")]
    [Tooltip("Minimum distance an axis must allow to be chosen when diagonal is blocked.")]
    public float axisFallbackThreshold = 0.1f;

    [Header("References")]
    public Rigidbody2D rb;
    public Collider2D coll;
    public SpriteRenderer spriteRenderer;

    bool isLeaping;
    bool holdLoopRunning;
    Vector2 heldDir = Vector2.zero;
    float pressStartTime = 0f;

    const float inputThreshold = 0.2f;

    [Header("Sprites")]
    public Sprite sideIdle;
    public Sprite sideJump;
    public Sprite sideFall;

    public Sprite frontIdle;
    public Sprite frontJump;
    public Sprite frontFall;

    public Sprite backIdle;
    public Sprite backJump;
    public Sprite backFall;

    void OnEnable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.performed += OnMovePerformed;
            moveAction.action.canceled += OnMoveCanceled;
            moveAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.performed -= OnMovePerformed;
            moveAction.action.canceled -= OnMoveCanceled;
            moveAction.action.Disable();
        }
    }

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (coll == null)
            coll = GetComponent<Collider2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        // record press start time if this is the first press in a chain
        if (heldDir == Vector2.zero)
            pressStartTime = Time.time;

        // update held direction immediately
        Vector2 raw = ctx.ReadValue<Vector2>();
        heldDir = GetDirectionVector(raw, inputThreshold);

        // start the grace+first-leap coroutine once per press
        if (!holdLoopRunning)
        {
            StartCoroutine(InitialGraceThenHoldLoop());
        }
    }

    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        // clearing heldDir will stop hold loop after current leap/cooldown finishes
        heldDir = Vector2.zero;
    }

    // Waits a short initial grace window (to allow a second key to produce a diagonal),
    // then immediately does a small (tap) leap and starts the hold loop which will produce subsequent leaps.
    // Subsequent leaps will use large jump parameters once the hold duration passes chargeTime.
    IEnumerator InitialGraceThenHoldLoop()
    {
        holdLoopRunning = true;

        // initial grace: for a tiny window, allow heldDir to update
        // so a near-simultaneous second key becomes diagonal.
        float tStart = Time.time;
        float tEnd = tStart + initialGraceWindow;

        Vector2 graceDir = heldDir;

        while (Time.time < tEnd)
        {
            // sample latest input from the action to capture second-key presses
            if (moveAction != null && moveAction.action != null)
            {
                Vector2 sample = moveAction.action.ReadValue<Vector2>();
                Vector2 d = GetDirectionVector(sample, inputThreshold);
                if (d != Vector2.zero)
                    graceDir = d;
            }

            // if the player released before the grace window ends, break early
            if (heldDir == Vector2.zero && Time.time - tStart > 0f)
                break;

            yield return null;
        }

        // if there was no meaningful direction
        if (graceDir == Vector2.zero)
        {
            holdLoopRunning = false;
            yield break;
        }

        // perform an immediate small leap for a quick press
        yield return StartCoroutine(Leap(graceDir, smallLeapDistance, smallLeapDuration, smallCooldown));

        // If the player released during the small leap and cooldown, we should end here.
        if (heldDir == Vector2.zero)
        {
            holdLoopRunning = false;
            yield break;
        }

        // Now enter the continuous hold loop for subsequent leaps while input is held.
        while (heldDir != Vector2.zero)
        {
            if (isLeaping)
            {
                yield return null;
                continue;
            }

            float heldTime = Time.time - pressStartTime;
            bool charged = heldTime >= chargeTime;

            float dist = charged ? largeLeapDistance : smallLeapDistance;
            float dur = charged ? largeLeapDuration : smallLeapDuration;
            float cd = charged ? largeCooldown : smallCooldown;

            // compute target using the current heldDir
            Vector2 currentInput = moveAction != null && moveAction.action != null ? moveAction.action.ReadValue<Vector2>() : heldDir;
            Vector2 dir = GetDirectionVector(currentInput, inputThreshold);
            if (dir == Vector2.zero)
            {
                dir = heldDir;
            }
            else
            {
                heldDir = dir;
            }

            Vector2 target = ComputeTarget(dir, dist);
            yield return StartCoroutine(Leap(dir, dist, dur, cd));
        }

        holdLoopRunning = false;
    }

    // Compute a target position in given direction for specific distance.
    Vector2 ComputeTarget(Vector2 dir, float distance)
    {
        const float touchEpsilon = 0.01f;

        dir = dir.normalized;
        Vector2 origin = rb.position;

        bool isDiagonal = !Mathf.Approximately(dir.x, 0f) && !Mathf.Approximately(dir.y, 0f);

        // --- Non-diagonal input: use standard logic ---
        if (!isDiagonal)
        {
            float avail = GetAvailableDistance(dir, distance);
            if (avail <= touchEpsilon) return origin;
            return origin + dir * Mathf.Min(avail, distance);
        }

        // --- Diagonal handling ---
        float availDiag = GetAvailableDistance(dir, distance);

        // If diagonal fully free, use it directly
        if (availDiag >= distance - Mathf.Epsilon)
            return origin + dir * distance;

        if (availDiag >= axisFallbackThreshold && availDiag > touchEpsilon)
            return origin + dir * Mathf.Min(availDiag, distance);

        // Otherwise check each axis individually
        Vector2 xDir = new Vector2(Mathf.Sign(dir.x), 0f);
        Vector2 yDir = new Vector2(0f, Mathf.Sign(dir.y));

        float availX = GetAvailableDistance(xDir, distance);
        float availY = GetAvailableDistance(yDir, distance);

        // Use direct world-unit threshold now
        bool xMeets = availX >= axisFallbackThreshold && availX > touchEpsilon;
        bool yMeets = availY >= axisFallbackThreshold && availY > touchEpsilon;

        if (xMeets && !yMeets)
            return origin + xDir * Mathf.Min(availX, distance);

        if (yMeets && !xMeets)
            return origin + yDir * Mathf.Min(availY, distance);

        if (xMeets && yMeets)
        {
            // both meet threshold: prefer farther axis
            if (availX >= availY)
                return origin + xDir * Mathf.Min(availX, distance);
            else
                return origin + yDir * Mathf.Min(availY, distance);
        }

        // Neither axis meets threshold — use partial diagonal if possible
        if (availDiag > touchEpsilon)
            return origin + dir * Mathf.Min(availDiag, distance);

        return origin; // fully blocked
    }

    // Helper (unchanged)
    float GetAvailableDistance(Vector2 dir, float maxDistance)
    {
        dir = dir.normalized;
        float castDist = maxDistance + obstacleCastPadding;

        RaycastHit2D[] hits = new RaycastHit2D[8];
        int hitCount = coll.Cast(dir, hits, castDist);

        float closest = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            if ((obstacleMask.value & (1 << h.collider.gameObject.layer)) == 0) continue;

            float candidate = Mathf.Max(0f, h.distance - obstacleCastPadding);
            if (candidate < closest) closest = candidate;
        }

        if (closest != float.PositiveInfinity)
            return Mathf.Max(0f, Mathf.Min(closest, maxDistance));

        return maxDistance;
    }


    IEnumerator Leap(Vector2 dir, float distance, float leapDuration, float cooldown)
    {
        if (isLeaping) yield break;

        Vector2 start = rb.position;
        Vector2 target = ComputeTarget(dir, distance);

        // If effectively not moving, still run a tiny "no-move" hop to respect cooldown + sprite
        if ((target - start).sqrMagnitude < 0.0001f)
        {
            isLeaping = true;
            SetSpriteForState("jump", dir);
            yield return new WaitForSeconds(cooldown);
            SetSpriteForState("idle", dir);
            isLeaping = false;
            yield break;
        }

        isLeaping = true;

        SetSpriteForState("jump", dir);

        // --- scale duration proportional to actual distance moved ---
        float actualDistance = Vector2.Distance(start, target);
        float effectiveDuration = leapDuration;
        if (distance > 0f)
        {
            // scale by fraction moved; clamp to avoid pathological tiny/zero durations
            float frac = Mathf.Clamp01(actualDistance / distance);
            effectiveDuration = leapDuration * frac;
            // optional: ensure we have a minimum duration so sprite transitions are visible
            effectiveDuration = Mathf.Max(effectiveDuration, 0.01f);
        }

        float elapsed = 0f;
        rb.linearVelocity = Vector2.zero;

        while (elapsed < effectiveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / effectiveDuration);
            float ease = t * t * (3f - 2f * t);

            Vector2 pos = Vector2.Lerp(start, target, ease);
            rb.MovePosition(pos);

            if (t > 0.5f)
            {
                SetSpriteForState("fall", dir);
            }

            yield return null;
        }

        // ensure final snap
        rb.MovePosition(target);

        SetSpriteForState("idle", dir);

        // keep cooldown behavior unchanged (you can scale this too if desired)
        yield return new WaitForSeconds(cooldown);

        isLeaping = false;
    }

    // Converts raw input to a normalized direction vector if above threshold.
    static Vector2 GetDirectionVector(Vector2 inVec, float threshold)
    {
        if (inVec.sqrMagnitude < (threshold * threshold)) return Vector2.zero;
        return inVec.normalized;
    }

    void SetSpriteForState(string state, Vector2 dir)
    {
        if (spriteRenderer == null) return;

        bool useVertical = Mathf.Abs(dir.y) > Mathf.Abs(dir.x);

        Sprite chosen = null;

        if (useVertical)
        {
            if (dir.y > 0f)
            {
                if (state == "idle") chosen = backIdle;
                else if (state == "jump") chosen = backJump;
                else if (state == "fall") chosen = backFall;
            }
            else
            {
                if (state == "idle") chosen = frontIdle;
                else if (state == "jump") chosen = frontJump;
                else if (state == "fall") chosen = frontFall;
            }
        }
        else
        {
            if (state == "idle") chosen = sideIdle;
            else if (state == "jump") chosen = sideJump;
            else if (state == "fall") chosen = sideFall;

            if (Mathf.Abs(dir.x) > 0.1f)
                spriteRenderer.flipX = dir.x > 0f;
        }

        if (chosen != null)
            spriteRenderer.sprite = chosen;
    }
}
