using System;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GrabItem : MonoBehaviour
{
    [Tooltip("ID passed to HeldTea - can be: Glass, Tea, Ice, Honey, Milk, Hot, Water, or a tea color name (Red/Green/Black/Blue)")]
    public string itemID;

    [Tooltip("y offset in world units for the button above the item")]
    public float promptOffsetY = .5f;

    // Shared references (set by EButtonRegistrar)
    public static Transform Button;
    public static Transform playerTransform;

    Collider2D col;
    HeldTea heldTea;
    public bool showingPrompt;

    // sensible defaults when emulating Hot/Water behavior
    public TeaType defaultHotTea = TeaType.Black;
    public TeaType defaultIcedTea = TeaType.Green;

    void Awake()
    {
        col = GetComponent<Collider2D>();

        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
            heldTea = playerTransform.GetComponentInChildren<HeldTea>();
    }

    void Update()
    {
        if (playerTransform == null || Button == null)
            return;

        bool playerInside = false;
        try
        {
            playerInside = col.OverlapPoint(playerTransform.position);
        }
        catch (Exception)
        {
            playerInside = false;
        }

        if (playerInside)
        {
            showingPrompt = true;
            if (Button != null)
            {
                Button.position = new Vector3(transform.position.x,
                                              transform.position.y + promptOffsetY,
                                              -1);
                Button.gameObject.SetActive(true);
            }
        }
        else if (!playerInside && showingPrompt)
        {
            if (Button != null) Button.gameObject.SetActive(false);
            showingPrompt = false;
        }

        if (showingPrompt && heldTea != null && Input.GetKeyDown(KeyCode.E))
        {
            HandleUse();
        }
    }

    void HandleUse()
    {
        if (heldTea == null) return;

        // Trashcan behaviour
        if (itemID.Equals("Trashcan", StringComparison.OrdinalIgnoreCase))
        {
            heldTea.ClearEverything();
            return;
        }

        string id = itemID.Trim().ToLowerInvariant();

        // Known keywords
        if (id == "glass")
        {
            heldTea.SetCup(CupType.Glass);
            return;
        }

        if (id == "tea")
        {
            heldTea.SetCup(CupType.Tea);
            return;
        }

        if (id == "ice")
        {
            heldTea.AddIce();
            return;
        }

        if (id == "honey")
        {
            heldTea.AddHoney();
            return;
        }

        if (id == "milk")
        {
            heldTea.AddMilk();
            return;
        }

        if (id == "hot")
        {
            // emulate "hot" by ensuring mug and setting a default tea
            heldTea.SetCup(CupType.Tea);
            heldTea.SetTea(defaultHotTea);
            return;
        }

        if (id == "water")
        {
            // emulate "water" as iced tea in a glass with a default flavor
            heldTea.SetCup(CupType.Glass);
            heldTea.SetTea(defaultIcedTea);
            heldTea.AddIce();
            return;
        }

        // If id isn't a keyword, try to parse it as a tea color name:
        // Accepts "red", "red tea", "Red Tea", etc.
        if (TryParseTeaType(itemID, out TeaType tea))
        {
            // Ensure there's a cup (if none, default to mug)
            // We assume HeldTea.SetTea will work even if cup wasn't set, but we'll set a default cup if needed.
            heldTea.SetTea(tea);
            return;
        }

        // If all else fails, as a fallback call ClearEverything or do nothing
        Debug.LogWarning($"GrabItem: unknown itemID '{itemID}' - no action taken.");
    }

    bool TryParseTeaType(string s, out TeaType result)
    {
        result = TeaType.Red;
        if (string.IsNullOrEmpty(s)) return false;

        string clean = s.Trim().ToLowerInvariant();
        if (clean.EndsWith(" tea")) clean = clean.Substring(0, clean.Length - 4).Trim();

        // capitalize first letter to match enum names (Red, Green, Black, Blue)
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
