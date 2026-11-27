
using System.Collections;
using TMPro;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public TMP_Text dialogueText;
    public float typingSpeed = 0.05f;
    public float displayDuration = 2f;

    void Start()
    {
        StartCoroutine(TypeDialogue(
            "Ugh... this exam is sooo boring."
        ));
    }

    IEnumerator TypeDialogue(string message)
    {
        dialogueText.text = "";

        // Type each letter one at a time
        foreach (char letter in message)
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        // Keep the completed sentence visible
        yield return new WaitForSeconds(displayDuration);

        // Hide the dialogue
        dialogueText.text = "";
    }
}
