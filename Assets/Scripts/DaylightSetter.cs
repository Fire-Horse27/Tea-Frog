using UnityEngine;
using System;

public class DaylightSetter : MonoBehaviour
{
    [Header("Refs")]
    public SpriteRenderer window;
    public Sprite[] skySprites;
    public GameTimer timer;

    [Header("Settings")]
    public float dayLength = 1f;            // must be > 0
    public float tolerance = 0.02f;         // tolerance for float comparison
    public float triggerCooldown = 0.5f;    // seconds to ignore repeated triggers

    private int currentIndex = 0;
    private float lastTriggerTime = -999f;
    private bool isShuttingDown = false;

    void Awake()
    {
        //Debug.Log($"{name} DaylightSetter Awake (id {GetInstanceID()})");
    }

    void Start()
    {
        // defensive checks early
        //if (window == null) Debug.LogWarning($"{name}: window SpriteRenderer is not assigned!");
        //if (skySprites == null || skySprites.Length == 0) Debug.LogWarning($"{name}: skySprites empty!");
        //if (timer == null) Debug.LogWarning($"{name}: timer is not assigned!");
        //if (dayLength <= 0f) Debug.LogWarning($"{name}: dayLength should be > 0");
    }

    void OnDestroy()
    {
        isShuttingDown = true;
        //Debug.LogWarning($"{name} DaylightSetter OnDestroy called. InstanceID={GetInstanceID()}\nStack:\n{Environment.StackTrace}");
        // optional: pause editor so you can inspect (uncomment while debugging in Editor)
        // #if UNITY_EDITOR
        // UnityEditor.EditorApplication.isPaused = true;
        // #endif
    }

    void Update()
    {
        // defensive quick exits
        if (isShuttingDown) return;
        if (timer == null) return;
        if (window == null) return;
        if (skySprites == null || skySprites.Length == 0) return;
        if (dayLength <= 0f) return;

        float t = timer.getTime();

        // only trigger when time > 0 and we are within tolerance of a multiple of dayLength
        float mod = t % dayLength;
        bool nearBoundary = (t > 0f && (Mathf.Abs(mod) < tolerance || Mathf.Abs(mod - dayLength) < tolerance));

        // cooldown prevents multiple updates across frames while still within tolerance
        if (nearBoundary && Time.time - lastTriggerTime > triggerCooldown)
        {
            lastTriggerTime = Time.time;

            // rotate index safely
            currentIndex = (currentIndex + 1) % skySprites.Length;

            // final null-check before assignment to avoid MissingReferenceException
            if (window != null && skySprites != null && skySprites.Length > 0)
            {
                //Debug.Log($"{name} Changing sprite to index {currentIndex} at game time {t:F2}");
                window.sprite = skySprites[currentIndex];
            }
        }
    }
}
