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
    public GameTimer gameTimer;                // assign your GameTimer here (optional)

    private int index = 0;
    private bool visible = false;

    void Start()
    {
        HideAllImages();
    }

    void Update()
    {
        // Toggle / Prev on Q
        if (Input.GetKeyDown(toggleKey))
        {
            if (!visible)
                OpenPanel();   // always reset to first image and pause timer
            else
                PrevImage();
        }

        // Next on E or left click
        if (visible && (Input.GetKeyDown(nextKey) || (useLeftMouseToAdvance && Input.GetMouseButtonDown(0))))
        {
            NextImage();
        }
    }

    // Public entrypoint so Menu (or any other script) can open the info panel.
    // This always resets to first image and pauses the timer.
    public void OpenPanel()
    {
        if (images == null || images.Length == 0)
        {
            Debug.LogWarning("[InfoPanelController] No images assigned.");
            return;
        }

        visible = true;
        index = 0;                // always start at first image
        UpdateVisibleImages();
        PauseTimer();
    }

    private void ClosePanel()
    {
        visible = false;
        HideAllImages();

        // If the game hasn't started yet, start the run and do NOT resume the timer:
        if (GameEngine.Instance != null && GameEngine.CurrentDay == 0)
        {
            GameEngine.Instance.StartRun();
            return;
        }

        // Otherwise (in-game) resume the timer
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
            // Passed the last image -> close and resume/start game as appropriate
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
            // At first image and Q pressed while panel visible: do nothing (stay open).
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
            gameTimer = FindObjectOfType<GameTimer>();
            if (gameTimer == null)
            {
                Debug.LogWarning("[InfoPanelController] GameTimer not assigned; cannot pause.");
                return;
            }
        }
        gameTimer.PauseTimer();
    }

    private void ResumeTimer()
    {
        if (gameTimer == null)
        {
            gameTimer = FindObjectOfType<GameTimer>();
            if (gameTimer == null)
            {
                Debug.LogWarning("[InfoPanelController] GameTimer not assigned; cannot resume.");
                return;
            }
        }
        gameTimer.ResumeTimer();
    }
}
