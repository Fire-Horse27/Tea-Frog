using UnityEngine;

public class DaylightSetter : MonoBehaviour
{
    [Header("Windows (drag in order: animated first, then clear-sky)")]
    public SpriteRenderer[] windows;   // windows[0..2] animate, windows[3..7] clear sky

    [Header("Sky sprite sets")]
    public Sprite[] daySky;
    public Sprite[] sunsetSky;
    public Sprite[] nightSky;

    [Header("Optional explicit clear-sky sprites")]
    public Sprite clearDaySprite;
    public Sprite clearSunsetSprite;
    public Sprite clearNightSprite;

    public GameTimer timer;

    [Header("Settings")]
    public float cloudSpeed = 1f;
    public float dayLength = 1f;
    public float triggerCooldown = 0.5f;

    private enum Phase { Day, Sunset, Night }
    private Phase phase = Phase.Day;

    private Sprite[] currentSprites;
    private float cloudOffset = 0f;
    private float lastTriggerTime = -999f;
    private bool isShuttingDown = false;

    private const int animatedWindowCount = 3;
    private int lastPeriod = 0; // <-- force start in DAY

    void Start()
    {
        // Always begin in Day
        phase = Phase.Day;
        lastPeriod = Mathf.FloorToInt(timer.startTime / dayLength);

        // Choose Day sprites
        if (daySky != null && daySky.Length > 0) currentSprites = daySky;
        else if (sunsetSky != null && sunsetSky.Length > 0) currentSprites = sunsetSky;
        else currentSprites = nightSky;
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
        int currentPeriod = Mathf.FloorToInt(t / dayLength);

        // Advance only when entering new period
        if (currentPeriod > lastPeriod && Time.time - lastTriggerTime > triggerCooldown)
        {
            Debug.Log("Entered a new period. Time: " + timer.getTime());
            lastTriggerTime = Time.time;
            lastPeriod = currentPeriod;
            AdvancePhase();
        }

        // Animate first 3 windows
        cloudOffset += cloudSpeed * Time.deltaTime;
        int baseIndex = Mathf.FloorToInt(cloudOffset);

        for (int i = 0; i < windows.Length; i++)
        {
            SpriteRenderer wr = windows[i];
            if (wr == null) continue;

            if (i < animatedWindowCount)
            {
                // animated window
                if (currentSprites != null && currentSprites.Length > 0)
                {
                    int idx = (baseIndex + i) % currentSprites.Length;
                    if (idx < 0) idx += currentSprites.Length;
                    wr.sprite = currentSprites[idx];
                }
            }
            else
            {
                // clear-sky windows
                wr.sprite = GetClearSpriteForPhase();
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

        // Optional: reset cloud animation per phase
        // cloudOffset = 0f;
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
