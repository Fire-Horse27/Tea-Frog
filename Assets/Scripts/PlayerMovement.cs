using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input (assign in inspector)")]
    public InputActionReference moveAction;

    [Header("Timing")]
    [Tooltip("How long you must hold (seconds) before subsequent leaps become large leaps.")]
    public float chargeTime = 0.20f;
    [Tooltip("Small grace window (seconds) to allow a near-simultaneous second keypress to form a diagonal.")]
    public float initialGraceWindow = 0.06f;

    [Header("MovementEngine (assign)")]
    public MovementEngine engine;

    const float inputThreshold = 0.2f;

    // Input state
    bool holdLoopRunning;
    Vector2 heldDir = Vector2.zero;
    float pressStartTime = 0f;

    // Sprites
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

    // runtime
    SpriteRenderer spriteRenderer;
    Coroutine spriteCoroutine;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (engine == null)
            engine = GetComponent<MovementEngine>();
    }

    void OnEnable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.performed += OnMovePerformed;
            moveAction.action.canceled += OnMoveCanceled;
            moveAction.action.Enable();
        }

        if (engine != null)
            engine.onLeapStarted += OnLeapStarted;
    }

    void OnDisable()
    {
        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.performed -= OnMovePerformed;
            moveAction.action.canceled -= OnMoveCanceled;
            moveAction.action.Disable();
        }

        if (engine != null)
            engine.onLeapStarted -= OnLeapStarted;

        if (spriteCoroutine != null)
        {
            StopCoroutine(spriteCoroutine);
            spriteCoroutine = null;
        }
    }

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (heldDir == Vector2.zero)
            pressStartTime = Time.time;

        Vector2 raw = ctx.ReadValue<Vector2>();
        heldDir = GetDirectionVector(raw, inputThreshold);

        if (!holdLoopRunning)
            StartCoroutine(InitialGraceThenHoldLoop());
    }

    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        heldDir = Vector2.zero;
    }

    IEnumerator InitialGraceThenHoldLoop()
    {
        holdLoopRunning = true;

        float tStart = Time.time;
        float tEnd = tStart + initialGraceWindow;

        Vector2 graceDir = heldDir;

        while (Time.time < tEnd)
        {
            if (moveAction != null && moveAction.action != null)
            {
                Vector2 sample = moveAction.action.ReadValue<Vector2>();
                Vector2 d = GetDirectionVector(sample, inputThreshold);
                if (d != Vector2.zero)
                    graceDir = d;
            }

            if (heldDir == Vector2.zero && Time.time - tStart > 0f)
                break;

            yield return null;
        }

        if (graceDir == Vector2.zero)
        {
            holdLoopRunning = false;
            yield break;
        }

        // issue first (small) leap via engine
        IssueLeap(graceDir, useLarge: false);

        if (heldDir == Vector2.zero)
        {
            while (engine != null && engine.isLeaping)
                yield return null;

            holdLoopRunning = false;
            yield break;
        }

        while (heldDir != Vector2.zero)
        {
            if (engine != null && engine.isLeaping)
            {
                yield return null;
                continue;
            }

            float heldTime = Time.time - pressStartTime;
            bool charged = heldTime >= chargeTime;
            bool large = charged;

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

            IssueLeap(dir, large);
        }

        holdLoopRunning = false;
    }

    void IssueLeap(Vector2 dir, bool useLarge)
    {
        if (engine == null) return;
        engine.StartLeapInDirection(dir, useLarge);
    }

    void OnLeapStarted(MovementEngine.LeapInfo info)
    {
        if (spriteCoroutine != null)
        {
            StopCoroutine(spriteCoroutine);
            spriteCoroutine = null;
        }

        spriteCoroutine = StartCoroutine(HandleLeapSprite(info));
    }

    IEnumerator HandleLeapSprite(MovementEngine.LeapInfo info)
    {
        SetSpriteForState("jump", info.dir);

        float duration = info.duration;
        float cooldown = info.cooldown;

        if (duration <= 0f)
        {
            yield return new WaitForSeconds(cooldown);
            SetSpriteForState("idle", info.dir);
            spriteCoroutine = null;
            yield break;
        }

        float half = duration * 0.5f;
        yield return new WaitForSeconds(half);

        SetSpriteForState("fall", info.dir);

        yield return new WaitForSeconds(duration - half);

        SetSpriteForState("idle", info.dir);

        yield return new WaitForSeconds(cooldown);

        spriteCoroutine = null;
    }

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
