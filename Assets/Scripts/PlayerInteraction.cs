
using UnityEngine;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionDistance = 4f;
    public bool hasKeycard = false;

    // UI elements
    public TMP_Text interactionPrompt;
    public TMP_Text objectiveText;
    public GameObject victoryPanel;

    // Temporary locked-door message
    private float messageEndTime = 0f;

    // Prevent interactions after winning
    private bool gameWon = false;

    void Start()
    {
        // Hide victory screen when the game starts
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        // Set starting objective
        if (objectiveText != null)
        {
            objectiveText.text = "Objective: Find the keycard";
        }
    }

    void Update()
    {
        // Stop interactions after winning
        if (gameWon)
            return;

        // Clear prompt unless a temporary message is active
        if (interactionPrompt != null && Time.time >= messageEndTime)
        {
            interactionPrompt.text = "";
        }

        // Raycast from the camera
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            if (!hit.collider.CompareTag("Interactable"))
                return;

            GameObject target = hit.collider.gameObject;

            // KEYCARD INTERACTION
            if (target.name == "Keycard")
            {
                if (interactionPrompt != null &&
                    Time.time >= messageEndTime)
                {
                    interactionPrompt.text =
                        "Press E to pick up keycard";
                }

                if (Input.GetKeyDown(KeyCode.E))
                {
                    hasKeycard = true;

                    if (objectiveText != null)
                    {
                        objectiveText.text =
                            "Objective: Reach the exit door";
                    }

                    Debug.Log("Keycard collected!");
                    Destroy(target);
                }
            }

            // EXIT DOOR INTERACTION
            else if (target.name == "ExitDoor")
            {
                if (interactionPrompt != null &&
                    Time.time >= messageEndTime)
                {
                    interactionPrompt.text = hasKeycard
                        ? "Press E to unlock exit"
                        : "Press E to check door";
                }

                if (Input.GetKeyDown(KeyCode.E))
                {
                    if (hasKeycard)
                    {
                        // Mark the game as won
                        gameWon = true;

                        Debug.Log("Door unlocked! You escaped!");

                        // Update objective
                        if (objectiveText != null)
                        {
                            objectiveText.text = "Objective Complete!";
                        }

                        // Clear interaction prompt
                        if (interactionPrompt != null)
                        {
                            interactionPrompt.text = "";
                        }

                        // Show victory screen
                        if (victoryPanel != null)
                        {
                            victoryPanel.SetActive(true);
                        }

                        // Stop camera rotation
                        MouseLook mouseLook =
                            GetComponent<MouseLook>();

                        if (mouseLook != null)
                        {
                            mouseLook.EndGame();
                        }

                        // Unlock cursor for Restart button
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;

                        // Remove exit door
                        Destroy(target);
                    }
                    else
                    {
                        Debug.Log(
                            "Door is locked! Find the keycard."
                        );

                        if (interactionPrompt != null)
                        {
                            interactionPrompt.text =
                                "DOOR LOCKED! Find the keycard.";

                            messageEndTime = Time.time + 2f;
                        }
                    }
                }
            }
        }
    }
}
