using System;
[Serializable]
public struct OrderData
{
    public string cupType;   // "Glass" or "Tea" (mug)
    public string teaColor;  // e.g. "Green", "Black", "Rooibos"
    public bool milk;
    public bool honey;
    public bool ice;

    public override string ToString()
    {
        // Build readable order string
        string s = $"{teaColor} tea in a {cupType}";
        string extras = "";
        if (milk) extras += (extras.Length > 0 ? ", " : "") + "milk";
        if (honey) extras += (extras.Length > 0 ? ", " : "") + "honey";
        if (ice) extras += (extras.Length > 0 ? ", " : "") + "ice";
        if (extras.Length > 0) s += $" with {extras}";
        return s;
    }

    public bool Matches(OrderData other)
    {
        // Exact match of all fields
        return cupType == other.cupType &&
               teaColor == other.teaColor &&
               milk == other.milk &&
               honey == other.honey &&
               ice == other.ice;
    }
}

