using UnityEngine;

/// <summary>
/// Sistema del grappling hook.
/// Dos modos de arco simétricos (lateral y vertical), ambos basados en
/// fuerza tangencial + reproyección sobre la esfera del cable.
/// La retracción actúa siempre pero se reduce mientras hay input de arco.
/// </summary>
public enum HookReleaseMode
{
    Toggle,      // Pulsar dispara; volver a pulsar suelta.
    HoldToKeep   // Pulsar dispara; soltar el botón suelta.
}

public class GrapplingHookController : MonoBehaviour
{
    #region Serialized — References

    [Header("References")]
    public GrapplingHookVisualizer visualizer;

    [Header("Ray Origin")]
    [Tooltip("Offset relativo al EyePosition. Ajusta Y para bajar el origen del rayo.")]
    public Vector3 rayOriginOffset = Vector3.zero;
    [Tooltip("Opcional. Cuando tengas la pistola, asigna su transform aquí.")]
    public Transform rayOriginOverride;

    #endregion

    #region Serialized — Aim

    [Header("Aim Raycast")]
    public float aimMaxDistance = 30f;
    public LayerMask environmentLayers = ~0;
    public string environmentTag = "Environment";

    #endregion

    #region Serialized — Retraction

    [Header("Retraction")]
    public float retractionSpeed = 8f;
    [Tooltip("Fracción del gap que se cubre por segundo al cambiar velocidad de retracción (0-1). " +
             "0.95 = cubre el 95% del recorrido en 1 segundo. Frame-rate independent.")]
    [Range(0f, 1f)]
    public float retractionFadeRate = 0.95f;

    #endregion

    #region Serialized — Lateral Arc

    [Header("Lateral Arc")]
    public float lateralArcSpeed = 5f;
    [Tooltip("Desactiva la retracción completamente durante el arco lateral.")]
    public bool suppressRetractionDuringLateral = false;
    [Tooltip("Velocidad mínima de retracción durante el arco lateral.")]
    public float minRetractionDuringLateral = 0.1f;

    #endregion

    #region Serialized — Vertical Arc

    [Header("Vertical Arc")]
    public float upwardArcSpeed = 4f;
    public float downwardArcSpeed = 4f;
    [Tooltip("Desactiva la retracción completamente durante el arco vertical.")]
    public bool suppressRetractionDuringVertical = false;
    [Tooltip("Velocidad mínima de retracción durante el arco vertical.")]
    public float minRetractionDuringVertical = 0.1f;

    #endregion

    #region Serialized — Release Conditions

    [Header("Release Conditions")]
    [Tooltip("Toggle: pulsar dispara y volver a pulsar suelta. HoldToKeep: soltar el botón suelta.")]
    public HookReleaseMode releaseMode = HookReleaseMode.Toggle;
    [Tooltip("Si está activo, soltar el gancho al pulsar el botón de salto.")]
    public bool releaseOnJumpInput = true;
    public bool autoReleaseWhenClose = true;
    public float autoReleaseDistanceThreshold = 1.5f;
    [Tooltip("Si está activo, suelta el hook al superar 90° horizontales sin input. Si está desactivo, nunca suelta por ángulo.")]
    public bool releaseOnPerpendicularCrossing = true;

    #endregion

    #region Serialized — Input Suppression On Release

    [Header("Input Suppression On Release")]
    [Tooltip("Suprime el input hacia abajo (S / stick abajo) al soltar el gancho, evitando que cancele la inercia del swing.")]
    public bool suppressInputOnSwingDown = true;
    [Tooltip("Suprime el input hacia arriba (W / stick arriba) al soltar el gancho.")]
    public bool suppressInputOnSwingUp = true;
    [Tooltip("Suprime el input lateral (A/D / stick horizontal) al soltar el gancho.")]
    public bool suppressInputOnSwingLateral = true;

    #endregion

    #region Serialized — Inertia

    [Header("Inertia On Release")]
    public float inertiaMultiplier = 2f;
    public float inertiaVerticalMultiplier = 1.5f;
    public float inertiaMinMagnitude = 1f;

    #endregion

    #region Serialized — Debug

    [Header("Debug — read only")]
    public bool isAimingAtValidSurface;
    public bool hookIsActive;
    public Vector3 hookImpactPoint;
    public float currentDistanceToImpact;

    #endregion

    #region Private — State

    private IPlayerContext _player;

    private Vector3 _aimRaycastEndPoint;
    private bool _aimRaycastHitSomething;
    public Vector3 initialCableDirection;

    private float _currentRetractionSpeed;

    private Vector3 _verticalPlaneNormal;
    private bool _verticalPlaneNormalSet;

    private Vector3 _previousPlayerPosition;
    private Vector3 _lastFrameVelocity;

    // Estado de swing activo este frame.
    // Se leen en ReleaseHook para construir el snapshot de supresión de input.
    private bool _isSwingingDown;
    private bool _isSwingingUp;
    private bool _isSwingingLateral;

    private InputManager _input;
    private const string FIRE_BUTTON = "Fire1";

    #endregion

    #region Initialization

    private void Awake()
    {
        _player = GetComponent<IPlayerContext>();
        _input = InputManager.Instance;

        if (visualizer == null)
            visualizer = GetComponent<GrapplingHookVisualizer>();
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        PerformAimRaycast();
        HandleFireInput();
        CheckJumpRelease();

        if (!hookIsActive)
            return;

        float lateralInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        bool lateralActive = Mathf.Abs(lateralInput) > 0.01f;
        bool verticalActive = Mathf.Abs(verticalInput) > 0.01f;

        // Resetear la normal vertical antes de todo para que ApplyRetraction
        // ya vea el estado correcto este mismo frame
        if (!verticalActive)
            _verticalPlaneNormalSet = false;

        UpdateSwingDirectionState(lateralInput, verticalInput, lateralActive, verticalActive);
        ApplyRetraction(lateralActive, verticalActive);

        if (lateralActive)
            ApplyLateralArc(lateralInput);

        if (verticalActive)
            ApplyVerticalArc(verticalInput);

        RotatePlayerTowardsImpact();
        CheckIfPlayerReachedImpactPoint();
        TrackPlayerVelocityThisFrame();
    }

    #endregion

    #region Swing Direction State

    /// Actualiza los tres bools de dirección de swing cada frame.
    /// Son la fuente de verdad del eje activo en el momento del release.
    private void UpdateSwingDirectionState(float lateralInput, float verticalInput, bool lateralActive, bool verticalActive)
    {
        _isSwingingDown = verticalActive && verticalInput < 0f;
        _isSwingingUp = verticalActive && verticalInput > 0f;
        _isSwingingLateral = lateralActive;
    }

    #endregion

    #region Arc Movement — Lateral

    private void ApplyLateralArc(float lateralInput)
    {
        Vector3 ropeDir = DirectionFromPlayerToImpact();
        Vector3 lateralTangent = Vector3.Cross(ropeDir, Vector3.up).normalized;

        _player.CharacterController.Move(
            lateralTangent * (-lateralInput * lateralArcSpeed * Time.deltaTime)
        );

        currentDistanceToImpact = DistanceFromPlayerToImpact();
        ReprojectOntoCableSphere();
    }

    #endregion

    #region Arc Movement — Vertical

    private void ApplyVerticalArc(float verticalInput)
    {
        if (!_verticalPlaneNormalSet)
        {
            _verticalPlaneNormal = Vector3.Cross(DirectionFromPlayerToImpact(), Vector3.up).normalized;
            _verticalPlaneNormalSet = true;
        }

        Vector3 ropeDir = DirectionFromPlayerToImpact();
        Vector3 verticalTangent = Vector3.Cross(_verticalPlaneNormal, ropeDir).normalized;
        float speed = verticalInput > 0f ? upwardArcSpeed : downwardArcSpeed;

        _player.CharacterController.Move(
            verticalTangent * (verticalInput * speed * Time.deltaTime)
        );

        currentDistanceToImpact = DistanceFromPlayerToImpact();
        ReprojectOntoCableSphere();
    }

    #endregion

    #region Retraction

    private void ApplyRetraction(bool lateralActive, bool verticalActive)
    {
        float targetSpeed;

        if (lateralActive && suppressRetractionDuringLateral)
            targetSpeed = minRetractionDuringLateral;
        else if (verticalActive && suppressRetractionDuringVertical)
            targetSpeed = minRetractionDuringVertical;
        else
            targetSpeed = retractionSpeed;

        // Fade exponencial frame-rate independent hacia abajo, instantáneo hacia arriba.
        // Pow(1 - rate, deltaTime) garantiza la misma curva a cualquier framerate.
        if (targetSpeed < _currentRetractionSpeed)
            _currentRetractionSpeed = Mathf.Lerp(_currentRetractionSpeed, targetSpeed, 1f - Mathf.Pow(1f - retractionFadeRate, Time.deltaTime));
        else
            _currentRetractionSpeed = targetSpeed;

        if (_currentRetractionSpeed < 0.001f)
            return;

        _player.CharacterController.Move(
            DirectionFromPlayerToImpact() * _currentRetractionSpeed * Time.deltaTime
        );

        currentDistanceToImpact = DistanceFromPlayerToImpact();
    }

    #endregion

    #region Sphere Reprojection

    private void ReprojectOntoCableSphere()
    {
        Vector3 toPlayer = _player.PlayerTransform.position - hookImpactPoint;

        if (toPlayer.magnitude < 0.01f)
            return;

        Vector3 correctPos = hookImpactPoint + toPlayer.normalized * currentDistanceToImpact;
        _player.CharacterController.Move(correctPos - _player.PlayerTransform.position);
    }

    #endregion

    #region Aim Raycast

    private void PerformAimRaycast()
    {
        Ray aimRay = new Ray(RayOrigin, RayDirection);

        _aimRaycastHitSomething = Physics.Raycast(
            aimRay,
            out RaycastHit hit,
            aimMaxDistance,
            environmentLayers,
            QueryTriggerInteraction.Ignore
        );

        if (_aimRaycastHitSomething)
        {
            _aimRaycastEndPoint = hit.point;
            isAimingAtValidSurface = hit.collider.CompareTag(environmentTag);
        }
        else
        {
            _aimRaycastEndPoint = aimRay.origin + aimRay.direction * aimMaxDistance;
            isAimingAtValidSurface = false;
        }
    }

    #endregion

    #region Fire Input

    private void HandleFireInput()
    {
        switch (releaseMode)
        {
            case HookReleaseMode.Toggle:
                HandleFireInputToggle();
                break;
            case HookReleaseMode.HoldToKeep:
                HandleFireInputHoldToKeep();
                break;
        }
    }

    /// Pulsar dispara. Volver a pulsar suelta.
    private void HandleFireInputToggle()
    {
        if (!Input.GetButtonDown(FIRE_BUTTON))
            return;

        if (!hookIsActive && isAimingAtValidSurface)
            FireHook();
        else if (hookIsActive)
            ReleaseHook();
    }

    /// Pulsar dispara. Soltar el botón suelta.
    private void HandleFireInputHoldToKeep()
    {
        if (Input.GetButtonDown(FIRE_BUTTON) && !hookIsActive && isAimingAtValidSurface)
            FireHook();
        else if (hookIsActive && Input.GetButtonUp(FIRE_BUTTON))
            ReleaseHook();
    }

    /// Suelta el gancho al pulsar salto, si la opción está activa.
    /// El salto en sí no se ejecuta — eso se gestiona por separado.
    private void CheckJumpRelease()
    {
        if (releaseOnJumpInput && hookIsActive && _input.jumpButtonInput)
            ReleaseHook();
    }

    private void FireHook()
    {
        if (_player.IsOnLedge)
            return;

        hookIsActive = true;
        hookImpactPoint = _aimRaycastEndPoint;
        currentDistanceToImpact = DistanceFromPlayerToImpact();
        initialCableDirection = (hookImpactPoint - _player.PlayerTransform.position).normalized;
        _currentRetractionSpeed = retractionSpeed;
        _verticalPlaneNormalSet = false;

        _player.SetHookActive(true);
        _player.SetMovementBlocked(true);
    }

    private void ReleaseHook()
    {
        hookIsActive = false;
        _verticalPlaneNormalSet = false;

        _player.SetHookActive(false);
        _player.SetMovementBlocked(false);

        // Capturamos el snapshot de input ANTES de aplicar la inercia.
        // Los bools de control por eje permiten decidir en el Inspector qué direcciones
        // participan en la supresión, combinados con el estado real de swing de este frame.
        Vector2 rawInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        _player.CaptureSwingInputSnapshot(
            rawInput,
            wasSwingingDown: _isSwingingDown && suppressInputOnSwingDown,
            wasSwingingUp: _isSwingingUp && suppressInputOnSwingUp,
            wasSwingingLateral: _isSwingingLateral && suppressInputOnSwingLateral
        );

        if (_lastFrameVelocity.magnitude < inertiaMinMagnitude)
        {
            Debug.Log($"[GrapplingHook] Released — inertia too weak ({_lastFrameVelocity.magnitude:F2})");
            return;
        }

        Vector3 amplifiedInertia = new Vector3(
            _lastFrameVelocity.x * inertiaMultiplier,
            _lastFrameVelocity.y * inertiaMultiplier * inertiaVerticalMultiplier,
            _lastFrameVelocity.z * inertiaMultiplier
        );

        _player.SetReceivedInertia(amplifiedInertia);
        Debug.Log($"[GrapplingHook] Released — inertia: {amplifiedInertia} (mag: {amplifiedInertia.magnitude:F2})");
    }

    #endregion

    #region Rotation

    private void RotatePlayerTowardsImpact()
    {
        Vector3 horizontal = new Vector3(
            DirectionFromPlayerToImpact().x,
            0f,
            DirectionFromPlayerToImpact().z
        );

        if (horizontal == Vector3.zero)
            return;

        float targetAngle = Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg;
        ((PlayerController)_player).RotateTowardsAngle(targetAngle);
    }

    #endregion

    #region Release Conditions

    private void CheckIfPlayerReachedImpactPoint()
    {
        if (autoReleaseWhenClose && currentDistanceToImpact < autoReleaseDistanceThreshold)
        {
            Debug.Log("[GrapplingHook] Released — reached impact point");
            ReleaseHook();
            return;
        }

        CheckIfPerpendicularReleaseRequired();
    }

    private void CheckIfPerpendicularReleaseRequired()
    {
        if (!releaseOnPerpendicularCrossing)
            return;

        // No cortar durante arco vertical — puede pasar los 90° horizontales
        if (_verticalPlaneNormalSet)
            return;

        Vector3 hInitial = new Vector3(initialCableDirection.x, 0f, initialCableDirection.z).normalized;
        Vector3 hCurrent = new Vector3(DirectionFromPlayerToImpact().x, 0f, DirectionFromPlayerToImpact().z).normalized;

        if (Vector3.Angle(hCurrent, hInitial) <= 90f)
            return;

        if (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f)
            return;

        ReleaseHook();
    }

    #endregion

    #region Velocity Tracking

    private void TrackPlayerVelocityThisFrame()
    {
        _lastFrameVelocity = (_player.PlayerTransform.position - _previousPlayerPosition) / Time.deltaTime;
        _previousPlayerPosition = _player.PlayerTransform.position;
    }

    #endregion

    #region Distance Helpers

    private float DistanceFromPlayerToImpact()
        => Vector3.Distance(_player.PlayerTransform.position, hookImpactPoint);

    private Vector3 DirectionFromPlayerToImpact()
        => (hookImpactPoint - _player.PlayerTransform.position).normalized;

    #endregion

    #region Public Accessors — para GrapplingHookVisualizer

    public Vector3 GetAimRaycastOrigin() => RayOrigin;
    public Vector3 GetAimRaycastEndPoint() => _aimRaycastEndPoint;
    public bool AimRaycastHitSomething() => _aimRaycastHitSomething;
    public Vector3 GetInitialCableDirection() => initialCableDirection;
    public Vector3 GetVerticalPlaneNormal() => _verticalPlaneNormal;
    public bool IsSwingingVertically => _verticalPlaneNormalSet;

    private Vector3 RayOrigin => rayOriginOverride != null
        ? rayOriginOverride.position
        : _player.EyePosition + rayOriginOffset;

    private Vector3 RayDirection
    {
        get
        {
            if (rayOriginOverride != null)
                return rayOriginOverride.forward;

            return _player.MainCamera.transform.forward;
        }
    }
    /// Suelta el gancho limpiamente sin aplicar inercia ni capturar snapshot de input.
    /// Usado cuando un sistema externo toma el control del personaje (ej: ledge grab).
    public void ForceRelease()
    {
        if (!hookIsActive)
            return;

        hookIsActive = false;
        hookImpactPoint = Vector3.zero;
        _verticalPlaneNormalSet = false;
        _lastFrameVelocity = Vector3.zero;
        _currentRetractionSpeed = 0f;

        _player.SetHookActive(false);
        _player.SetMovementBlocked(false);
        _player.SetReceivedInertia(Vector3.zero);
    }

    #endregion

}