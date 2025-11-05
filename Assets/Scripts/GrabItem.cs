using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GrabItem : MonoBehaviour
{
    public string itemID;
    public float promptOffsetY = 1f;
    public static Transform Button;
    public static Transform playerTransform;

    Collider2D col;
    HeldTea heldTea;
    bool showingPrompt;

    void Awake()
    {
        col = GetComponent<Collider2D>();

        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (Button == null)
        {
            var buttonObj = GameObject.Find("EButton");
            if (buttonObj != null) Button = buttonObj.transform;
        }

        if (playerTransform != null)
            heldTea = playerTransform.GetComponentInChildren<HeldTea>();
    }

    void Update()
    {
        if (col.OverlapPoint(playerTransform.position))
        {
            if (!showingPrompt)
            {
                showingPrompt = true;
                Button.gameObject.SetActive(true);
                Button.position = new Vector3(transform.position.x, transform.position.y + promptOffsetY, transform.position.z);
            }

            if (Input.GetKeyDown(KeyCode.E) && heldTea != null)
            {
                heldTea.SetHeld(itemID);
                if (itemID == "Hot" && heldTea.cupFilled == "")
                {
                    var kettle = GetComponent<KettleFuntion>();
                    if (kettle != null) kettle.NextSprite();
                }
            }
        }
        else if (showingPrompt)
        {
            showingPrompt = false;
            Button.gameObject.SetActive(false);
        }
    }
}
