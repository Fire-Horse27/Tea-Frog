using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerMovement
/// - Single-slot queued direction during leap & cooldown with overwrite rules:
///   1) Release-all then press => new direction
///   2) Holding a direction + press compatible direction => pick diagonal
///   3) Incompatible direction pressed => most recent press wins
/// - Jumps are LONG by default unless the press duration is a SHORT TAP (< shortTapThreshold).
/// - Player may choose the next leap direction while the previous leap is still happening (engine.isLeaping)
///   and presses during the active leap are accepted and queued.
/// - Fix: when a player releases a key that contributed to the current movement, that key's axis
///   is removed from queued direction so it no longer affects the next leap (prevents double-diagonals).
/// 
/// Wiring assumptions:
/// - MovementEngine exposes: bool isLeaping and StartLeapInDirection(Vector2 dir, bool large)
/// - MovementEngine invokes onLeapStarted with MovementEngine.LeapInfo (containing duration, cooldown, and optionally dir)
/// Adapt method names if your engine API differs.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input (assign in inspector)")]
    public InputActionReference moveAction;

    [Header("Timing")]
    [Tooltip("How long you must hold (seconds) before subsequent leaps become large leaps (not used as the default rule here).")]
    public float chargeTime = 0.20f;
    [Tooltip("If a press duration is shorter than this, treat it as a short tap -> small hop. Long by default otherwise.")]
    public float shortTapThreshold = 0.12f;
    [Tooltip("Small grace window (seconds) to allow a near-simultaneous second keypress to form a diagonal.")]
    public float initialGraceWindow = 0.06f;

    [Header("MovementEngine (assign)")]
    public MovementEngine engine;

    const float inputThreshold = 0.2f;

    // ---------- Runtime input state ----------
    bool holdLoopRunning = false;        // whether the hold/leap loop coroutine is running
    Vector2 heldDir = Vector2.zero;      // what is currently held right now (live input)
    float pressStartTime = 0f;           // when the current heldDir was first pressed (for short-tap detection)

    // queued input (single-slot) captured during engine.isLeaping OR during engine cooldown
    Vector2 queuedDir = Vector2.zero;
    float queuedPressStartTime = 0f;

    // last leap metadata
    Vector2 lastLeapDir = Vector2.zero;  // direction used by most recent leap
    float leapCooldownEndTime = 0f;      // time when current leap's duration+cooldown finishes

    // held-through-leap semantics:
    // true when the input was held continuously across the leap start (used to decide "release during cooldown stops")
    bool heldThroughLeap = false;

    // whether any input is currently down (helps detect "release then new press" vs "still holding")
    bool inputHeld = false;
    bool prevInputHeld = false; // previous held state captured at OnMovePerformed entry

    // track the last raw vector read from the action so we can determine which axis/key was released
    Vector2 lastRawInput = Vector2.zero;

    // ---------- Sprite fields ----------
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
        GameEngine.OnDayStarted += HandleDayStarted;
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
        GameEngine.OnDayStarted -= HandleDayStarted;
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

    private void HandleDayStarted(int dayIndex)
    {
        // Stop any running sprite coroutine and input loops
        if (spriteCoroutine != null)
        {
            StopCoroutine(spriteCoroutine);
            spriteCoroutine = null;
        }

        holdLoopRunning = false;
        heldDir = Vector2.zero;
        queuedDir = Vector2.zero;
        queuedPressStartTime = 0f;
        pressStartTime = 0f;
        inputHeld = false;
        prevInputHeld = false;
        lastRawInput = Vector2.zero;
        lastLeapDir = Vector2.zero;
        leapCooldownEndTime = 0f;
        heldThroughLeap = false;

        // Optionally set idle sprite
        SetSpriteForState("idle", Vector2.zero);
    }

    // ---------------- Input handlers ----------------

    // Called on performed (first frame of press and repeats for held input depending on action type)
    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        // capture previous held state to detect "fresh press" vs "already holding"
        prevInputHeld = inputHeld;

        // capture prevHeldDir BEFORE we update heldDir so the queue/diagonal logic can inspect the prior hold
        Vector2 prevHeldDir = heldDir;

        Vector2 raw = ctx.ReadValue<Vector2>();
        Vector2 newDir = GetDirectionVector(raw, inputThreshold);

        // Save raw input for later release-detection comparisons
        lastRawInput = raw;

        // update "inputHeld" and heldDir/pressStartTime
        if (newDir != Vector2.zero)
        {
            // mark that something is held now
            inputHeld = true;

            // if we were not already holding, this is a fresh press -> set pressStartTime and heldDir
            if (!prevInputHeld)
            {
                pressStartTime = Time.time;
                heldDir = newDir;
            }
            else
            {
                // still holding: don't overwrite heldDir yet for queue-decision logic.
                // We'll delay updating heldDir to after the queue handler so the handler sees prevHeldDir.
                // (This prevents the race that prevented diagonals from being recognized.)
            }
        }
        else
        {
            // performed with zero — ignore
            return;
        }

        // Accept presses that occur while the engine is currently leaping OR during cooldown.
        // This lets the player choose the next leap while the previous one is still active.
        bool acceptingQueueNow = (engine != null && engine.isLeaping) || (Time.time < leapCooldownEndTime);

        if (acceptingQueueNow)
        {
            // pass prevHeldDir so handler can check previous hold + newDir for diagonal formation
            HandlePressDuringLeapOrCooldown(newDir, prevInputHeld, prevHeldDir);

            // now update heldDir to the live input for subsequent frames (we delayed this above)
            heldDir = newDir;

            return;
        }

        // Not in leap/cooldown -> normal immediate behavior: ensure hold loop runs
        // update heldDir if we previously deferred it
        if (prevInputHeld)
            heldDir = newDir;

        if (!holdLoopRunning)
            StartCoroutine(InitialGraceThenHoldLoop());
    }

    // Called on canceled (release)
    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        // A release happened — we'll detect which axis(es) were released and remove their influence.
        prevInputHeld = inputHeld;

        // read current raw input after the cancel — this reflects remaining pressed keys (if any)
        Vector2 remainingRaw = Vector2.zero;
        if (moveAction != null && moveAction.action != null)
            remainingRaw = moveAction.action.ReadValue<Vector2>();

        // Determine which axis components were released by comparing lastRawInput (before) vs remainingRaw (after)
        bool releasedPosX = (lastRawInput.x > inputThreshold) && !(remainingRaw.x > inputThreshold);
        bool releasedNegX = (lastRawInput.x < -inputThreshold) && !(remainingRaw.x < -inputThreshold);
        bool releasedPosY = (lastRawInput.y > inputThreshold) && !(remainingRaw.y > inputThreshold);
        bool releasedNegY = (lastRawInput.y < -inputThreshold) && !(remainingRaw.y < -inputThreshold);

        // Update stored raw input
        lastRawInput = remainingRaw;

        // Update high-level held flags
        inputHeld = remainingRaw.sqrMagnitude >= (inputThreshold * inputThreshold);
        // heldDir becomes remaining live direction (normalized)
        heldDir = GetDirectionVector(remainingRaw, inputThreshold);

        // Reset pressStartTime on release -> this makes repeated short presses produce short jumps
        // because a fresh press after release gets a new pressStartTime.
        if (!inputHeld)
            pressStartTime = 0f;

        // If any axis that contributed to queuedDir or lastLeapDir was released while the leap was active,
        // remove its influence from queuedDir so it won't cause a future hop in that axis.
        // This is the key fix: a mid-leap release removes that axis from queued direction immediately.
        if (releasedPosX || releasedNegX || releasedPosY || releasedNegY)
        {
            Vector2 releasedMask = Vector2.zero;
            if (releasedPosX) releasedMask.x = 1f;
            if (releasedNegX) releasedMask.x = -1f;
            if (releasedPosY) releasedMask.y = 1f;
            if (releasedNegY) releasedMask.y = -1f;

            RemoveReleasedAxesFromQueued(releasedMask);

            // Also, if the player held-through the leap and then released during cooldown, previously we cleared queued same-direction.
            // Keep that behavior: if queued equals lastLeapDir and heldThroughLeap and release during cooldown => drop queued entirely.
            if (queuedDir != Vector2.zero && ApproximatelyEqual(queuedDir, lastLeapDir))
            {
                if (heldThroughLeap && Time.time < leapCooldownEndTime)
                {
                    queuedDir = Vector2.zero;
                    queuedPressStartTime = 0f;
                }
            }
        }
    }

    // Remove the axis/components represented in releasedMask from queuedDir.
    // releasedMask uses sign to indicate which direction on that axis was released:
    // x =  1 => positive-x released (right)
    // x = -1 => negative-x released (left)
    // y =  1 => positive-y released (up)
    // y = -1 => negative-y released (down)
    void RemoveReleasedAxesFromQueued(Vector2 releasedMask)
    {
        if (queuedDir == Vector2.zero) return;

        Vector2 newQueued = queuedDir;

        // X axis
        if (releasedMask.x > 0f && queuedDir.x > 0f) newQueued.x = 0f;   // released right, queued had right
        if (releasedMask.x < 0f && queuedDir.x < 0f) newQueued.x = 0f;   // released left, queued had left

        // Y axis
        if (releasedMask.y > 0f && queuedDir.y > 0f) newQueued.y = 0f;   // released up, queued had up
        if (releasedMask.y < 0f && queuedDir.y < 0f) newQueued.y = 0f;   // released down, queued had down

        // If the new queued vector collapses to zero, clear it
        if (newQueued.sqrMagnitude < 0.0001f)
        {
            queuedDir = Vector2.zero;
            queuedPressStartTime = 0f;
            return;
        }

        // Otherwise normalize the remaining components so engine receives unit direction
        queuedDir = newQueued.normalized;
    }

    // ---------------- Leap & cooldown queue handling ----------------
    // This unified helper applies the overwrite rules regardless of whether the press happened during the leap
    // or during the cooldown period. It lets the player choose the next leap while the previous is active.
    //
    // Parameters:
    // - newDir: the freshly pressed direction
    // - wasHoldingBeforePress: whether any input was held immediately before this press (prevInputHeld)
    // - prevHeldDir: the direction that was held immediately before this press (important for diagonal checks)
    void HandlePressDuringLeapOrCooldown(Vector2 newDir, bool wasHoldingBeforePress, Vector2 prevHeldDir)
    {
        if (newDir == Vector2.zero) return;

        // Detect "fresh press" = player had released all keys before this press.
        bool wasReleasedBeforePress = !wasHoldingBeforePress;

        // Rule 1: fresh press -> overwrites queued
        if (wasReleasedBeforePress)
        {
            queuedDir = newDir;
            queuedPressStartTime = Time.time;
            return;
        }

        // Player was holding something and pressed another direction.

        // Rule 2: if the previously held direction and newDir can form a diagonal, pick the diagonal.
        // Use prevHeldDir here (not the updated heldDir) to avoid races where heldDir was overwritten first.
        if (CanFormDiagonal(prevHeldDir, newDir))
        {
            queuedDir = CombineDirectionsForDiagonal(prevHeldDir, newDir);
            queuedPressStartTime = Time.time;
            return;
        }

        // Rule 3: incompatible (opposite or non-diagonal) -> most recent press wins
        queuedDir = newDir;
        queuedPressStartTime = Time.time;
    }

    // ---------------- Hold / Leap loop ----------------
    IEnumerator InitialGraceThenHoldLoop()
    {
        holdLoopRunning = true;

        float tStart = Time.time;
        float tEnd = tStart + initialGraceWindow;

        // sample initial input; prefer live sampling for diagonal formation during the short grace window
        Vector2 sampledDir = heldDir;

        while (Time.time < tEnd)
        {
            if (moveAction != null && moveAction.action != null)
            {
                Vector2 sample = moveAction.action.ReadValue<Vector2>();
                Vector2 d = GetDirectionVector(sample, inputThreshold);
                if (d != Vector2.zero)
                    sampledDir = d;
            }

            // If no held input and a tiny bit of time passed, give up
            if (heldDir == Vector2.zero && Time.time - tStart > 0f)
                break;

            yield return null;
        }

        // If we have no direction to act on, stop
        if (sampledDir == Vector2.zero)
        {
            holdLoopRunning = false;
            yield break;
        }

        // Decide if the initial leap should be large:
        // Jumps are LONG by default unless the press duration is a SHORT TAP (< shortTapThreshold).
        float heldTimeAtIssue = pressStartTime > 0f ? Time.time - pressStartTime : 0f;
        bool initialLarge = (heldTimeAtIssue >= shortTapThreshold) || IsHoldingDirectionNow(sampledDir);

        // If a queuedDir was set earlier (including while the previous leap was active), prefer it once cooldown expired.
        // Note: queuedDir can be set during the previous leap because HandlePressDuringLeapOrCooldown accepts inputs while engine.isLeaping.
        if (queuedDir != Vector2.zero && Time.time >= leapCooldownEndTime)
        {
            sampledDir = queuedDir;
            // adopt queued press time if available (0 means unknown)
            pressStartTime = queuedPressStartTime != 0f ? queuedPressStartTime : pressStartTime;
            queuedDir = Vector2.zero;
            queuedPressStartTime = 0f;

            // recompute large condition for queued input
            heldTimeAtIssue = pressStartTime > 0f ? Time.time - pressStartTime : 0f;
            initialLarge = (heldTimeAtIssue >= shortTapThreshold) || IsHoldingDirectionNow(sampledDir);
        }

        // Issue first leap
        IssueLeap(sampledDir, initialLarge);

        // If we immediately stopped holding after issuing, wait for engine and exit
        if (heldDir == Vector2.zero)
        {
            while (engine != null && engine.isLeaping)
                yield return null;

            holdLoopRunning = false;
            yield break;
        }

        // Main loop: while player holds an input, continue issuing leaps when engine is ready
        while (heldDir != Vector2.zero)
        {
            // Wait until engine finishes current leap
            while (engine != null && engine.isLeaping)
                yield return null;

            // First check queuedDir: if present and cooldown finished, consume it
            if (queuedDir != Vector2.zero && Time.time >= leapCooldownEndTime)
            {
                Vector2 dirToUse = queuedDir;
                pressStartTime = queuedPressStartTime != 0f ? queuedPressStartTime : Time.time;
                queuedDir = Vector2.zero;
                queuedPressStartTime = 0f;

                // Large if not a short tap OR if the player is holding the direction now
                float heldDuration = pressStartTime > 0f ? Time.time - pressStartTime : 0f;
                bool large = (heldDuration >= shortTapThreshold) || IsHoldingDirectionNow(dirToUse);

                // Clear heldThroughLeap because we're past the decision point
                heldThroughLeap = false;

                IssueLeap(dirToUse, large);
                continue;
            }

            // Otherwise sample current live input (this allows forming diagonals while holding)
            Vector2 currentInput = (moveAction != null && moveAction.action != null) ? GetDirectionVector(moveAction.action.ReadValue<Vector2>(), inputThreshold) : heldDir;
            Vector2 dir = currentInput;
            if (dir == Vector2.zero)
            {
                // no current live input -> stop repeating unless queuedDir becomes available (handled above)
                break;
            }
            else
            {
                // update heldDir with the live input
                heldDir = dir;
            }

            // compute whether large: not a short tap OR currently held
            float heldDurationNow = pressStartTime > 0f ? Time.time - pressStartTime : 0f;
            bool largeNow = (heldDurationNow >= shortTapThreshold) || IsHoldingDirectionNow(dir);

            IssueLeap(dir, largeNow);

            // yield a frame to allow cancellations/presses to be processed
            yield return null;
        }

        holdLoopRunning = false;
    }

    // ---------- IssueLeap wrapper ----------
    // Sets up lastLeapDir and heldThroughLeap immediately, then calls the engine to start a leap.
    void IssueLeap(Vector2 dir, bool useLarge)
    {
        if (engine == null)
        {
            Debug.LogWarning("No MovementEngine assigned to PlayerMovement.");
            return;
        }

        // record direction for this leap (before engine starts it)
        lastLeapDir = dir;

        // heldThroughLeap: true when input was held continuously across the leap start and matched the leap direction
        heldThroughLeap = inputHeld && ApproximatelyEqual(heldDir, lastLeapDir);

        // tell the engine to start the leap – adapt this call if your engine API differs
        engine.StartLeapInDirection(dir, useLarge);
    }

    // Engine callback: when the engine announces a leap started it provides LeapInfo (duration, cooldown).
    // We use that to set the cooldown timer and also to start sprite handling.
    void OnLeapStarted(MovementEngine.LeapInfo info)
    {
        // set leap cooldown end time based on engine-provided values (duration + cooldown)
        leapCooldownEndTime = Time.time + info.duration + info.cooldown;

        // sprite handling
        if (spriteCoroutine != null)
        {
            StopCoroutine(spriteCoroutine);
            spriteCoroutine = null;
        }

        spriteCoroutine = StartCoroutine(HandleLeapSprite(info));
    }

    // ---------- Sprite coroutine ----------
    IEnumerator HandleLeapSprite(MovementEngine.LeapInfo info)
    {
        // If your LeapInfo doesn't include dir, you can use lastLeapDir for sprite orientation.
        Vector2 dirForSprite = (info.dir != Vector2.zero) ? info.dir : lastLeapDir;
        SetSpriteForState("jump", dirForSprite);

        float duration = info.duration;
        float cooldown = info.cooldown;

        if (duration <= 0f)
        {
            yield return new WaitForSeconds(cooldown);
            SetSpriteForState("idle", dirForSprite);
            spriteCoroutine = null;
            yield break;
        }

        float half = duration * 0.5f;
        yield return new WaitForSeconds(half);

        SetSpriteForState("fall", dirForSprite);

        yield return new WaitForSeconds(duration - half);

        SetSpriteForState("idle", dirForSprite);

        yield return new WaitForSeconds(cooldown);

        spriteCoroutine = null;
    }

    // ---------------- Helpers ----------------

    // Convert raw input vector (stick / keyboard axis) into a normalized direction or zero if below threshold
    static Vector2 GetDirectionVector(Vector2 inVec, float threshold)
    {
        if (inVec.sqrMagnitude < (threshold * threshold)) return Vector2.zero;
        return inVec.normalized;
    }

    // Compare vectors approximately (to avoid float noise)
    bool ApproximatelyEqual(Vector2 a, Vector2 b)
    {
        return Vector2.SqrMagnitude(a - b) < 0.01f;
    }

    // Determine if two unit directions can form a diagonal (not opposite, not colinear)
    bool CanFormDiagonal(Vector2 a, Vector2 b)
    {
        if (a == Vector2.zero || b == Vector2.zero) return false;
        if (ApproximatelyEqual(a, -b)) return false; // exact opposite not compatible

        // Cross product non-zero => not colinear
        float cross = a.x * b.y - a.y * b.x;
        return Mathf.Abs(cross) > 0.001f;
    }

    // Combine two compatible directions into a normalized diagonal vector
    Vector2 CombineDirectionsForDiagonal(Vector2 a, Vector2 b)
    {
        Vector2 combined = a + b;
        if (combined == Vector2.zero) return Vector2.zero;
        return combined.normalized;
    }

    // Checks whether the given dir is still currently being held by live input
    bool IsHoldingDirectionNow(Vector2 dir)
    {
        if (moveAction == null || moveAction.action == null) return false;
        Vector2 current = GetDirectionVector(moveAction.action.ReadValue<Vector2>(), inputThreshold);
        return ApproximatelyEqual(current, dir);
    }

    // Sprite selection (kept from your original file)
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
