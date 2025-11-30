using System;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;

public class HeldTea : MonoBehaviour
{
    public CupType cupHeld = CupType.Tea;
    public TeaType teaType = TeaType.Empty;
    private bool hasMilk;
    private bool hasHoney;
    private bool hasIce;

    // -----------------------
    // Simple setters
    // -----------------------
    public void SetCup(CupType cup)
    {
        if (cupHeld == CupType.None)
        {
            cupHeld = cup;
            EnableSprite(cup.ToString() + " Cup");
        }
    }

    public void AddMilk()
    {
        if (teaType != TeaType.Empty && teaType != TeaType.Water)
        {
            hasMilk = true;
            EnableSprite("Milk " + cupHeld.ToString());
        }
    }

    public void AddHoney()
    {
        if (teaType != TeaType.Empty && teaType != TeaType.Water)
        {
            hasHoney = true;
            EnableSprite("Honey " + cupHeld.ToString());
        }
    }

    public void AddIce()
    {
        if (cupHeld == CupType.Glass)
        {
            hasIce = true;
            EnableSprite("Ice");
        }
    }

    public void SetWater(string temp)
    {
        if (
            (cupHeld == CupType.Tea && teaType == TeaType.Empty && temp == "Hot") || 
            (cupHeld == CupType.Glass && teaType == TeaType.Empty && temp == "Water")
           )
        {
            teaType = TeaType.Water;
            EnableSprite("Water " + cupHeld.ToString());
        }
        else if (cupHeld == CupType.Bucket)
        {
            SetBucket("Full");
        }
    }

    public void SetBucket(string capacity)
    {
        if (cupHeld == CupType.None || cupHeld == CupType.Bucket)
        {
            cupHeld = CupType.Bucket;
            DisableSprite("Empty Bucket");
            DisableSprite("Full Bucket");
            EnableSprite(capacity + " Bucket");
            if (capacity == "Full") teaType = TeaType.Water;
        }
    }

    public void SetTea(TeaType tea)
    {
        if (teaType == TeaType.Water && cupHeld != CupType.Bucket)
        {
            DisableSprite("Water " + cupHeld.ToString());
            teaType = tea;
            EnableSprite(tea.ToString() + " " + cupHeld.ToString());
        }
    }

    void EnableSprite(string name)
    {
        var t = transform.Find(name);
        if (t)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr) sr.enabled = true;
        }
        else
        {
            Debug.LogWarning($"Child '{name}' not found!");
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
        else
        {
            Debug.LogWarning($"Child '{name}' not found!");
        }
    }

    public void ClearEverything()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = false;
        teaType = TeaType.Empty;
        cupHeld = CupType.None;
        hasMilk = hasHoney = hasIce = false;
    }

    // -----------------------
    // Comparison interface
    // -----------------------
    public OrderData GetOrderData()
    {
        OrderData od = new OrderData
        {
            cupType = cupHeld,
            teaType = teaType,
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
    }

    public void SetHeld(string item)
    {
        switch (item)
        {
            case "Glass":
                SetCup(CupType.Glass);
                break;

            case "Tea":
                SetCup(CupType.Tea);
                break;

            case "Ice":
                AddIce();
                break;

            case "Honey":
                AddHoney();
                break;

            case "Milk":
                AddMilk();
                break;

            case "Water":
            case "Hot":
                SetWater(item);
                break;

            case "Black":
                SetTea(TeaType.Black);
                break;

            case "Blue":
                SetTea(TeaType.Blue);
                break;

            case "Green":
                SetTea(TeaType.Green);
                break;

            case "Red":
                SetTea(TeaType.Red);
                break;

            case "Bucket":
                SetBucket("Empty");
                break;

            default:
                Debug.Log(item + " not found");
                break;
        }
    }
}
