using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public GameTimer timer;
    public GameObject titlePanel;

    // Reference to the InfoPanelController (drag into inspector)
    public InfoPanelController infoPanelController;

    void Start()
    {
        titlePanel.SetActive(true);
    }

    public void StartGame()
    {
        // Open the info panels; the InfoPanelController will start the GameEngine when the player finishes.
        if (infoPanelController != null)
        {
            infoPanelController.OpenPanel();
            titlePanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[Menu] No InfoPanelController assigned. Starting game directly.");
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void TitleScreen()
    {
        timer.ToTitle();
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}
