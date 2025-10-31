using UnityEngine;

public class HeldTea : MonoBehaviour
{
    string cupHeld = "";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

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
        Debug.LogWarning($"Finding '{item}'!");
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
        if (cupHeld != "") EnableSprite(tea + " " + cupHeld);
    }

    void SetIce()
    {
        if (cupHeld == "Glass") EnableSprite("Ice");
    }

    void SetHoney()
    {
        if (cupHeld != "") EnableSprite("Honey " + cupHeld);
    }

    void SetMilk()
    {
        if (cupHeld != "") EnableSprite("Milk " + cupHeld);
    }

    public void ClearEverything()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.enabled = false;
        }
    }
}
