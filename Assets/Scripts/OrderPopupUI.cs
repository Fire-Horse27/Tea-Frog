using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OrderPopupUI : MonoBehaviour
{
    [Header("Player / detection")]
    public Transform player;                // player transform (drag your player here)
    public float showRadius = 2f;           // how close the player must be to show the popup

    [Header("UI references (panel children)")]
    public GameObject panelRoot;            // the panel GameObject to enable/disable
    public TMP_Text orderText;              // TextMeshPro text showing order
    public Image teaFillImage;              // bottom layer: tea color/texture
    public Image cupImage;                  // middle layer: cup sprite (mug/glass)
    public Image milkOverlay;               // top layers for extras (enable/disable)
    public Image honeyOverlay;
    public Image iceOverlay;

    [Header("Sprites (map assets)")]
    public Sprite mugSprite;
    public Sprite glassSprite;
    public Sprite[] teaFillSprites;
    public Sprite[] coldTeaSprites;
    public string[] teaColorNames; // e.g. "red tea", "blue tea", "green tea", "black tea"

    public Sprite milkSprite;
    public Sprite honeySprite;
    public Sprite iceSprite;
    public Sprite coldMilkSprite;
    public Sprite coldHoneySprite;

    [Header("Optional: make popup follow frog")]
    public Camera worldCamera;              // set main camera if using world->screen positioning
    public RectTransform panelRect;         // panel RectTransform (OrderPanel)

    // internal state to avoid repeated SetActive calls spam
    private bool panelVisible = false;

    void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        panelVisible = false;
    }

    void Update()
    {
        if (player == null || panelRoot == null)
        {
            if (player == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) { player = p.transform; }
            }
            return;
        }

        FrogAI nearest = FindNearestFrog(player.position, showRadius);

        // if no frog or frog isn't ready to show an order, hide
        if (nearest == null || !nearest.orderTaken)
        {
            if (panelVisible)
            {
                panelRoot.SetActive(false);
                panelVisible = false;
            }
            return;
        }

        var co = nearest.GetComponent<CustomerOrder>();
        if (co == null)
        {
            if (panelVisible) { panelRoot.SetActive(false); panelVisible = false; }
            return;
        }

        if (!panelVisible)
        {
            panelRoot.SetActive(true);
            panelVisible = true;
        }

        if (orderText != null) orderText.text = co.GetOrderString();

        // --- TEA FILL sprite: use teaType enum to select matching sprite/index
        Sprite fill = FindFillSpriteForTeaType(co.order.teaType, co.order.cupType);
        if (teaFillImage != null) { teaFillImage.sprite = fill; teaFillImage.enabled = (fill != null); }

        // cup sprite
        Sprite cup = (co.order.cupType == CupType.Glass) ? glassSprite : mugSprite;
        if (cupImage != null) { cupImage.sprite = cup; cupImage.enabled = (cup != null); }

        // extras
        if (milkOverlay != null && co.order.cupType == CupType.Tea) 
        { 
            milkOverlay.sprite = milkSprite; 
            milkOverlay.enabled = co.order.milk && milkSprite != null; 
        } 
        else if (milkOverlay != null && co.order.cupType == CupType.Glass)
        {
            milkOverlay.sprite = coldMilkSprite;
            milkOverlay.enabled = co.order.milk && coldMilkSprite != null;
        }
        if (honeyOverlay != null && co.order.cupType == CupType.Tea) 
        { 
            honeyOverlay.sprite = honeySprite; 
            honeyOverlay.enabled = co.order.honey && honeySprite != null; 
        }
        else if (honeyOverlay != null && co.order.cupType == CupType.Glass)
        {
            honeyOverlay.sprite = coldHoneySprite;
            honeyOverlay.enabled = co.order.honey && coldHoneySprite != null;
        }
        if (iceOverlay != null) { iceOverlay.sprite = iceSprite; iceOverlay.enabled = co.order.ice && iceSprite != null; }

        // optional: move panel to follow frog's screen position
        if (panelRect != null && worldCamera != null && nearest != null)
        {
            Vector3 worldPos = nearest.transform.position + Vector3.up * 1.2f;
            Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);
            panelRect.position = screenPos;
        }
    }

    FrogAI FindNearestFrog(Vector3 origin, float radius)
    {
        FrogAI[] frogs = GameObject.FindObjectsOfType<FrogAI>();
        FrogAI best = null;
        float bestD2 = radius * radius;
        foreach (var f in frogs)
        {
            if (f == null) continue;
            float d2 = (f.transform.position - origin).sqrMagnitude;
            if (d2 <= bestD2)
            {
                if (best == null || d2 < (best.transform.position - origin).sqrMagnitude)
                {
                    best = f;
                }
            }
        }
        return best;
    }

    // Normalize teaColorNames entries and match against TeaType enum
    Sprite FindFillSpriteForTeaType(TeaType tea, CupType cup)
    {
        if (teaFillSprites == null || teaFillSprites.Length == 0) return null;

        string key = tea.ToString().ToLowerInvariant(); // e.g. "red", "green"
        if (teaColorNames != null)
        {
            for (int i = 0; i < teaColorNames.Length && i < teaFillSprites.Length; i++)
            {
                if (string.IsNullOrEmpty(teaColorNames[i])) continue;
                // normalize: remove "tea" and spaces
                string norm = teaColorNames[i].ToLowerInvariant().Replace(" ", "").Replace("tea", "").Trim();
                if (norm == key && cup == CupType.Tea)
                {
                    return teaFillSprites[i];
                }
                else if (norm == key && cup == CupType.Glass)
                {
                    return coldTeaSprites[i];
                }
            }
        }

        // fallback: try to find a sprite whose filename contains the enum name
        for (int i = 0; i < teaFillSprites.Length; i++)
        {
            var s = teaFillSprites[i];
            if (s == null) continue;
            if (s.name.ToLowerInvariant().Contains(key)) return s;
        }

        // last resort return index 0
        if (cup == CupType.Tea) {
            return teaFillSprites[0];
        }
        else
        {
            return coldTeaSprites[0];
        }
    }
}
