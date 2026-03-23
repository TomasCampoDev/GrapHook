using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

public enum InputDeviceType
{
    None,
    KeyboardMouse,
    Gamepad
}

public class InputManager : MonoBehaviour
{

    public InputDeviceType selectedInputDevice = InputDeviceType.None;

    private InputAction detectAnyInput;

    public static InputManager Instance { get; private set; }
    [SerializeField] public bool displayMobileControls;


    // ?? Input Actions ??????????????????????????????????????????????????????????
    InputActionAsset inputActions;

    InputAction moveAction;
    public Vector2 movementInput;
    public float horizontalInput;
    public float verticalInput;

    InputAction cameraAction;
    public Vector2 cameraInput;
    public float cameraInputX;
    public float cameraInputY;

    InputAction startAction;
    public bool startButtonInput;
    public bool startButtonInputFlag;

    InputAction jumpAction;
    public bool jumpButtonInput;
    public bool jumpButtonInputFlag;

    InputAction actionAction;
    public bool actionButtonInput;
    public bool actionButtonInputFlag;

    InputAction shoulderSwapAction;
    public bool shoulderSwapInput;

    // ?? UI Persistente ????????????????????????????????????????????????????????
    [Header("Prefabs de UI móvil (deja vacío para cargar desde Resources/UI/)")]
    [Tooltip("Prefab del joystick de movimiento")]
    [SerializeField] private GameObject moveJoystickPrefab;

    [Tooltip("Prefab del joystick de cámara")]
    [SerializeField] private GameObject cameraJoystickPrefab;

    [Tooltip("Prefab del botón de salto")]
    [SerializeField] private GameObject jumpButtonPrefab;

    [Tooltip("Prefab del botón de caída")]
    [SerializeField] private GameObject fallButtonPrefab;

    [Header("Configuración del Canvas persistente")]
    [Tooltip("Debe coincidir EXACTAMENTE con la resolución de referencia del Canvas donde diseñaste los prefabs")]
    [SerializeField] private Vector2 canvasReferenceResolution = new Vector2(1080f, 1920f);

    [Tooltip("0 = escala por ancho, 1 = escala por alto, 0.5 = mezcla. Igual que en tu Canvas original")]
    [Range(0f, 1f)]
    [SerializeField] private float canvasMatchWidthOrHeight = 0.5f;

    // Canvas raíz de la UI persistente (accesible por si otros scripts lo necesitan)
    public Canvas UICanvas { get; private set; }

    // ???????????????????????????????????????????????????????????????????????????
    // Awake – singleton, input y UI, todo de una vez
    // ???????????????????????????????????????????????????????????????????????????

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

        // Input
        inputActions = Resources.Load<InputActionAsset>("Input/PlayerControls");
        moveAction = inputActions.FindAction("Move");
        cameraAction = inputActions.FindAction("Camera");
        startAction = inputActions.FindAction("Start");
        jumpAction = inputActions.FindAction("Jump");
        actionAction = inputActions.FindAction("Action");
        shoulderSwapAction = inputActions.FindAction("ShoulderSwap");

        detectAnyInput = new InputAction("DetectAnyInput", InputActionType.Button);
        detectAnyInput.AddBinding("<Keyboard>/anyKey");
        detectAnyInput.AddBinding("<Pointer>/press");
        detectAnyInput.AddBinding("<Gamepad>/*");

        // UI persistente
        if (!displayMobileControls)
            return;
        CreatePersistentUI();
    }

    // ???????????????????????????????????????????????????????????????????????????
    // Creación de la UI
    // ???????????????????????????????????????????????????????????????????????????

    /// <summary>
    /// Crea un Canvas independiente y persistente con todos los controles táctiles.
    /// Se llama una sola vez en Awake; nunca se destruirá al cambiar de escena.
    /// </summary>
    private void CreatePersistentUI()
    {

        // ?? Canvas ????????????????????????????????????????????????????????????
        GameObject canvasGO = new GameObject("InputUI_Persistent");
        UICanvas = canvasGO.AddComponent<Canvas>();
        UICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UICanvas.sortingOrder = 10; // por encima de la UI de escena, debajo del score (99)

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = canvasReferenceResolution;
        scaler.matchWidthOrHeight = canvasMatchWidthOrHeight;

        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // ?? Prefabs ???????????????????????????????????????????????????????????
        // Si no se han asignado en el Inspector, intenta cargarlos desde Resources
        if (moveJoystickPrefab == null) moveJoystickPrefab = Resources.Load<GameObject>("UI/MoveJoystick");
        if (cameraJoystickPrefab == null) cameraJoystickPrefab = Resources.Load<GameObject>("UI/CameraJoystick");
        if (jumpButtonPrefab == null) jumpButtonPrefab = Resources.Load<GameObject>("UI/JumpButton");
        if (fallButtonPrefab == null) fallButtonPrefab = Resources.Load<GameObject>("UI/FallButton");

        // ?? Instancia ?????????????????????????????????????????????????????????
        InstantiateUIElement(moveJoystickPrefab, "MoveJoystick", canvasGO.transform);
        InstantiateUIElement(cameraJoystickPrefab, "CameraJoystick", canvasGO.transform);
        InstantiateUIElement(jumpButtonPrefab, "JumpButton", canvasGO.transform);
        InstantiateUIElement(fallButtonPrefab, "FallButton", canvasGO.transform);
    }

    /// <summary>
    /// Instancia un prefab de UI como hijo del canvas persistente
    /// preservando exactamente el RectTransform definido en el prefab.
    /// Si el prefab es null lanza un aviso pero no rompe nada.
    /// </summary>
    private GameObject InstantiateUIElement(GameObject prefab, string elementName, Transform parent)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[InputManager] Prefab '{elementName}' no encontrado. " +
                             $"Asígnalo en el Inspector o colócalo en Resources/UI/{elementName}.");
            return null;
        }

        // Leemos los valores del RectTransform del prefab ANTES de instanciar
        RectTransform prefabRT = prefab.GetComponent<RectTransform>();
        Vector2 anchorMin = new Vector2(0.5f, 0.5f);
        Vector2 anchorMax = new Vector2(0.5f, 0.5f);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        Vector2 anchoredPosition = Vector2.zero;
        Vector2 sizeDelta = new Vector2(200f, 200f);

        if (prefabRT != null)
        {
            anchorMin = prefabRT.anchorMin;
            anchorMax = prefabRT.anchorMax;
            pivot = prefabRT.pivot;
            anchoredPosition = prefabRT.anchoredPosition;
            sizeDelta = prefabRT.sizeDelta;
        }

        // Instanciamos SIN padre primero para evitar que Unity altere el transform
        GameObject instance = Instantiate(prefab);
        instance.name = elementName;

        // Metemos en el canvas con worldPositionStays = false
        instance.transform.SetParent(parent, false);

        // Reaplicamos explícitamente los valores originales del prefab
        RectTransform rt = instance.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPosition;
        }

        return instance;
    }

    // ???????????????????????????????????????????????????????????????????????????
    // OnEnable / OnDisable
    // ???????????????????????????????????????????????????????????????????????????

    void OnEnable()
    {
        
        inputActions.Enable();
        moveAction.Enable();
        cameraAction.Enable();
        startAction.Enable();
        jumpAction.Enable();
        actionAction.Enable();
        shoulderSwapAction.Enable();
        // Detectar dispositivo activo
        detectAnyInput = new InputAction("DetectAnyInput", InputActionType.Button);
        detectAnyInput.AddBinding("<Keyboard>/anyKey");
        detectAnyInput.AddBinding("<Pointer>/press");
        detectAnyInput.AddBinding("<Gamepad>/*");
        if (detectAnyInput != null)
            {
                detectAnyInput.performed += OnAnyInput;
                detectAnyInput.Enable();
            }
        

    }

    void OnDisable()
    {
        if (detectAnyInput != null)
        {
            detectAnyInput.performed -= OnAnyInput;
            detectAnyInput.Disable();
        }

        if (moveAction != null) moveAction.Disable();
        if (cameraAction != null) cameraAction.Disable();
        if (startAction != null) startAction.Disable();
        if (jumpAction != null) jumpAction.Disable();
        if (actionAction != null) actionAction.Disable();
        if (shoulderSwapAction != null) shoulderSwapAction.Disable();
        if (inputActions != null) inputActions.Disable();

    }

    // ???????????????????????????????????????????????????????????????????????????
    // Update
    // ???????????????????????????????????????????????????????????????????????????
    private void OnAnyInput(InputAction.CallbackContext context)
    {
        InputDeviceType newDevice = context.control.device is Gamepad
            ? InputDeviceType.Gamepad
            : InputDeviceType.KeyboardMouse;

        if (selectedInputDevice == newDevice)
            return;

        selectedInputDevice = newDevice;
        Debug.Log("Input device changed to: " + selectedInputDevice);
    }

    void Update()
    {
        ProcessInputs();
    }

    void ProcessInputs()
    {
        if (selectedInputDevice == InputDeviceType.None)
            return;

        // Move
        movementInput = moveAction.ReadValue<Vector2>();
        horizontalInput = movementInput.x;
        verticalInput = movementInput.y;

        // Camera
        cameraInput = cameraAction.ReadValue<Vector2>();
        cameraInputX = cameraInput.x;
        cameraInputY = cameraInput.y;

        // Start
        if (startAction.WasPressedThisFrame()) { startButtonInput = true; startButtonInputFlag = true; }
        if (startAction.WasReleasedThisFrame()) { startButtonInput = false; startButtonInputFlag = false; }

        // Jump
        if (jumpAction.WasPressedThisFrame()) { jumpButtonInput = true; jumpButtonInputFlag = true; }
        if (jumpAction.WasReleasedThisFrame()) { jumpButtonInput = false; jumpButtonInputFlag = false; }

        // Action  (nota: el original usaba jumpAction.WasReleased para soltar — corregido a actionAction)
        if (actionAction.WasPressedThisFrame()) { actionButtonInput = true; actionButtonInputFlag = true; }
        if (actionAction.WasReleasedThisFrame()) { actionButtonInput = false; actionButtonInputFlag = false; }

        // Shoulder
        if (shoulderSwapAction.WasPressedThisFrame()) shoulderSwapInput = true;
        else shoulderSwapInput = false;
    }
}