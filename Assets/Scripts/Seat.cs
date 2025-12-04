using UnityEngine;

public class Seat : MonoBehaviour
{
    public bool IsOccupied { get; private set; }
    public Transform SeatPoint => transform;
    public void Reserve() => IsOccupied = true;
    public void Free() => IsOccupied = false;
    public int seatNumber;

    public int getSeatNumber ()
    {
        return seatNumber;
    }
}

