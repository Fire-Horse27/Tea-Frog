using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cafe manager that coordinates seats and a reserved queue.
/// Ensure queuePoints is ordered back -> front (last element sits at the counter).
/// Simplified with the assumption: queued frogs <= seats available.
/// Seating only happens when a frog's order is taken (TryAssignSeat is called for that frog).
/// </summary>
public class CafeManager : MonoBehaviour
{
    public static CafeManager Instance;

    [Header("Seats & Queue (set in inspector)")]
    public Seat[] seats;
    public Transform[] queuePoints; // back -> front (last = at counter)

    // Reservation array parallel to queuePoints. Public for convenience.
    [HideInInspector] public FrogAI[] queueOccupant;

    // Optional list for frogs waiting directly at the counter for player serve
    private List<FrogAI> counterList = new List<FrogAI>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // init queueOccupant
        if (queuePoints != null && queuePoints.Length > 0)
            queueOccupant = new FrogAI[queuePoints.Length];
        else
            queueOccupant = new FrogAI[0];
    }

    #region Queue API

    /// <summary>
    /// Entry point: ask the manager to enqueue this frog.
    /// </summary>
    public void Enqueue(FrogAI frog)
    {
        if (frog == null) return;
        frog.JoinQueue();
    }

    /// <summary>
    /// Return the transform representing the requested queue point (safely).
    /// </summary>
    public Transform GetQueuePoint(int index)
    {
        if (queuePoints == null || queuePoints.Length == 0) return null;
        if (index < 0) return null;
        if (index >= queuePoints.Length) return queuePoints[queuePoints.Length - 1];
        return queuePoints[index];
    }

    /// <summary>
    /// Try to reserve a queue spot immediately for a frog.
    /// Returns true if reservation succeeded or if already reserved by that frog.
    /// </summary>
    public bool TryReserveQueueSpot(int index, FrogAI frog)
    {
        if (index < 0 || index >= queueOccupant.Length) return false;
        if (queueOccupant[index] == null)
        {
            queueOccupant[index] = frog;
            return true;
        }
        if (queueOccupant[index] == frog) return true;
        return false;
    }

    /// <summary>
    /// Called by frog when it physically reaches its queue spot to confirm occupant.
    /// </summary>
    public void ConfirmQueueSpot(int index, FrogAI frog)
    {
        if (index < 0 || index >= queueOccupant.Length) return;
        if (queueOccupant[index] == null || queueOccupant[index] == frog)
            queueOccupant[index] = frog;
    }

    /// <summary>
    /// Free a queue spot if the calling frog owns it.
    /// </summary>
    public void FreeQueueSpot(int index, FrogAI frog)
    {
        if (index < 0 || index >= queueOccupant.Length) return;
        if (queueOccupant[index] == frog)
            queueOccupant[index] = null;
    }

    #endregion

    #region Seating API

    /// <summary>
    /// Finds first free seat and reserves it. Returns true and the seat if found.
    /// Under your assumption (queued frogs <= seats) this should generally always succeed
    /// when called as a result of servicing a frog at the counter.
    /// </summary>
    public bool TryAssignSeat(FrogAI frog, out Seat seat)
    {
        if (seats != null)
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
        }
        seat = null;
        return false;
    }

    /// <summary>
    /// Called by Frog when a seat is freed.
    /// IMPORTANT: do NOT auto-seat from here anymore.
    /// Seating happens only when a frog's order is taken (TryAssignSeat is called).
    /// </summary>
    public void NotifySeatFreed(Seat s)
    {
        if (s == null) return;

        // mark seat free
        s.Free();

        // DO NOT assign the next frog here. Under the "queued frogs <= seats" assumption
        // the seat will be immediately claimed when a frog's OrderTakenByPlayer() calls
        // TryAssignSeat(this, out seat). This prevents non-served frogs from being moved.
    }

    #endregion

    #region Counter / Player serve support (optional)

    /// <summary>
    /// Called by frogs when they arrive at the counter tile and should be served by player.
    /// Adds frog to counterList if not already present.
    /// </summary>
    public void ArriveAtCounter(FrogAI frog)
    {
        if (frog == null) return;
        if (!counterList.Contains(frog)) counterList.Add(frog);
    }

    /// <summary>
    /// Player calls this to serve the nearest frog at the counter (within radius).
    /// Returns true when served.
    /// This method will try to seat the frog being served (only that frog).
    /// </summary>
    public bool ServeNearestAtCounter(Vector3 playerPos, float serveRadius = 1.6f)
    {
        if (counterList.Count == 0) return false;

        FrogAI nearest = null;
        float best = float.MaxValue;
        foreach (var f in counterList)
        {
            if (f == null) continue;
            float d = Vector3.Distance(playerPos, f.transform.position);
            if (d < best) { best = d; nearest = f; }
        }

        if (nearest == null) return false;
        if (best > serveRadius) return false; // too far

        // remove from counter list
        counterList.Remove(nearest);

        // try to seat this frog (only this frog), otherwise re-enqueue
        if (TryAssignSeat(nearest, out Seat seat))
        {
            nearest.AssignSeat(seat);
        }
        else
        {
            Enqueue(nearest);
        }

        // mark as served (frog will call Serve/Seated routine/etc)
        nearest.Serve();
        return true;
    }

    #endregion
}
