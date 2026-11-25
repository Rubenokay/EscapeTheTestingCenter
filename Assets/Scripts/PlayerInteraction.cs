
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

    void Start()
    {
        // Hide victory screen when game starts
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        // Set initial objective
        if (objectiveText != null)
        {
            objectiveText.text = "Objective: Find the keycard";
        }
    }

    void Update()
    {
        // Clear interaction prompt
        if (interactionPrompt != null)
        {
            interactionPrompt.text = "";
        }

        // Cast a ray from the camera
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            if (!hit.collider.CompareTag("Interactable"))
                return;

            GameObject target = hit.collider.gameObject;

            // Keycard interaction
            if (target.name == "Keycard")
            {
                if (interactionPrompt != null)
                {
                    interactionPrompt.text = "Press E to pick up keycard";
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

            // Exit door interaction
            else if (target.name == "ExitDoor")
            {
                if (interactionPrompt != null)
                {
                    interactionPrompt.text = hasKeycard
                        ? "Press E to unlock exit"
                        : "Press E to check door";
                }

                if (Input.GetKeyDown(KeyCode.E))
                {
                    if (hasKeycard)
                    {
                        Debug.Log("Door unlocked! You escaped!");

                        // Display victory screen
                        if (victoryPanel != null)
                        {
                            victoryPanel.SetActive(true);
                        }

                        // Update objective
                        if (objectiveText != null)
                        {
                            objectiveText.text = "Objective Complete!";
                        }

                        // Remove exit door
                        Destroy(target);
                    }
                    else
                    {
                        Debug.Log(
                            "Door is locked! Find the keycard."
                        );
                    }
                }
            }
        }
    }
}

