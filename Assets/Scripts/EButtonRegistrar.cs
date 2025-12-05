using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class EButtonRegistrar : MonoBehaviour
{
    [Header("Press Sprites")]
    [Tooltip("Sprite shown when E is not pressed")]
    public Sprite unpressedSprite;

    [Tooltip("Sprite shown while E is held down")]
    public Sprite pressedSprite;

    SpriteRenderer sr;
    bool lastPressed;

    void Awake()
    {
        // Register the shared button reference for all GrabItem scripts
        GrabItem.Button = transform;
        Cashregister.Button = transform;

        // Cache SpriteRenderer
        sr = GetComponent<SpriteRenderer>();

        // Ensure correct starting sprite and hide the button
        if (unpressedSprite != null)
            sr.sprite = unpressedSprite;

        gameObject.SetActive(false);
    }

    void Update()
    {
        // Only bother checking when the button is visible
        if (!gameObject.activeSelf) return;

        bool isPressed = Input.GetKey(KeyCode.E);
        if (isPressed != lastPressed)
        {
            lastPressed = isPressed;
            sr.sprite = isPressed ? pressedSprite : unpressedSprite;
        }
    }

    void OnEnable()
    {
        GameEngine.OnDayStarted += HandleDayStarted;
    }

    void OnDisable()
    {
        GameEngine.OnDayStarted -= HandleDayStarted;
    }

    private void HandleDayStarted(int dayIndex)
    {
        gameObject.SetActive(false);
        lastPressed = false;
        if (sr != null && unpressedSprite != null) sr.sprite = unpressedSprite;
    }
}
