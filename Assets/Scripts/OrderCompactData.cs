using System;

public static class OrderDataCompat
{
    public static string GetTeaColorString(this OrderData od)
    {
        return od.teaType.ToString(); // or use .ToDisplayString() if you added that
    }
}

