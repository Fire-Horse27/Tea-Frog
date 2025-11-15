using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks what the player is holding and exposes it as OrderData.
/// Keeps your existing sprite enable/disable behavior but also records flags
/// so we can compare against a Customer's order.
/// </summary>
public class HeldTea : MonoBehaviour
{
    public string cupHeld = "";   // "Glass" or "Tea" (mug)
    public string cupFilled = ""; // "Tea", "Hot", "Water", etc.

    // tracked extras
    private bool hasMilk = false;
    private bool hasHoney = false;
    private bool hasIce = false;
    private string currentTeaColor = ""; // e.g. "Green", "Black"

    void EnableSprite(string childName)
    {
        Transform t = transform.Find(childName);
        if (t != null)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = true;
        }
        else
        {
            Debug.LogWarning($"Child '{childName}' not found!");
        }
    }

    void DisableSprite(string childName)
    {
        Transform t = transform.Find(childName);
        if (t != null)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
        else
        {
            Debug.LogWarning($"Child '{childName}' not found!");
        }
    }

    public void SetHeld(string item)
    {
        switch (item)
        {
            case "Glass":
            case "Tea":
                SetCup(item);
                break;

            case "Ice":
                SetIce();
                break;

            case "Honey":
                SetHoney();
                break;

            case "Milk":
                SetMilk();
                break;

            case "Hot":
                SetHot();
                break;

            case "Water":
                SetWater();
                break;

            default:
                // treat any other string as a tea color e.g. "Green", "Black"
                SetTea(item);
                break;
        }
    }

    void SetCup(string cup)
    {
        cupHeld = cup;
        EnableSprite(cup + " Cup");
    }

    void SetTea(string tea)
    {
        // Only set tea if a cup exists that accepts tea/water/hot behavior (keeps your original guards)
        // We will set currentTeaColor so matching works.
        if (cupFilled == "Water" || cupFilled == "Hot" || cupFilled == "" || cupFilled == "Tea")
        {
            EnableSprite(tea + " " + cupHeld);
            DisableSprite("Hot Tea");
            DisableSprite("Water Glass");
            cupFilled = "Tea";
            currentTeaColor = tea;
        }
    }

    void SetIce()
    {
        if (cupHeld == "Glass")
        {
            EnableSprite("Ice");
            hasIce = true;
        }
    }

    void SetHoney()
    {
        if (cupFilled == "Tea")
        {
            EnableSprite("Honey " + cupHeld);
            hasHoney = true;
        }
    }

    void SetMilk()
    {
        if (cupFilled == "Tea")
        {
            EnableSprite("Milk " + cupHeld);
            hasMilk = true;
        }
    }

    void SetHot()
    {
        if (cupHeld == "Tea" && cupFilled == "")
        {
            EnableSprite("Hot Tea");
            cupFilled = "Hot";
            currentTeaColor = "Black"; // optional default for hot, or leave empty
        }
    }

    void SetWater()
    {
        if (cupHeld == "Glass" && cupFilled == "")
        {
            EnableSprite("Iced Tea");
            cupFilled = "Water";
            currentTeaColor = "Green"; // optional default for iced, or leave empty
        }
    }

    public void ClearEverything()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.enabled = false;
        }
        cupFilled = "";
        cupHeld = "";
        hasMilk = false;
        hasHoney = false;
        hasIce = false;
        currentTeaColor = "";
    }

    // ----------------------
    // Expose order information so we can compare to customer's order
    // ----------------------
    public OrderData GetOrderData()
    {
        OrderData od = new OrderData
        {
            cupType = cupHeld,
            teaColor = currentTeaColor,
            milk = hasMilk,
            honey = hasHoney,
            ice = hasIce
        };
        return od;
    }

    // Optional: helper to set precise order data (useful in testing / UI)
    public void ApplyOrderData(OrderData od)
    {
        ClearEverything();

        if (!string.IsNullOrEmpty(od.cupType)) SetCup(od.cupType);
        if (!string.IsNullOrEmpty(od.teaColor)) SetTea(od.teaColor);
        if (od.ice) SetIce();
        if (od.honey) SetHoney();
        if (od.milk) SetMilk();
    }
}
