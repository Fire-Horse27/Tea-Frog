using System;
using UnityEngine;

public class HeldTea : MonoBehaviour
{
    public CupType cupHeld = CupType.Tea;
    public TeaType? teaType = null; // null = empty cup
    private bool hasMilk;
    private bool hasHoney;
    private bool hasIce;

    // compatibility: some scripts still read/write cupFilled
    public string cupFilled = "";

    void EnableSprite(string name)
    {
        var t = transform.Find(name);
        if (t)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = true;
        }
    }

    void DisableSprite(string name)
    {
        var t = transform.Find(name);
        if (t)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
    }

    public void ClearEverything()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = false;
        teaType = null;
        hasMilk = hasHoney = hasIce = false;
        cupFilled = "";
    }

    // -----------------------
    // Simple setters
    // -----------------------
    public void SetCup(CupType type)
    {
        cupHeld = type;
        EnableSprite(type == CupType.Glass ? "Glass Cup" : "Tea Cup");
    }

    public void SetTea(TeaType tea)
    {
        teaType = tea;
        EnableSprite($"{tea} {(cupHeld == CupType.Glass ? "Glass" : "Tea")}");
    }

    public void AddMilk()
    {
        if (teaType != null) { hasMilk = true; EnableSprite("Milk " + (cupHeld == CupType.Glass ? "Glass" : "Tea")); }
    }

    public void AddHoney()
    {
        if (teaType != null) { hasHoney = true; EnableSprite("Honey " + (cupHeld == CupType.Glass ? "Glass" : "Tea")); }
    }

    public void AddIce()
    {
        if (cupHeld == CupType.Glass) { hasIce = true; EnableSprite("Ice"); }
    }

    // -----------------------
    // Comparison interface
    // -----------------------
    public OrderData GetOrderData()
    {
        OrderData od = new OrderData
        {
            cupType = cupHeld,
            teaType = teaType ?? TeaType.Red,
            milk = hasMilk,
            honey = hasHoney,
            ice = hasIce
        };
        return od;
    }

    public void ApplyOrderData(OrderData od)
    {
        ClearEverything();
        SetCup(od.cupType);
        SetTea(od.teaType);
        if (od.milk) AddMilk();
        if (od.honey) AddHoney();
        if (od.ice) AddIce();
        cupFilled = od.ice ? "Water" : (od.teaType.ToString() + " tea"); // mild compatibility
    }

    // ------- Compatibility shim (string SetHeld) -------
    public void SetHeld(string item)
    {
        if (string.IsNullOrEmpty(item)) return;

        // Cup names
        if (item.Equals("Glass", StringComparison.OrdinalIgnoreCase))
        {
            SetCup(CupType.Glass);
            cupFilled = "";
            return;
        }
        if (item.Equals("Tea", StringComparison.OrdinalIgnoreCase))
        {
            SetCup(CupType.Tea);
            cupFilled = "";
            return;
        }

        // extras + special strings
        if (item.Equals("Ice", StringComparison.OrdinalIgnoreCase))
        {
            AddIce();
            if (cupHeld == CupType.Glass) cupFilled = "Water";
            return;
        }
        if (item.Equals("Honey", StringComparison.OrdinalIgnoreCase))
        {
            AddHoney();
            return;
        }
        if (item.Equals("Milk", StringComparison.OrdinalIgnoreCase))
        {
            AddMilk();
            return;
        }
        if (item.Equals("Hot", StringComparison.OrdinalIgnoreCase))
        {
            // emulate hot: mug + default hot tea
            SetCup(CupType.Tea);
            SetTea(TeaType.Black);
            cupFilled = "Hot";
            return;
        }
        if (item.Equals("Water", StringComparison.OrdinalIgnoreCase))
        {
            // emulate iced glass
            SetCup(CupType.Glass);
            SetTea(TeaType.Green);
            AddIce();
            cupFilled = "Water";
            return;
        }

        // Try parse as tea color: "red", "red tea", etc.
        if (TryParseTeaType(item, out TeaType tea))
        {
            SetTea(tea);
            cupFilled = "Tea";
            return;
        }

        Debug.LogWarning($"HeldTea.SetHeld: unknown item '{item}' (compat shim)", this);
    }

    // helper used by shim
    bool TryParseTeaType(string s, out TeaType result)
    {
        result = TeaType.Red;
        if (string.IsNullOrEmpty(s)) return false;

        string clean = s.Trim().ToLowerInvariant();
        if (clean.EndsWith(" tea")) clean = clean.Substring(0, clean.Length - 4).Trim();

        string candidate = CapitalizeFirst(clean);
        return Enum.TryParse<TeaType>(candidate, true, out result);
    }

    string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length == 1) return s.ToUpperInvariant();
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
