
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public GameObject gameOverPanel;
    public MouseLook mouseLook;
    public PlayerMovement playerMovement;

    private bool gameEnded = false;

    void Start()
    {
        Time.timeScale = 1f;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
    void Update()
{
    // Temporary test for the Game Over screen
    if (Input.GetKeyDown(KeyCode.G))
    {
        Debug.Log("G KEY PRESSED!");
        GameOver();
    }
}

    public void GameOver()
    {
        if (gameEnded)
            return;

        gameEnded = true;

        Debug.Log("YOU WERE CAUGHT!");

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (mouseLook != null)
            mouseLook.EndGame();

        if (playerMovement != null)
            playerMovement.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }
}
