using UnityEngine;
using UnityEngine.InputSystem;

public enum InputDeviceType
{
    None,
    KeyboardMouse,
    Gamepad
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public InputDeviceType selectedInputDevice = InputDeviceType.None;

    InputAction detectAnyInput;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            enabled = false;
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        //Input
        detectAnyInput = new InputAction("DetectAnyInput", InputActionType.Button);
        detectAnyInput.AddBinding("<Keyboard>/anyKey");
        detectAnyInput.AddBinding("<Pointer>/press");
        detectAnyInput.AddBinding("<Gamepad>/*");
    }

    void OnEnable()    //Input
    {
        detectAnyInput.performed += OnAnyInput;
        detectAnyInput.Enable();
    }

    void OnDisable()    //Input
    {
        if (detectAnyInput != null)
        {
            detectAnyInput.performed -= OnAnyInput;
            detectAnyInput.Disable();
        }

    }


    void OnAnyInput(InputAction.CallbackContext context)    //Input
    {
        if (selectedInputDevice != InputDeviceType.None)
            return;

        selectedInputDevice = context.control.device is Gamepad
            ? InputDeviceType.Gamepad
            : InputDeviceType.KeyboardMouse;

        detectAnyInput.Disable();
    }
}


