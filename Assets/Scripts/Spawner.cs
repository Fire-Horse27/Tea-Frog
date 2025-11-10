using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public FrogAI frogPrefab;
    public int poolSize = 10;
    public Transform spawnPoint;

    public float spawnInterval = 4f;
    public int maxActiveFrogs = 6;

    private List<FrogAI> pool = new List<FrogAI>();
    private float spawnTimer = 0f;

    void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var f = Instantiate(frogPrefab);
            f.gameObject.SetActive(false);
            pool.Add(f);
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
        int activeCount = 0;
        foreach (var f in pool) if (f.gameObject.activeInHierarchy) activeCount++;
        if (activeCount >= maxActiveFrogs) return;

        var frog = GetFromPool();
        if (frog == null) return;
        frog.transform.position = spawnPoint.position;
        frog.gameObject.SetActive(true);
        frog.InitializeNew();
    }

    FrogAI GetFromPool()
    {
        foreach (var f in pool)
        {
            if (!f.gameObject.activeInHierarchy) return f;
        }
        return null;
    }
}
