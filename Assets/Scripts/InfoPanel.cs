using UnityEngine;

public class InfoPanelController : MonoBehaviour
{
    [Header("UI Images (order: first -> last)")]
    public GameObject[] images;                // GameObjects to toggle (SetActive)

    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.Q;      // open panel / prev image when open
    public KeyCode nextKey = KeyCode.E;        // advance to next image
    public bool useLeftMouseToAdvance = true;

    [Header("Timer")]
    public GameTimer gameTimer;                // assign your GameTimer here

    private int index = 0;
    private bool visible = false;

    void Start()
    {
        // Hide all images initially
        HideAllImages();
    }

    void Update()
    {
        // Toggle / Prev on Q
        if (Input.GetKeyDown(toggleKey))
        {
            if (!visible)
                ShowPanel();
            else
                PrevImage();
        }

        // Next on E or left click
        if (visible && (Input.GetKeyDown(nextKey) || (useLeftMouseToAdvance && Input.GetMouseButtonDown(0))))
        {
            NextImage();
        }
    }

    // Public entrypoint so Menu (or any other script) can open the info panel
    public void OpenPanel()
    {
        ShowPanel();
    }

    private void ShowPanel()
    {
        if (images == null || images.Length == 0)
        {
            Debug.LogWarning("[InfoPanelController] No images assigned.");
            return;
        }

        visible = true;
        index = 0;                // show first image when opening
        UpdateVisibleImages();
        PauseTimer();
    }

    private void ClosePanel()
    {
        visible = false;
        HideAllImages();
        ResumeTimer();
    }

    private void NextImage()
    {
        if (images == null || images.Length == 0) return;

        index++;
        if (index < images.Length)
        {
            UpdateVisibleImages();
        }
        else
        {
            // Passed the last image -> close and resume timer
            ClosePanel();
        }
    }

    private void PrevImage()
    {
        if (images == null || images.Length == 0) return;

        if (index > 0)
        {
            index--;
            UpdateVisibleImages();
        }
        else
        {
            // At first image and Q pressed: currently do nothing.
            // If you'd prefer to close the panel here, uncomment:
            // ClosePanel();
        }
    }

    private void UpdateVisibleImages()
    {
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;
            images[i].SetActive(i == index);
        }
    }

    private void HideAllImages()
    {
        if (images == null) return;
        for (int i = 0; i < images.Length; i++)
            if (images[i] != null) images[i].SetActive(false);
    }

    private void PauseTimer()
    {
        if (gameTimer == null)
        {
            Debug.LogWarning("[InfoPanelController] GameTimer not assigned; cannot pause.");
            return;
        }
        gameTimer.PauseTimer();
    }

    private void ResumeTimer()
    {
        if (gameTimer == null)
        {
            Debug.LogWarning("[InfoPanelController] GameTimer not assigned; cannot resume.");
            return;
        }
        gameTimer.ResumeTimer();
    }
}
