using System.Collections.Generic;
using UnityEngine;

public class CafeManager : MonoBehaviour
{
    public static CafeManager Instance;
    public Seat[] seats;
    public Transform[] queuePoints; // set in scene: from back -> front

    private Queue<FrogAI> waitingQueue = new Queue<FrogAI>();

    void Awake()
    {
        if (Instance == null) Instance = this; else Destroy(gameObject);
    }

    public Transform GetQueuePoint(int index)
    {
        if (index < 0) return null;
        if (index >= queuePoints.Length) return queuePoints[queuePoints.Length - 1];
        return queuePoints[index];
    }

    public void Enqueue(FrogAI frog)
    {
        waitingQueue.Enqueue(frog);
        UpdateQueuePositions();
    }

    void UpdateQueuePositions()
    {
        int i = 0;

        // Track which queue spots are currently occupied
        bool[] spotOccupied = new bool[queuePoints.Length];

        // Mark any queue point that already has a frog standing near it
        foreach (var frog in waitingQueue)
        {
            for (int j = 0; j < queuePoints.Length; j++)
            {
                if (Vector3.Distance(frog.transform.position, queuePoints[j].position) < 0.1f)
                {
                    spotOccupied[j] = true;
                    break;
                }
            }
        }

        // Now assign queue indexes only to open spots
        foreach (var frog in waitingQueue)
        {
            // Skip if the frog already has a queue spot assigned and is still there
            bool alreadyInSpot = false;
            for (int j = 0; j < queuePoints.Length; j++)
            {
                if (Vector3.Distance(frog.transform.position, queuePoints[j].position) < 0.1f)
                {
                    alreadyInSpot = true;
                    break;
                }
            }
            if (alreadyInSpot) continue;

            // Find the next open queue spot
            while (i < queuePoints.Length && spotOccupied[i]) i++;

            if (i < queuePoints.Length)
            {
                frog.SetQueueIndex(i);
                spotOccupied[i] = true;
                Debug.Log($"{frog.name} moved to queue spot {i + 1}");
                i++;
            }
            else
            {
                Debug.Log($"{frog.name} cannot move forward — all queue spots are full");
            }
        }
    }


    public bool TryAssignSeat(FrogAI frog, out Seat seat)
    {
        foreach (var s in seats)
        {
            if (!s.IsOccupied)
            {
                s.Reserve();
                seat = s;
                return true;
            }
        }
        seat = null;
        return false;
    }

    public void NotifySeatFreed(Seat s)
    {
        s.Free();
        // immediately send next frog to seat if one is waiting
        if (waitingQueue.Count > 0)
        {
            var next = waitingQueue.Dequeue();
            UpdateQueuePositions();
            next.AssignSeat(s);
        }
    }
}

