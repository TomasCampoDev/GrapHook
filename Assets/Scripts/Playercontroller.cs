using System.Collections;
using UnityEngine;

/// <summary>
/// Orquestador principal del personaje.
/// Responsabilidades: movimiento en suelo, rotación, cámara de hombro y estado compartido (IPlayerContext).
/// La física/salto vive en PlayerPhysicsController.
/// El ledge vive en PlayerLedgeGrabController.
/// El grappling hook vive en GrapplingHookController.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerAnimatorBridge))]
public class PlayerController : MonoBehaviour, IPlayerContext
{
    #region Serialized — Character

    [Header("Character Identity")]
    [SerializeField] private string characterName;

    #endregion

    #region Serialized — Movement

    [Header("Movement")]
    [SerializeField] private float baseMoveSpeed = 6f;
    [SerializeField] private float jumpMoveSpeedMultiplier = 1.5f;
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float animationBlendRate = 10f;

    #endregion

    #region Serialized — Camera Base

    [Header("Camera — Base")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float cameraSensitivity = 100f;
    [SerializeField] private float cameraFollowSpeed = 7f;
    [SerializeField] private float cameraRotationSpeed = 7f;
    [SerializeField] private float cameraHeightOffset = 2.8f;
    [SerializeField] private float cameraDistance = 5f;
    [SerializeField] private float minPitch = -19f;
    [SerializeField] private float maxPitch = 10f;

    #endregion

    #region Serialized — Camera Shoulder

    [Header("Camera — Shoulder")]
    [Tooltip("Offset lateral del hombro activo. Positivo = derecha, negativo = izquierda.")]
    [SerializeField] private float shoulderOffset = 0.6f;

    #endregion

    #region Serialized — Camera Aim

    [Header("Camera — Aim")]
    [SerializeField] private float aimCameraDistance = 2.5f;
    [SerializeField] private float aimHeightOffset = 1.8f;
    [SerializeField] private float aimTransitionSpeed = 8f;

    #endregion

    #region Serialized — Ground Check

    [Header("Ground Check")]
    [SerializeField] private float groundedRadius = 0.2f;
    [SerializeField] private float groundedOffset = 0f;
    [SerializeField] private LayerMask groundLayers;
    private const float GROUND_RAYCAST_EXTRA = 0.3f;

    #endregion

    #region Serialized — Dissolve

    [Header("Dissolve")]
    public float dissolveDuration = 0.5f;

    #endregion

    #region Private — Components

    private CharacterController _characterController;
    private PlayerAnimatorBridge _animatorBridge;
    private InputManager _input;
    private GrapplingHookController _hookController;

    #endregion

    #region Private — Movement State

    private float _currentSpeed;
    private float _movementAnimBlend;
    private float _currentRotationAngle;
    private float _rotationVelocity;
    private float _moveSpeed;
    private bool _movementBlocked;
    private bool _rotationBlocked;

    #endregion

    #region Private — Camera State

    private float _cameraPitch;
    private float _cameraYaw;
    private float _currentCameraDistance;
    private float _currentShoulderOffset;
    private float _currentHeightOffset;

    #endregion

    #region Private — Shared State

    private float _verticalVelocity;
    private bool _isAiming;
    private bool _isOnLedge;
    private bool _isHookActive;
    private Vector3 _receivedInertia;
    private bool _isGrounded;
    private Transform _currentGround;
    private bool _canCheckGrounded = true;

    #endregion

    #region Serialized — Swing Input Suppression Recovery

    [Header("Swing Input Suppression — Recovery")]
    [Tooltip("Si está activo, la supresión de input se cancela en cuanto el personaje toca el suelo, " +
             "aunque el jugador siga pulsando. Ideal para que el aterrizaje se sienta natural.")]
    [SerializeField] private bool recoverSuppressionOnGrounded = true;

    [Tooltip("Si está activo, la supresión de input se cancela tras el tiempo indicado, " +
             "aunque el jugador no haya soltado el eje. Evita que la supresión dure indefinidamente.")]
    [SerializeField] private bool recoverSuppressionAfterTimeout = true;

    [Tooltip("Tiempo en segundos hasta que la supresión de input se cancela automáticamente.")]
    [SerializeField] private float suppressionTimeoutDuration = 0.5f;

    #endregion

    #region Private — Swing Input Suppression

    // Input raw capturado en el momento de soltar el gancho, por eje.
    // Mientras el jugador siga pulsando ese mismo eje con el mismo signo,
    // el movement input se trata como cero para no cancelar la inercia del swing.
    // Se limpia automáticamente cuando el jugador suelta los ejes implicados,
    // o al cumplirse las condiciones de recuperación configuradas más abajo.
    private Vector2 _suppressedSwingInput;
    private bool _suppressSwingDown;
    private bool _suppressSwingUp;
    private bool _suppressSwingLateral;
    private float _suppressionTimer;

    #endregion

    #region Constants

    private const float INPUT_THRESHOLD = 0.01f;
    private const float GROUNDED_CHECK_DELAY = 0.2f;
    private const string ENVIRONMENT_TAG = "Environment";

    #endregion

    #region Initialization

    public Vector3 StartPosition { get; private set; }
    public Quaternion StartRotation { get; private set; }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _animatorBridge = GetComponent<PlayerAnimatorBridge>();
        _input = InputManager.Instance;
        _hookController = GetComponent<GrapplingHookController>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        _moveSpeed = baseMoveSpeed;
        _currentCameraDistance = cameraDistance;
        _currentShoulderOffset = 0f;
        _currentHeightOffset = cameraHeightOffset;
        groundLayers = LayerMask.GetMask("Environment");

        StartPosition = transform.position;
        StartRotation = transform.rotation;

        Cursor.lockState = CursorLockMode.Locked;
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (!_isOnLedge)
            HandleGroundMovement();

        ApplyVerticalVelocity();
        ApplyInertia();
    }

    private void LateUpdate()
    {
        CheckGroundedStatus();
        HandleCameraControl();
    }

    #endregion

    #region Ground Movement

    private void HandleGroundMovement()
    {
        if (_movementBlocked || _isHookActive)
            return;

        Vector2 input = GetMovementInputWithSwingSuppression();
        float inputMagnitude = input.magnitude;
        _currentSpeed = _moveSpeed * inputMagnitude;

        UpdateMovementAnimation();

        Vector3 inputDirection = new Vector3(input.x, 0f, input.y).normalized;

        if (inputDirection != Vector3.zero && !_rotationBlocked)
            RotateCharacter(inputDirection);

        MoveCharacter(GetMovementDirection(inputDirection));
    }

    /// Devuelve el input de movimiento filtrado por la supresión de swing.
    /// Si el jugador sigue pulsando el eje que tenía al soltarse del gancho,
    /// ese eje se devuelve como 0 para no cancelar la inercia.
    /// La supresión se limpia al soltar el eje, al tocar suelo (si está activado)
    /// o al superar el timeout configurado (si está activado).
    private Vector2 GetMovementInputWithSwingSuppression()
    {
        if (!HasActiveSwingSuppression())
            return _input.movementInput;

        Vector2 raw = _input.movementInput;

        TickSuppressionTimer();
        ClearSwingSuppressionIfRecoveryConditionMet(raw);

        if (!HasActiveSwingSuppression())
            return raw;

        return BuildSuppressedInput(raw);
    }

    private bool HasActiveSwingSuppression()
        => _suppressSwingDown || _suppressSwingUp || _suppressSwingLateral;

    /// Avanza el timer de supresión y limpia todo si se supera el timeout configurado.
    private void TickSuppressionTimer()
    {
        if (!recoverSuppressionAfterTimeout)
            return;

        _suppressionTimer += Time.deltaTime;

        if (_suppressionTimer >= suppressionTimeoutDuration)
            ClearAllSwingSuppression();
    }

    /// Evalúa todas las condiciones de recuperación y limpia las flags que correspondan.
    private void ClearSwingSuppressionIfRecoveryConditionMet(Vector2 currentInput)
    {
        if (recoverSuppressionOnGrounded && _isGrounded)
        {
            ClearAllSwingSuppression();
            return;
        }

        ClearSwingSuppressionIfInputReleased(currentInput);
    }

    /// Limpia cada flag de supresión en cuanto el jugador suelta el eje correspondiente.
    /// Para teclado: el eje baja a 0. Para stick analógico: el signo cambia o baja de threshold.
    private void ClearSwingSuppressionIfInputReleased(Vector2 currentInput)
    {
        bool verticalReleased = Mathf.Abs(currentInput.y) < INPUT_THRESHOLD;
        bool lateralReleased = Mathf.Abs(currentInput.x) < INPUT_THRESHOLD;

        bool verticalSignChanged = currentInput.y * _suppressedSwingInput.y < 0f;
        bool lateralSignChanged = currentInput.x * _suppressedSwingInput.x < 0f;

        if (_suppressSwingDown && (verticalReleased || verticalSignChanged)) _suppressSwingDown = false;
        if (_suppressSwingUp && (verticalReleased || verticalSignChanged)) _suppressSwingUp = false;
        if (_suppressSwingLateral && (lateralReleased || lateralSignChanged)) _suppressSwingLateral = false;
    }

    /// Cancela toda la supresión de swing de golpe.
    /// Usado por las condiciones de recuperación (grounded, timeout).
    private void ClearAllSwingSuppression()
    {
        _suppressSwingDown = false;
        _suppressSwingUp = false;
        _suppressSwingLateral = false;
        _suppressedSwingInput = Vector2.zero;
        _suppressionTimer = 0f;
    }

    /// Construye el input final aplicando las supresiones activas eje a eje.
    private Vector2 BuildSuppressedInput(Vector2 raw)
    {
        float x = _suppressSwingLateral ? 0f : raw.x;
        float y = (_suppressSwingDown || _suppressSwingUp) ? 0f : raw.y;
        return new Vector2(x, y);
    }

    private void UpdateMovementAnimation()
    {
        _movementAnimBlend = Mathf.Lerp(_movementAnimBlend, _currentSpeed, Time.deltaTime * animationBlendRate);
        _animatorBridge.SetMoveAmount(_movementBlocked ? 0f : _movementAnimBlend);
    }

    private void RotateCharacter(Vector3 inputDirection)
    {
        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg
                          + mainCamera.transform.eulerAngles.y;

        float smoothAngle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref _rotationVelocity,
            rotationSmoothTime
        );

        _currentRotationAngle = targetAngle;
        transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
    }

    /// Fuerza la rotación hacia un ángulo Y con lerp suave.
    /// Usado por GrapplingHookController al disparar.
    public void RotateTowardsAngle(float targetYAngle)
    {
        float smoothAngle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetYAngle,
            ref _rotationVelocity,
            rotationSmoothTime
        );

        _currentRotationAngle = targetYAngle;
        transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
    }

    private Vector3 GetMovementDirection(Vector3 inputDirection)
    {
        if (inputDirection == Vector3.zero)
            return Vector3.zero;

        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg
                          + mainCamera.transform.eulerAngles.y;

        return Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
    }

    private void MoveCharacter(Vector3 direction)
    {
        _characterController.Move(direction * _currentSpeed * Time.deltaTime);
    }

    #endregion

    #region Vertical Velocity & Inertia

    private void ApplyVerticalVelocity()
    {
        if (_isOnLedge || _isHookActive)
            return;

        _characterController.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
    }

    private void ApplyInertia()
    {
        if (_isHookActive || _isGrounded)
        {
            _receivedInertia = Vector3.zero;
            return;
        }

        if (_receivedInertia.magnitude < 0.01f)
            return;

        _characterController.Move(_receivedInertia * Time.deltaTime);
        _receivedInertia = Vector3.Lerp(_receivedInertia, Vector3.zero, 3f * Time.deltaTime);
    }

    #endregion

    #region Ground Check

    private void CheckGroundedStatus()
    {
        if (!_canCheckGrounded)
            return;

        Vector3 spherePos = new Vector3(transform.position.x,
                                        transform.position.y - groundedOffset,
                                        transform.position.z);

        Collider[] hits = Physics.OverlapSphere(spherePos, groundedRadius, ~0, QueryTriggerInteraction.Ignore);

        _isGrounded = false;
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag(ENVIRONMENT_TAG))
            {
                _isGrounded = true;
                _currentGround = hit.transform;
                break;
            }
        }

        if (_isGrounded)
            UpdateGroundParent();
        else
            ClearGroundParent();

        UpdateAirborneAnimations();
    }

    private void UpdateGroundParent()
    {
        Ray ray = new Ray(transform.position, Vector3.down);
        float dist = groundedOffset + GROUND_RAYCAST_EXTRA;

        if (Physics.Raycast(ray, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag(ENVIRONMENT_TAG))
            {
                _currentGround = hit.transform;
                transform.SetParent(_currentGround);
                NotifyPlatformColorChange(_currentGround);
            }
        }
    }

    private void ClearGroundParent()
    {
        _isGrounded = false;
        _currentGround = null;
        transform.SetParent(null);
    }

    private void UpdateAirborneAnimations()
    {
        if (!_isGrounded)
        {
            _moveSpeed = baseMoveSpeed * jumpMoveSpeedMultiplier;
            _animatorBridge.SetGrounded(false);
            _animatorBridge.SetFreeFall(true);
        }
        else
        {
            _moveSpeed = baseMoveSpeed;
            _animatorBridge.SetGrounded(true);
            _animatorBridge.SetFreeFall(false);
            _animatorBridge.SetJump(false);
        }
    }

    public void TemporarilyDisableGroundCheck(float duration)
    {
        StartCoroutine(GroundCheckDelayCoroutine(duration));
    }

    private IEnumerator GroundCheckDelayCoroutine(float duration)
    {
        _canCheckGrounded = false;
        _currentGround = null;
        yield return new WaitForSeconds(duration);
        _canCheckGrounded = true;
    }

    #endregion

    #region Camera

    private void HandleCameraControl()
    {
        ReadAimInput();
        UpdateCameraAngles();
        UpdateAimTransition();
        UpdateCameraPosition();
    }

    private void ReadAimInput()
    {
        _isAiming = Input.GetMouseButton(1);

        if (_input.shoulderSwapInput)
            shoulderOffset = -shoulderOffset;
    }

    private void UpdateCameraAngles()
    {
        Vector2 cameraInput = _input.cameraInput;

        _cameraYaw += cameraInput.x * cameraSensitivity * Time.deltaTime;
        _cameraPitch -= cameraInput.y * cameraSensitivity * Time.deltaTime;
        _cameraPitch = Mathf.Clamp(_cameraPitch, minPitch, maxPitch);
    }

    private void UpdateAimTransition()
    {
        float targetDistance = _isAiming ? aimCameraDistance : cameraDistance;
        float targetShoulder = _isAiming ? shoulderOffset : 0f;
        float targetHeight = _isAiming ? aimHeightOffset : cameraHeightOffset;

        float t = Time.deltaTime * aimTransitionSpeed;
        _currentCameraDistance = Mathf.Lerp(_currentCameraDistance, targetDistance, t);
        _currentShoulderOffset = Mathf.Lerp(_currentShoulderOffset, targetShoulder, t);
        _currentHeightOffset = Mathf.Lerp(_currentHeightOffset, targetHeight, t);
    }

    private void UpdateCameraPosition()
    {
        if (mainCamera == null)
        {
            Debug.LogError("[PlayerController] mainCamera is null. Destroying player.", this);
            Destroy(gameObject);
            return;
        }

        Quaternion cameraRotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
        Vector3 cameraRight = cameraRotation * Vector3.right;
        Vector3 cameraForward = cameraRotation * Vector3.forward;

        // La cámara se desplaza lateralmente
        Vector3 lateralOffset = cameraRight * _currentShoulderOffset;

        // El lookTarget se desplaza en dirección OPUESTA para que el personaje
        // quede al lado en pantalla en lugar de al centro
        Vector3 lookTarget = transform.position
                           + Vector3.up * _currentHeightOffset
                           - lateralOffset;

        Vector3 desiredPos = lookTarget
                           - cameraForward * _currentCameraDistance
                           + lateralOffset;

        bool isTransitioning = Mathf.Abs(_currentCameraDistance - (_isAiming ? aimCameraDistance : cameraDistance)) > 0.01f
                            || Mathf.Abs(_currentShoulderOffset - (_isAiming ? shoulderOffset : 0f)) > 0.01f
                            || Mathf.Abs(_currentHeightOffset - (_isAiming ? aimHeightOffset : cameraHeightOffset)) > 0.01f;

        mainCamera.transform.position = isTransitioning
            ? Vector3.Lerp(mainCamera.transform.position, desiredPos, Time.deltaTime * cameraFollowSpeed)
            : desiredPos;

        mainCamera.transform.rotation = Quaternion.LookRotation(lookTarget - mainCamera.transform.position);
    }

    #endregion

    #region Platform Utilities

    private void NotifyPlatformColorChange(Transform platform)
    {
        PlatformColorChange colorChange = platform.GetComponent<PlatformColorChange>();
        colorChange?.ChangeColorOfPlatformFromPlayerSignal();
    }

    #endregion

    #region IPlayerContext — Implementation

    public Transform PlayerTransform => transform;
    public CharacterController CharacterController => _characterController;
    public float VerticalVelocity => _verticalVelocity;
    public bool IsGrounded => _isGrounded;
    public bool IsOnLedge => _isOnLedge;
    public bool IsHookActive => _isHookActive;
    public bool IsAiming => _isAiming;
    public Camera MainCamera => mainCamera;
    public PlayerAnimatorBridge Animator => _animatorBridge;

    public Vector3 EyePosition => transform.position + Vector3.up * cameraHeightOffset;
    public Vector3 CameraForward => mainCamera != null ? mainCamera.transform.forward : transform.forward;

    public void SetGrounded(bool grounded, Transform ground)
    {
        _isGrounded = grounded;
        _currentGround = ground;
    }

    public void SetOnLedge(bool onLedge) => _isOnLedge = onLedge;
    public void SetVerticalVelocity(float velocity) => _verticalVelocity = velocity;
    public void SetMovementBlocked(bool blocked) => _movementBlocked = blocked;
    public void SetRotationBlocked(bool blocked) => _rotationBlocked = blocked;
    public void SetHookActive(bool active) => _isHookActive = active;
    public void SetReceivedInertia(Vector3 inertia) => _receivedInertia = inertia;

    public void CaptureSwingInputSnapshot(Vector2 rawInput, bool wasSwingingDown, bool wasSwingingUp, bool wasSwingingLateral)
    {
        _suppressedSwingInput = rawInput;
        _suppressSwingDown = wasSwingingDown;
        _suppressSwingUp = wasSwingingUp;
        _suppressSwingLateral = wasSwingingLateral;
        _suppressionTimer = 0f;
    }
    public void ForceUngrounded()
    {
        _isGrounded = false;
        _currentGround = null;
        transform.SetParent(null);
    }
    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 spherePos = new Vector3(transform.position.x,
                                        transform.position.y - groundedOffset,
                                        transform.position.z);
        Gizmos.DrawWireSphere(spherePos, groundedRadius);
    }

    #endregion
}