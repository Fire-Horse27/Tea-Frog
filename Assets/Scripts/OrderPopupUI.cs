using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a Canvas in the scene. Configure the panel and sprite references in inspector.
/// The script will show the panel when the player is near a frog and update visuals from CustomerOrder.
/// </summary>
public class OrderPopupUI : MonoBehaviour
{
    [Header("Player / detection")]
    public Transform player;                // player transform (drag your player here)
    public float showRadius = 2f;           // how close the player must be to show the popup

    [Header("UI references (panel children)")]
    public GameObject panelRoot;            // the panel GameObject to enable/disable
    public Text orderText;                  // text field that shows human-readable order
    public Image teaFillImage;              // bottom layer: tea color/texture
    public Image cupImage;                  // middle layer: cup sprite (mug/glass)
    public Image milkOverlay;               // top layers for extras (enable/disable)
    public Image honeyOverlay;
    public Image iceOverlay;

    [Header("Sprites (map assets)")]
    public Sprite mugSprite;                // e.g. Tea Cup sprite
    public Sprite glassSprite;              // Glass sprite
    public Sprite[] teaFillSprites;         // same order as teaColors array used by CustomerOrder
    public string[] teaColorNames;          // must match index with teaFillSprites

    public Sprite milkSprite;               // overlay sprite for milk
    public Sprite honeySprite;              // overlay sprite for honey
    public Sprite iceSprite;                // overlay sprite for ice cubes

    void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void Update()
    {
        if (player == null) return;

        // find nearest frog within radius
        FrogAI nearest = FindNearestFrog(player.position, showRadius);
        if (nearest == null)
        {
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
            return;
        }

        // Get its CustomerOrder
        var co = nearest.GetComponent<CustomerOrder>();
        if (co == null)
        {
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
            return;
        }

        // Show panel
        if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);

        // Update text
        if (orderText != null) orderText.text = co.GetOrderString();

        // Update composed image
        // 1) tea fill (match color name -> sprite)
        string color = co.order.teaColor;
        Sprite fill = FindFillSpriteForColor(color);
        if (teaFillImage != null) { teaFillImage.sprite = fill; teaFillImage.enabled = (fill != null); }

        // 2) cup sprite
        Sprite cup = (co.order.cupType == "Glass") ? glassSprite : mugSprite;
        if (cupImage != null) { cupImage.sprite = cup; cupImage.enabled = (cup != null); }

        // 3) extras overlays
        if (milkOverlay != null) { milkOverlay.sprite = milkSprite; milkOverlay.enabled = co.order.milk && milkSprite != null; }
        if (honeyOverlay != null) { honeyOverlay.sprite = honeySprite; honeyOverlay.enabled = co.order.honey && honeySprite != null; }
        if (iceOverlay != null) { iceOverlay.sprite = iceSprite; iceOverlay.enabled = co.order.ice && iceSprite != null; }
    }

    FrogAI FindNearestFrog(Vector3 origin, float radius)
    {
        FrogAI[] frogs = GameObject.FindObjectsOfType<FrogAI>();
        FrogAI best = null;
        float bestD = float.MaxValue;
        float r2 = radius * radius;
        foreach (var f in frogs)
        {
            if (f == null) continue;
            float d2 = (f.transform.position - origin).sqrMagnitude;
            if (d2 <= r2 && d2 < bestD)
            {
                best = f;
                bestD = d2;
            }
        }
        return best;
    }

    Sprite FindFillSpriteForColor(string colorName)
    {
        if (string.IsNullOrEmpty(colorName) || teaFillSprites == null || teaFillSprites.Length == 0) return null;
        if (teaColorNames != null)
        {
            for (int i = 0; i < teaColorNames.Length && i < teaFillSprites.Length; i++)
            {
                if (teaColorNames[i] == colorName) return teaFillSprites[i];
            }
        }
        // fallback: try to parse by name matching (case insensitive)
        for (int i = 0; i < teaFillSprites.Length; i++)
        {
            if (teaFillSprites[i] != null && teaFillSprites[i].name.ToLower().Contains(colorName.ToLower()))
                return teaFillSprites[i];
        }
        return teaFillSprites[0];
    }
}

