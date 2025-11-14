using System.Collections.Generic;
using UnityEngine;

public class CafeManager : MonoBehaviour
{
    public static CafeManager Instance;
    public Seat[] seats;
    public Transform[] queuePoints; // set in scene: from back -> front

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

    /// <summary>
    /// Enqueue is now a thin wrapper that tells the frog to join the shared queue.
    /// </summary>
    public void Enqueue(FrogAI frog)
    {
        if (frog == null) return;
        frog.JoinQueue();
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

        // Attempt to seat the next waiting frog from the shared queue.
        FrogAI next = FrogAI.PopFrontOfQueue();
        if (next != null)
        {
            // AssignSeat will remove the frog from the queue (safe if already removed).
            next.AssignSeat(s);
        }
    }
}
