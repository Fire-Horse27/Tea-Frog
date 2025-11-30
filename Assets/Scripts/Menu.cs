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
        // Start game normally
        timer.TimerStart();

        // Immediately open the info panel after starting
        if (infoPanelController != null)
        {
            infoPanelController.OpenPanel();
        }
        else
        {
            Debug.LogWarning("[Menu] No InfoPanelController assigned!");
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
