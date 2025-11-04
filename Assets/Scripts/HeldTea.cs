using UnityEngine;

public class HeldTea : MonoBehaviour
{
    string cupHeld = "";
    public string cupFilled = "";

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
        if (cupFilled == "Water" || cupFilled == "Hot") EnableSprite(tea + " " + cupHeld);
        cupFilled = "Tea";
    }

    void SetIce()
    {
        if (cupHeld == "Glass") EnableSprite("Ice");
    }

    void SetHoney()
    {
        if (cupFilled == "Tea") EnableSprite("Honey " + cupHeld);
    }

    void SetMilk()
    {
        if (cupFilled == "Tea") EnableSprite("Milk " + cupHeld);
    }

    void SetHot()
    {
        if (cupHeld == "Tea" && cupFilled == "") EnableSprite("Hot Tea");
        cupFilled = "Hot";
    }

    void SetWater()
    {
        if (cupHeld == "Glass" && cupFilled == "") EnableSprite("Iced Tea");
        cupFilled = "Water";
    }

    public void ClearEverything()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.enabled = false;
        }
        cupFilled = "";
        cupHeld = "";
    }
}
