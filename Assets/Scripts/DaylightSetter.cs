using UnityEngine;

public class DaylightSetter : MonoBehaviour
{
    [Header("Windows (drag in order: animated first, then clear-sky)")]
    public SpriteRenderer[] windows;   // windows[0..3] animate, windows[4..] clear sky

    [Header("Sky sprite sets (for animated windows)")]
    public Sprite[] daySky;
    public Sprite[] sunsetSky;
    public Sprite[] nightSky;

    [Header("Optional clear-sky sprites")]
    public Sprite clearDaySprite;
    public Sprite clearSunsetSprite;
    public Sprite clearNightSprite;

    public GameTimer timer;

    [Header("Settings")]
    public float cloudSpeed = 4f;      // how fast the animation cycles (frames per second)
    public float dayLength = 10f;      // how long each phase lasts (same units as timer.getTime())
    public float triggerCooldown = 0.5f; // seconds between allowed phase triggers
    public float triggerEpsilon = 0.05f; // tolerance for "divisible" check

    private enum Phase { Day, Sunset, Night }
    private Phase phase = Phase.Day;

    private Sprite[] currentSprites;
    private float cloudOffset = 0f;
    private float lastTriggerTime = -999f;
    private bool isShuttingDown = false;

    private const int animatedWindowCount = 4; // windows 0,1,2,3 animate

    void Start()
    {
        phase = Phase.Day;
        currentSprites = (daySky != null && daySky.Length > 0) ? daySky : null;
        lastTriggerTime = -999f;
        cloudOffset = 0f;

        if (windows == null || windows.Length < animatedWindowCount)
            Debug.LogWarning("DaylightSetter: windows array should contain at least " + animatedWindowCount + " entries (animated windows 0..3).");
    }

    void OnDestroy()
    {
        isShuttingDown = true;
    }

    void Update()
    {
        if (isShuttingDown) return;
        if (timer == null) return;
        if (windows == null || windows.Length == 0) return;
        if (dayLength <= 0f) return;

        float t = timer.getTime();
        float mod = t % dayLength;

        if (t >= dayLength && mod <= triggerEpsilon && (Time.time - lastTriggerTime) > triggerCooldown)
        {
            lastTriggerTime = Time.time;
            AdvancePhase();
        }

        cloudOffset += cloudSpeed * Time.deltaTime;
        int baseIndex = Mathf.FloorToInt(cloudOffset);

        for (int i = 0; i < windows.Length; i++)
        {
            SpriteRenderer wr = windows[i];
            if (wr == null) continue;

            if (i < animatedWindowCount)
            {
                if (currentSprites != null && currentSprites.Length > 0)
                {
                    int idx = (baseIndex + i) % currentSprites.Length;
                    if (idx < 0) idx += currentSprites.Length;
                    wr.sprite = currentSprites[idx];
                }
            }
            else
            {
                Sprite clear = GetClearSpriteForPhase();
                if (clear != null) wr.sprite = clear;
            }
        }
    }

    void AdvancePhase()
    {
        switch (phase)
        {
            case Phase.Day:
                phase = Phase.Sunset;
                currentSprites = (sunsetSky != null && sunsetSky.Length > 0) ? sunsetSky : currentSprites;
                break;
            case Phase.Sunset:
                phase = Phase.Night;
                currentSprites = (nightSky != null && nightSky.Length > 0) ? nightSky : currentSprites;
                break;
            case Phase.Night:
                phase = Phase.Day;
                currentSprites = (daySky != null && daySky.Length > 0) ? daySky : currentSprites;
                break;
        }

        cloudOffset = 0f;
        Debug.Log($"DaylightSetter: Phase changed to {phase} at timer {timer.getTime():F2}");
    }

    Sprite GetClearSpriteForPhase()
    {
        switch (phase)
        {
            case Phase.Day:
                if (clearDaySprite != null) return clearDaySprite;
                if (daySky != null && daySky.Length > 0) return daySky[0];
                break;
            case Phase.Sunset:
                if (clearSunsetSprite != null) return clearSunsetSprite;
                if (sunsetSky != null && sunsetSky.Length > 0) return sunsetSky[0];
                break;
            case Phase.Night:
                if (clearNightSprite != null) return clearNightSprite;
                if (nightSky != null && nightSky.Length > 0) return nightSky[0];
                break;
        }
        return null;
    }
}
