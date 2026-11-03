using UnityEngine;
using UnityEngine.InputSystem;

public class Camera_Movement : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] [Range(.01f, 1)] float sensitivity;
    [SerializeField] float fov;

    [Header("References")]
    [SerializeField] Transform orientation;

    private InputAction look_InputAction;

    private Vector2 look_Input;

    float x_rot, y_rot;
   
    void Start()
    {
        look_InputAction = InputSystem.actions.FindAction("Look");

        GetComponent<Camera>().fieldOfView = fov;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        look_Input = look_InputAction.ReadValue<Vector2>();

        y_rot += look_Input.x * sensitivity;

        x_rot -= look_Input.y * sensitivity;
        x_rot = Mathf.Clamp(x_rot, -90, 90);
        
        transform.localRotation = Quaternion.Euler(x_rot, y_rot, 0);
        orientation.localRotation = Quaternion.Euler(0, y_rot, 0);
    }
}
