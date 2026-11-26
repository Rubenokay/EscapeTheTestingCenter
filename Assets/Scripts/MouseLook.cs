
using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float sensitivity = 2f;
    public Transform playerBody;

    private float verticalRotation = 0f;
    private bool gameEnded = false;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (gameEnded)
            return;

        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);

        transform.localRotation =
            Quaternion.Euler(verticalRotation, 0f, 0f);

        if (playerBody != null)
            playerBody.Rotate(Vector3.up * mouseX);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void EndGame()
    {
        gameEnded = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
