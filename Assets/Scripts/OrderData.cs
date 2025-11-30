// OrderData.cs
using UnityEngine;

public enum CupType
{
    None,
    Tea,
    Glass,
    Bucket
}

public enum TeaType
{
    Empty,
    Water,
    Red,
    Green,
    Black,
    Blue
}

[System.Serializable]
public class OrderData
{
    public CupType cupType = CupType.None;
    public TeaType teaType = TeaType.Empty;
    public bool milk;
    public bool honey;
    public bool ice;

    public override string ToString()
    {
        string parts = cupType.ToString();
        if (teaType != TeaType.Empty)
            parts += " " + teaType.ToString();
        if (milk) parts += " + Milk";
        if (honey) parts += " + Honey";
        if (ice) parts += " + Ice";
        return parts;
    }

    // exact field-by-field match
    public bool Matches(OrderData other)
    {
        if (other == null) return false;
        return cupType == other.cupType
            && teaType == other.teaType
            && milk == other.milk
            && honey == other.honey
            && ice == other.ice;
    }
}
