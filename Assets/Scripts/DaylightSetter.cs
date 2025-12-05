using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class DaylightSetter : MonoBehaviour
{
    // ----- Parent / manager fields (use on the parent object) -----
    [Header("Parent: assign on the parent object")]
    public SpriteRenderer[] windows;   // the list you showed in the screenshot
    public Sprite[] daySky;
    public Sprite[] sunsetSky;
    public Sprite[] nightSky;
    public Sprite clearDaySprite;
    public Sprite clearSunsetSprite;
    public Sprite clearNightSprite;
    public GameTimer timer; // must provide getTime() and getMaxTime()

    [Header("Animation / selection settings (parent)")]
    public float cloudSpeed = 4f;           // frames/sec
    public float referenceY = 0f;           // cutoff for top (0) vs bottom (1)

    // ----- Per-window override fields (use on window objects) -----
    [Header("Per-window overrides (put these on each window)")]
    [Tooltip("-1 = let manager decide 50/50, 0 = force static, 1 = force animated")]
    public int animateOverride = -1;

    [Tooltip("Manual row index override. -1 = auto (compare y to referenceY)")]
    public int rowIndexOverride = -1;

    [Tooltip("Integer index offset so different windows pick different frames")]
    public int manualIndexOffset = 0;

    [Tooltip("Fractional seconds offset to stagger animation")]
    public float animationOffsetSeconds = 0f;

    // ----- Internal (parent-only) -----
    private bool isManager = false;
    private float startTime;
    private bool[] chosenAnimated;    // which windows the manager decided are animated (by index)
    private int[] resolvedRowIndex;   // per-window resolved row index
    private System.Random rng;        // used when selecting exact half randomly

    // cached forced sets (populated during InitializeManager)
    private HashSet<int> forcedAnimated = new HashSet<int>();
    private HashSet<int> forcedStatic = new HashSet<int>();

    // sunset-specific allowed indices (hard-coded per your request)
    private readonly int[] sunsetAllowedIndices = new int[] { 0, 1, 4, 5 };
    private const int sunsetCloudCount = 2;

    void Awake()
    {
        // Decide whether this instance is the manager: if it has a non-empty windows array we treat it as the parent/manager.
        isManager = (windows != null && windows.Length > 0);

        // Start time for animation base
        startTime = Time.time;

        // If manager, initialize selection and row arrays
        if (isManager)
        {
            InitializeManager();
        }
        else
        {
            // nothing to do here for window instances; manager will read per-window fields from the component on each window
        }
    }

    void InitializeManager()
    {
        int n = windows != null ? windows.Length : 0;
        chosenAnimated = new bool[n];
        resolvedRowIndex = new int[n];

        forcedAnimated.Clear();
        forcedStatic.Clear();

        // resolve forced settings and row indices
        for (int i = 0; i < n; i++)
        {
            var sr = windows[i];
            if (sr == null)
            {
                resolvedRowIndex[i] = 0;
                continue;
            }

            var child = sr.GetComponent<DaylightSetter>();

            // row
            if (child != null && child.rowIndexOverride >= 0)
                resolvedRowIndex[i] = child.rowIndexOverride;
            else
                resolvedRowIndex[i] = (sr.transform.position.y > referenceY ? 0 : 1);

            // forced
            if (child != null)
            {
                if (child.animateOverride == 1) forcedAnimated.Add(i);
                else if (child.animateOverride == 0) forcedStatic.Add(i);
            }
        }

        // ---------------------------------------------
        //  SIMPLE GROUP SELECTION (NO HASHING)
        // ---------------------------------------------

        // group 1: indices 0..3
        PickTwoInGroup(0, 4);

        // group 2: indices 4..7
        PickTwoInGroup(4, 4);

        // local helper — chooses 2 non-forced windows and ensures unique offset
        void PickTwoInGroup(int start, int length)
        {
            int end = Mathf.Min(start + length, n);

            // List of candidates
            List<int> candidates = new List<int>();

            // forced animated go first
            foreach (int i in forcedAnimated)
            {
                if (i >= start && i < end)
                    candidates.Add(i);
            }

            // then fill with other available indices
            for (int i = start; i < end; i++)
            {
                if (forcedAnimated.Contains(i) || forcedStatic.Contains(i))
                    continue;

                if (!candidates.Contains(i))
                    candidates.Add(i);
            }

            // select exactly 2 if possible
            if (candidates.Count > 0)
            {
                chosenAnimated[candidates[0]] = true;
            }
            if (candidates.Count > 1)
            {
                chosenAnimated[candidates[1]] = true;
            }

            // ensure they don't start on the same cloud:
            // give distinct manualIndexOffset values
            if (candidates.Count > 1)
            {
                var c0 = windows[candidates[0]].GetComponent<DaylightSetter>();
                var c1 = windows[candidates[1]].GetComponent<DaylightSetter>();

                if (c0 != null) c0.manualIndexOffset = 0;
                if (c1 != null) c1.manualIndexOffset = 1;
            }
        }
    }

    void Update()
    {
        if (!isManager)
        {
            // window instances do nothing on Update; parent manages sprites
            return;
        }

        if (windows == null || windows.Length == 0) return;

        // calculate percent through timer (0 start, 1 end)
        float percent = 0f;
        if (timer != null)
        {
            float t = timer.getTime();
            float max = Mathf.Max(0.0001f, timer.startTime);
            percent = Mathf.Clamp01(1f - (t / max));
        }

        Sprite[] sky = GetSkyForPercent(percent);

        // base animation index shared among windows
        float timeBaseF = (Time.time - startTime) * cloudSpeed;
        int baseIndex = Mathf.FloorToInt(timeBaseF);

        bool[] sunsetChosen = null;
        bool inSunset = (percent >= 0.33f && percent < 0.66f);
        if (inSunset)
        {
            int n = windows.Length;
            sunsetChosen = new bool[n];

            // Always make indices 0,1,4,5 cloudy when they exist.
            // If you want to ignore missing windows, we simply check bounds.
            int[] forcedSet = new int[] { 0, 1, 4, 5 };
            for (int k = 0; k < forcedSet.Length; k++)
            {
                int idx = forcedSet[k];
                if (idx >= 0 && idx < n && windows[idx] != null)
                {
                    sunsetChosen[idx] = true;
                }
            }

            for (int i = 0; i < windows.Length; i++)
            {
                var wr = windows[i];
                if (wr == null) continue;

                // read per-window overrides from the DaylightSetter component on the window (if present)
                DaylightSetter per = wr.GetComponent<DaylightSetter>();
                int perAnimateOverride = per != null ? per.animateOverride : -1;
                int perRowOverride = per != null ? per.rowIndexOverride : -1;
                int perManualIndex = per != null ? per.manualIndexOffset : 0;
                float perAnimOffset = per != null ? per.animationOffsetSeconds : 0f;

                // If we're in sunset phase, use sunsetChosen when deciding which windows are animated.
                // Otherwise fall back to chosenAnimated.
                bool baseChoice = chosenAnimated != null && i < chosenAnimated.Length ? chosenAnimated[i] : false;
                bool animate;
                if (perAnimateOverride == 0) animate = false;                 // forced static always honored
                else if (perAnimateOverride == 1)
                {
                    // forced animated honored only if NOT in sunset or if in sunset and the window is one of the sunset-chosen ones.
                    if (!inSunset) animate = true;
                    else animate = (sunsetChosen != null && i < sunsetChosen.Length && sunsetChosen[i]);
                }
                else
                {
                    // no per-window force: use sunset selection if in sunset, else normal chosenAnimated
                    animate = inSunset ? (sunsetChosen != null && i < sunsetChosen.Length && sunsetChosen[i]) : baseChoice;
                }

                // row index: prefer per-window override, else resolvedRowIndex (computed earlier)
                int row = (perRowOverride >= 0) ? perRowOverride : (resolvedRowIndex != null && i < resolvedRowIndex.Length ? resolvedRowIndex[i] : 0);

                if (animate)
                {
                    if (sky == null || sky.Length == 0) continue;

                    // compute index similar to original: baseIndex + i + row + manual offset + fractional offset effect
                    float timeBaseWithOffset = (Time.time - startTime + perAnimOffset) * cloudSpeed;
                    int idx = Mathf.FloorToInt(timeBaseWithOffset) + i + row + perManualIndex;

                    idx %= sky.Length;
                    if (idx < 0) idx += sky.Length;
                    wr.sprite = sky[idx];
                }
                else
                {
                    // static window: prefer clear sprite for the phase
                    Sprite clear = GetClearSpriteForPercent(percent, row);
                    if (clear != null) wr.sprite = clear;
                    else if (sky != null && sky.Length > 0)
                    {
                        int idx = (row + perManualIndex) % sky.Length;
                        if (idx < 0) idx += sky.Length;
                        wr.sprite = sky[idx];
                    }
                }
            }
        }

        // ----- helpers -----
        Sprite[] GetSkyForPercent(float p)
        {
            if (p < 0.33f) return daySky;
            if (p < 0.66f) return sunsetSky;
            return nightSky;
        }

        Sprite GetClearSpriteForPercent(float p, int row)
        {
            if (p < 0.33f)
            {
                if (clearDaySprite != null) return clearDaySprite;
                if (daySky != null && daySky.Length > 0) return daySky[row % daySky.Length];
            }
            else if (p < 0.66f)
            {
                if (clearSunsetSprite != null) return clearSunsetSprite;
                if (sunsetSky != null && sunsetSky.Length > 0) return sunsetSky[row % sunsetSky.Length];
            }
            else
            {
                if (clearNightSprite != null) return clearNightSprite;
                if (nightSky != null && nightSky.Length > 0) return nightSky[row % nightSky.Length];
            }
            return null;
        }
    }
}