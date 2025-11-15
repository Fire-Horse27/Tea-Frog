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
    public string[] teaColorNames;

    public Sprite milkSprite;
    public Sprite honeySprite;
    public Sprite iceSprite;

    [Header("Optional: make popup follow frog")]
    public Camera worldCamera;              // set main camera if using world->screen positioning
    public RectTransform panelRect;         // panel RectTransform (OrderPanel)

    // internal state to avoid repeated SetActive calls spam
    private bool panelVisible = false;

    void Start()
    {
        // Basic reference checks
        if (player == null) Debug.LogWarning("[OrderPopupUI] player reference is NULL. Assign your player Transform in inspector.", this);
        if (panelRoot == null) Debug.LogWarning("[OrderPopupUI] panelRoot reference is NULL. Assign OrderPanel in inspector.", this);
        if (orderText == null) Debug.LogWarning("[OrderPopupUI] orderText (TMP) is NULL. Assign OrderText (TextMeshPro) in inspector.", this);
        if (teaFillImage == null) Debug.LogWarning("[OrderPopupUI] teaFillImage is NULL. Assign TeaFillImage UI Image in inspector.", this);
        if (cupImage == null) Debug.LogWarning("[OrderPopupUI] cupImage is NULL. Assign CupImage UI Image in inspector.", this);

        // hide panel at start
        if (panelRoot != null) panelRoot.SetActive(false);
        panelVisible = false;
    }

    void Update()
    {
        // quick safety: do nothing if required references missing
        if (player == null || panelRoot == null)
        {
            // still attempt to find player automatically (helpful if forgot to assign)
            if (player == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) { player = p.transform; Debug.Log("[OrderPopupUI] Auto-assigned player from tag 'Player'.", this); }
            }
            return;
        }

        // find nearest frog in radius
        FrogAI nearest = FindNearestFrog(player.position, showRadius);

        if (nearest == null)
        {
            // hide panel if it was shown
            if (panelVisible)
            {
                panelRoot.SetActive(false);
                panelVisible = false;
                Debug.Log("[OrderPopupUI] No frog within range -> hiding panel.", this);
            }
            return;
        }

        // found a frog. debug log (not every frame)
        // show details once when it becomes active or new frog changes
        if (!panelVisible)
        {
            Debug.Log($"[OrderPopupUI] Nearest frog: {nearest.name} at distance {Vector3.Distance(player.position, nearest.transform.position):F2}", this);
        }

        // get order
        var co = nearest.GetComponent<CustomerOrder>();
        if (co == null)
        {
            Debug.LogWarning("[OrderPopupUI] nearest frog has no CustomerOrder component.", nearest);
            if (panelVisible) { panelRoot.SetActive(false); panelVisible = false; }
            return;
        }

        // show panel and update UI only when necessary
        if (!panelVisible)
        {
            panelRoot.SetActive(true);
            panelVisible = true;
        }

        // update text (safe check)
        if (orderText != null) orderText.text = co.GetOrderString();

        // update sprites: tea fill
        Sprite fill = FindFillSpriteForColor(co.order.teaColor);
        if (teaFillImage != null) { teaFillImage.sprite = fill; teaFillImage.enabled = (fill != null); }

        // cup sprite
        Sprite cup = (co.order.cupType == "Glass") ? glassSprite : mugSprite;
        if (cupImage != null) { cupImage.sprite = cup; cupImage.enabled = (cup != null); }

        // extras
        if (milkOverlay != null) { milkOverlay.sprite = milkSprite; milkOverlay.enabled = co.order.milk && milkSprite != null; }
        if (honeyOverlay != null) { honeyOverlay.sprite = honeySprite; honeyOverlay.enabled = co.order.honey && honeySprite != null; }
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
                // we prefer the closest
                if (best == null || d2 < (best.transform.position - origin).sqrMagnitude)
                {
                    best = f;
                }
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
                if (teaColorNames[i].ToLower() == colorName.ToLower()) return teaFillSprites[i];
            }
        }
        // fallback: try contains match
        for (int i = 0; i < teaFillSprites.Length; i++)
        {
            if (teaFillSprites[i] != null && teaFillSprites[i].name.ToLower().Contains(colorName.ToLower()))
                return teaFillSprites[i];
        }
        return teaFillSprites[0];
    }
}
