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
        foreach (var frog in waitingQueue)
        {
            frog.SetQueueIndex(i);
            i++;
            Debug.Log(frog + " made it to " + i + " spot in the queue");
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

