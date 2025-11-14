using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class MovementEngine : MonoBehaviour
{
    [Header("Small Leap")]
    public float smallLeapDistance = 0.6f;
    public float smallLeapDuration = 0.14f;
    public float smallCooldown = 0.1f;

    [Header("Large Leap")]
    public float largeLeapDistance = 1.2f;
    public float largeLeapDuration = 0.12f;
    public float largeCooldown = 0.1f;

    [Header("Timing")]
    public float chargeTime = 0.20f;
    public float initialGraceWindow = 0.06f;

    [Header("Collision")]
    public LayerMask obstacleMask;
    public float obstacleCastPadding = 0.01f;

    [Header("Movement fallback")]
    public float axisFallbackThreshold = 0.1f;

    [Header("References")]
    public Rigidbody2D rb;
    public Collider2D coll;
    public SpriteRenderer spriteRenderer;

    public bool isLeaping { get; private set; }
    public event Action<LeapInfo> onLeapStarted;

    [Serializable]
    public struct LeapInfo
    {
        public Vector2 dir;
        public Vector2 start;
        public Vector2 target;
        public float duration;
        public float cooldown;
    }

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();
    }

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (coll == null) coll = GetComponent<Collider2D>();
    }

    // Public API to compute the target given a direction and intended distance
    public Vector2 ComputeTarget(Vector2 dir, float distance)
    {
        const float touchEpsilon = 0.01f;
        dir = dir.normalized;
        Vector2 origin = rb.position;

        bool isDiagonal = !Mathf.Approximately(dir.x, 0f) && !Mathf.Approximately(dir.y, 0f);

        if (!isDiagonal)
        {
            float avail = GetAvailableDistance(dir, distance);
            if (avail <= touchEpsilon) return origin;
            return origin + dir * Mathf.Min(avail, distance);
        }

        float availDiag = GetAvailableDistance(dir, distance);
        if (availDiag >= distance - Mathf.Epsilon)
            return origin + dir * distance;

        if (availDiag >= axisFallbackThreshold && availDiag > touchEpsilon)
            return origin + dir * Mathf.Min(availDiag, distance);

        Vector2 xDir = new Vector2(Mathf.Sign(dir.x), 0f);
        Vector2 yDir = new Vector2(0f, Mathf.Sign(dir.y));

        float availX = GetAvailableDistance(xDir, distance);
        float availY = GetAvailableDistance(yDir, distance);

        bool xMeets = availX >= axisFallbackThreshold && availX > touchEpsilon;
        bool yMeets = availY >= axisFallbackThreshold && availY > touchEpsilon;

        if (xMeets && !yMeets)
            return origin + xDir * Mathf.Min(availX, distance);

        if (yMeets && !xMeets)
            return origin + yDir * Mathf.Min(availY, distance);

        if (xMeets && yMeets)
        {
            if (availX >= availY)
                return origin + xDir * Mathf.Min(availX, distance);
            else
                return origin + yDir * Mathf.Min(availY, distance);
        }

        if (availDiag > touchEpsilon)
            return origin + dir * Mathf.Min(availDiag, distance);

        return origin;
    }

    // Cast the collider to find the available distance in dir up to maxDistance
    public float GetAvailableDistance(Vector2 dir, float maxDistance)
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

    // Public: command an immediate leap using "small" or "large" style based on 'useLarge'
    public void StartLeapInDirection(Vector2 dir, bool useLarge = false)
    {
        float distance = useLarge ? largeLeapDistance : smallLeapDistance;
        float duration = useLarge ? largeLeapDuration : smallLeapDuration;
        float cooldown = useLarge ? largeCooldown : smallCooldown;

        Vector2 target = ComputeTarget(dir, distance);
        // pass intended distance so duration scaling matches PlayerMovement
        StartLeapToTarget(target, duration, cooldown, dir, distance);
    }

    // Public: command a leap to an explicit target world position
    // 'intendedDistance' is optional; when provided the leap-duration scaling will match PlayerMovement exactly.
    public void StartLeapToTarget(Vector2 target, float duration, float cooldown, Vector2 dirHint, float intendedDistance = -1f)
    {
        if (isLeaping) return;
        Vector2 start = rb.position;

        // If effectively no movement, still produce a tiny hop for sprite/cooldown consistency
        if ((target - start).sqrMagnitude < 0.0001f)
        {
            StartCoroutine(NoMoveHopCoroutine(cooldown));
            LeapInfo tiny = new LeapInfo { dir = dirHint, start = start, target = start, duration = 0f, cooldown = cooldown };
            onLeapStarted?.Invoke(tiny);
            return;
        }

        StartCoroutine(LeapCoroutine(start, target, duration, cooldown, dirHint, intendedDistance));
    }

    IEnumerator NoMoveHopCoroutine(float cooldown)
    {
        isLeaping = true;
        // sprite hook
        SetSpriteForState("jump", Vector2.zero);
        yield return new WaitForSeconds(cooldown);
        SetSpriteForState("idle", Vector2.zero);
        isLeaping = false;
    }

    IEnumerator LeapCoroutine(Vector2 start, Vector2 target, float leapDuration, float cooldown, Vector2 dirHint, float intendedDistance = -1f)
    {
        isLeaping = true;

        // publish event
        LeapInfo info = new LeapInfo { dir = dirHint, start = start, target = target, duration = leapDuration, cooldown = cooldown };
        onLeapStarted?.Invoke(info);

        // sprite
        SetSpriteForState("jump", dirHint);

        // scale duration to actual distance
        float actualDistance = Vector2.Distance(start, target);
        float effectiveDuration = leapDuration;

        if (intendedDistance > 0f)
        {
            // Match PlayerMovement scaling: fraction = actual / intended
            float frac = Mathf.Clamp01(actualDistance / intendedDistance);
            effectiveDuration = leapDuration * frac;
            effectiveDuration = Mathf.Max(effectiveDuration, 0.01f);
        }
        else
        {
            // Fallback: keep a reasonable minimum duration
            effectiveDuration = Mathf.Max(0.01f, leapDuration * Mathf.Clamp01(actualDistance / Mathf.Max(actualDistance, 1f)));
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
                SetSpriteForState("fall", dirHint);

            yield return null;
        }

        rb.MovePosition(target);
        SetSpriteForState("idle", dirHint);

        yield return new WaitForSeconds(cooldown);

        isLeaping = false;
    }

    // Hook for sprite state changes; replace or extend to integrate with your existing sprite logic or Animator
    void SetSpriteForState(string state, Vector2 dir)
    {
        if (spriteRenderer == null) return;

        bool useVertical = Mathf.Abs(dir.y) > Mathf.Abs(dir.x);

        Sprite chosen = null;

        if (useVertical)
        {
            if (dir.y > 0f)
            {
                if (state == "idle") { /* set backIdle */ }
                else if (state == "jump") { /* set backJump */ }
                else if (state == "fall") { /* set backFall */ }
            }
            else
            {
                if (state == "idle") { /* set frontIdle */ }
                else if (state == "jump") { /* set frontJump */ }
                else if (state == "fall") { /* set frontFall */ }
            }
        }
        else
        {
            if (state == "idle") { /* set sideIdle */ }
            else if (state == "jump") { /* set sideJump */ }
            else if (state == "fall") { /* set sideFall */ }

            if (Mathf.Abs(dir.x) > 0.1f)
                spriteRenderer.flipX = dir.x > 0f;
        }
    }
}
