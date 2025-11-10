using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Header("Prefab & Pool")]
    // Make sure this type matches the component on your prefab (FrogAI2D or FrogAI)
    public FrogAI frogPrefab;           // or FrogAI if your class is named FrogAI
    public int poolSize = 10;

    [Header("Spawn Points & Limits")]
    public Transform spawnPoint;
    public Transform counterPoint;         // assign your CounterPoint in inspector
    public Transform exitPoint;            // optional (not required if frogs use tag "Exit")
    public int maxActiveFrogs = 6;
    public float spawnInterval = 4f;

    // optional color palette (leave empty to skip tinting)
    [Header("Coloring (optional)")]
    public Color[] frogColors;

    // runtime
    private List<FrogAI> pool = new List<FrogAI>();
    private float spawnTimer;

    void Start()
    {
        // initialize timer so first spawn happens after spawnInterval (or set to 0 to spawn immediately)
        spawnTimer = spawnInterval;

        // create pool
        for (int i = 0; i < poolSize; i++)
        {
            var instance = Instantiate(frogPrefab);
            instance.gameObject.SetActive(false);
            pool.Add(instance);
        }

        // if no colors provided, set default palette
        if (frogColors == null || frogColors.Length == 0)
        {
            frogColors = new Color[]
            {
                Color.green, Color.blue, Color.red, Color.yellow, Color.cyan,
                new Color(1f, 0.5f, 0f),
                new Color(0.5f, 0f, 1f),
                new Color(0.8f, 0.2f, 0.2f),
                new Color(0.3f, 0.9f, 0.5f), Color.white
            };
        }
    }

    void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            AttemptSpawn();
            spawnTimer = spawnInterval;
        }
    }

    void AttemptSpawn()
    {
        // count active
        int activeCount = 0;
        foreach (var f in pool) if (f.gameObject.activeInHierarchy) activeCount++;
        if (activeCount >= maxActiveFrogs) return;

        var frog = GetFromPool();
        if (frog == null)
        {
            Debug.LogWarning("Spawner: no available frog in pool (consider increasing poolSize).", this);
            return;
        }

        // position & activate
        frog.transform.position = spawnPoint.position;
        frog.gameObject.SetActive(true);

        // assign scene references before initializing
        frog.counterPoint = counterPoint;
        // optionally give an exit point if your frog uses it (otherwise it finds by tag)
        // if (exitPoint != null) frog.exitPoint = exitPoint;

        // set random color if SpriteRenderer exists
        var sr = frog.GetComponent<SpriteRenderer>();
        if (sr != null && frogColors != null && frogColors.Length > 0)
        {
            sr.color = frogColors[Random.Range(0, frogColors.Length)];
        }

        // start AI
        frog.InitializeNew();

        Debug.Log($"Spawned {frog.name} (active={activeCount + 1}) and sent to counter.", frog);
    }

    FrogAI GetFromPool()
    {
        foreach (var f in pool)
            if (!f.gameObject.activeInHierarchy)
                return f;
        return null;
    }

    // Optional helper to expand pool at runtime
    public FrogAI ExpandPoolAndGet()
    {
        var newInstance = Instantiate(frogPrefab);
        newInstance.gameObject.SetActive(false);
        pool.Add(newInstance);
        return newInstance;
    }
}
