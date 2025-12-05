using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Header("Prefab & Pool")]
    public FrogAI frogPrefab;
    public int poolSize = 10;

    [Header("Spawn Points & Limits")]
    public Transform spawnPoint;
    public Transform counterPoint;
    public Transform exitPoint;
    public int maxActiveFrogs = 6;
    public float spawnInterval = 4f;

    [Header("Coloring (optional)")]
    public Color[] frogColors;

    // runtime
    private List<FrogAI> pool = new List<FrogAI>();
    private float spawnTimer;
    public GameTimer timer;

    // per-day spawn control
    private int customersToSpawn = 0;   // quota for current day
    private int spawnedThisDay = 0;     // how many spawned so far this day

    void OnEnable()
    {
        GameEngine.OnDayStarted += HandleDayStarted;
    }

    void OnDisable()
    {
        GameEngine.OnDayStarted -= HandleDayStarted;
    }

    void Start()
    {
        spawnTimer = spawnInterval;

        for (int i = 0; i < poolSize; i++)
        {
            var instance = Instantiate(frogPrefab);
            instance.gameObject.SetActive(false);
            pool.Add(instance);
        }

        if (frogColors == null || frogColors.Length == 0)
        {
            frogColors = new Color[] {
                Color.green, Color.blue, Color.red, Color.yellow, Color.cyan,
                new Color(1f, 0.5f, 0f),
                new Color(0.5f, 0f, 1f),
                new Color(0.8f, 0.2f, 0.2f),
                new Color(0.3f, 0.9f, 0.5f), Color.white
            };
        }

        // If the day already started before this spawner, initialize quota
        if (GameEngine.CurrentDay > 0)
        {
            customersToSpawn = GameEngine.Instance != null
                ? GameEngine.Instance.GetCustomersRequiredForDay(GameEngine.CurrentDay)
                : GameEngine.Instance.GetCustomersRequiredForDay(GameEngine.CurrentDay);
            spawnedThisDay = 0;
        }
    }

    void Update()
    {
        // don't spawn if game over or timer paused
        if (GameEngine.IsGameOver) return;
        if (timer == null || !timer.GetTimer()) return;

        // stop spawning if we've already spawned the day's quota
        if (spawnedThisDay >= customersToSpawn) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            AttemptSpawn();
            spawnTimer = spawnInterval;
        }
    }

    void AttemptSpawn()
    {
        // check active cap
        int activeCount = 0;
        foreach (var f in pool) if (f.gameObject.activeInHierarchy) activeCount++;
        if (activeCount >= maxActiveFrogs) return;

        // re-check quota
        if (spawnedThisDay >= customersToSpawn) return;

        var frog = GetFromPool();

        // If pool empty, expand it so we can still reach the exact quota
        if (frog == null)
            frog = ExpandPoolAndGet();

        if (frog == null)
        {
            Debug.LogWarning("Spawner: no available frog in pool even after expanding.", this);
            return;
        }

        // position & activate
        frog.transform.position = spawnPoint.position;
        frog.gameObject.SetActive(true);

        // assign scene refs
        frog.counterPoint = counterPoint;

        // random color
        var sr = frog.GetComponent<SpriteRenderer>();
        if (sr != null && frogColors != null && frogColors.Length > 0)
            sr.color = frogColors[Random.Range(0, frogColors.Length)];

        // start AI
        frog.InitializeNew();

        // mark spawn toward today's quota (important: counts even if customer later leaves unserved)
        spawnedThisDay++;
    }

    FrogAI GetFromPool()
    {
        foreach (var f in pool)
            if (!f.gameObject.activeInHierarchy)
                return f;
        return null;
    }

    public FrogAI ExpandPoolAndGet()
    {
        var newInstance = Instantiate(frogPrefab);
        newInstance.gameObject.SetActive(false);
        pool.Add(newInstance);
        return newInstance;
    }

    // --- Event handlers ---

    private void HandleDayStarted(int dayIndex)
    {
        // deactivate any active frogs (clean start of day) and reset counters/timers
        foreach (var f in pool)
        {
            if (f != null)
            {
                f.gameObject.SetActive(false);
                // optional: call a frog reset method if you add one (keeps pool elements clean)
                // f.ResetState();
            }
        }

        customersToSpawn = GameEngine.Instance != null
            ? GameEngine.Instance.GetCustomersRequiredForDay(dayIndex)
            : 0;

        spawnedThisDay = 0;
        spawnTimer = spawnInterval;
    }
}
