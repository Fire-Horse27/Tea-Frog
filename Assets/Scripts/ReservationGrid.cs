using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple grid reservation manager.
/// Frogs reserve Vector3Int cell positions while they move into them.
/// Call TryReserve(cell, frog) to reserve; Release(cell) when leaving.
/// </summary>
public class ReservationGrid : MonoBehaviour
{
    public static ReservationGrid Instance { get; private set; }

    // map cell -> frog that reserved it
    private Dictionary<Vector3Int, FrogAI> reservations = new Dictionary<Vector3Int, FrogAI>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public bool IsReserved(Vector3Int cell)
    {
        return reservations.ContainsKey(cell);
    }

    public FrogAI GetOwner(Vector3Int cell)
    {
        reservations.TryGetValue(cell, out var f);
        return f;
    }

    /// <summary>
    /// Try to reserve a cell for a frog. Returns true if reserved or already reserved by same frog.
    /// </summary>
    public bool TryReserve(Vector3Int cell, FrogAI frog)
    {
        if (frog == null) return false;
        if (reservations.TryGetValue(cell, out var owner))
        {
            if (owner == frog) return true; // already ours
            return false; // someone else owns it
        }
        reservations[cell] = frog;
        return true;
    }

    /// <summary>
    /// Release a cell if owned by this frog (safe to call even if not owner).
    /// </summary>
    public void Release(Vector3Int cell, FrogAI frog)
    {
        if (frog == null) return;
        if (reservations.TryGetValue(cell, out var owner) && owner == frog)
            reservations.Remove(cell);
    }

    /// <summary>
    /// Force release of all cells owned by a frog (call when frog deactivates).
    /// </summary>
    public void ReleaseAllOwnedBy(FrogAI frog)
    {
        if (frog == null) return;
        var remove = new List<Vector3Int>();
        foreach (var kv in reservations)
            if (kv.Value == frog) remove.Add(kv.Key);
        foreach (var c in remove) reservations.Remove(c);
    }
}

