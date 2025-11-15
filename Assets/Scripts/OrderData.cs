using System;

[Serializable]
public struct OrderData
{
    public CupType cupType;    // Tea mug or Glass
    public TeaType teaType;    // Red, Green, Black, or Blue
    public bool milk;
    public bool honey;
    public bool ice;

    public override string ToString()
    {
        string s = $"{teaType} tea in a {(cupType == CupType.Tea ? "mug" : "glass")}";
        string extras = "";
        if (milk) extras += (extras.Length > 0 ? ", " : "") + "milk";
        if (honey) extras += (extras.Length > 0 ? ", " : "") + "honey";
        if (ice) extras += (extras.Length > 0 ? ", " : "") + "ice";
        if (extras.Length > 0) s += $" with {extras}";
        return s;
    }

    public bool Matches(OrderData other)
    {
        return cupType == other.cupType &&
               teaType == other.teaType &&
               milk == other.milk &&
               honey == other.honey &&
               ice == other.ice;
    }
}

public enum CupType { Tea, Glass }
public enum TeaType { Red, Green, Black, Blue }
