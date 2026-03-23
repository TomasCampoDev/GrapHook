using System.Collections;
using UnityEngine;

/// <summary>
/// Módulo encargado de toda la lógica de ledge grab del personaje:
/// detección, lerp de entrada, movimiento lateral, looking back, climb y salidas.
///
/// Espacio → sube si IsClimbable, salta hacia atrás si no.
/// F (actionButton) → suelta el ledge.
/// Input lateral → se mueve a lo largo del borde (de LeftEdge a RightEdge).
/// Input hacia atrás → activa LookingBack, bloquea movimiento lateral.
///
/// Sigue el mismo patrón que PlayerPhysicsController:
///   - Lee y escribe estado a través de IPlayerContext.
///   - No referencia PlayerController directamente salvo para TemporarilyDisableGroundCheck.
/// </summary>
public class LedgeGrabController : MonoBehaviour, ILedgeGrabbable
{
    #region Inspector

    [Header("Snap Offset")]
    [Tooltip("Offset en espacio local del ledge para posicionar al personaje. " +
             "Ajusta Y y Z para que encaje con la animación de colgarse.")]
    [SerializeField] private Vector3 characterSnapOffset = new Vector3(0f, -1.54f, -0.4f);

    [Header("Lerp")]
    [SerializeField] private float snapLerpSpeed = 4.5f;

    [Header("Lateral Movement")]
    [SerializeField] private float lateralMoveSpeed = 6f;
    [Tooltip("Ángulo máximo entre el input y transform.right para considerar que el jugador " +
             "quiere moverse lateralmente. Por encima de este ángulo se ignora el movimiento.")]
    [SerializeField] private float lateralInputConeAngle = 60f;

    [Header("Looking Back")]
    [Tooltip("Ángulo del cono trasero. Si el input apunta dentro de este cono, " +
             "se activa LookingBack y se bloquea el movimiento lateral.")]
    [SerializeField] private float lookingBackConeAngle = 80f;
    [SerializeField] private float lookingBackSideLerpSpeed = 3f;

    [Header("Climb")]
    [Tooltip("Cuánto avanza hacia adelante (transform.forward) al trepar.")]
    [SerializeField] public float climbForwardOffset = 0.3f;
    [Tooltip("Cuánto sube (transform.up) al trepar.")]
    [SerializeField] public float climbUpOffset = 1.5f;
    [SerializeField] private float climbMoveSpeed = 4f;

    [Header("Jump From Ledge")]
    [SerializeField] private float jumpHeightFromLedge = 2f;
    [SerializeField] private float gravityForJumpCalculation = -25f;

    [Header("Cooldowns")]
    [Tooltip("Tiempo mínimo tras soltar un ledge antes de poder agarrarse a otro.")]
    [SerializeField] private float ledgeDetectionCooldown = 0.25f;
    [SerializeField] private float groundCheckDisableDuration = 0.2f;

    [Header("Detection Collider")]
    [Tooltip("Si se deja vacío, se crea automáticamente un hijo con CapsuleCollider.")]
    public CapsuleCollider detectionCollider;

    [Header("Debug - Read Only")]
    [SerializeField] private bool isGrabbingLedge;
    [SerializeField] private bool isLerpingToLedge;
    [SerializeField] private bool isClimbing;
    [SerializeField] private bool isLookingBack;
    [SerializeField] private float currentNormalizedTDebug;
    public float LookingBackConeHalfAngle => lookingBackConeAngle * 0.5f;
    public float LateralInputConeHalfAngle => lateralInputConeAngle * 0.5f;

    #endregion

    #region Constants — Detection Collider Auto-Creation

    private const float DetectionCapsuleRadius = 0.6f;
    private const float DetectionCapsuleHeight = 0.6f;
    private const float DetectionCapsuleOffsetY = 1.5f;
    private const float DetectionCapsuleOffsetZ = 0.5f;
    private const string DetectionColliderName = "LedgeDetectionCollider";

    #endregion

    #region Private State

    private IPlayerContext _player;

    private LedgeAnchor _currentLedge;
    private float _currentNormalizedT;
    private float _lookingBackSideLerped;

    private bool _detectionEnabled = true;
    private bool _jumpBufferedDuringLerp;
    private bool _dropBufferedDuringLerp;
    public LedgeAnchor _previousLedgeFromLeft;
    public LedgeAnchor _previousLedgeFromRight;
    #endregion

    #region Initialization

    private void Awake()
    {
        _player = GetComponent<IPlayerContext>();
        EnsureDetectionColliderExists();
    }

    private void EnsureDetectionColliderExists()
    {
        if (detectionCollider != null)
            return;

        detectionCollider = GetComponentInChildren<CapsuleCollider>();

        if (detectionCollider != null)
            return;

        GameObject colliderHost = new GameObject(DetectionColliderName);
        colliderHost.transform.SetParent(transform);
        colliderHost.transform.localPosition = new Vector3(0f, DetectionCapsuleOffsetY, DetectionCapsuleOffsetZ);
        colliderHost.transform.localRotation = Quaternion.identity;

        CapsuleCollider capsule = colliderHost.AddComponent<CapsuleCollider>();
        capsule.radius = DetectionCapsuleRadius;
        capsule.height = DetectionCapsuleHeight;
        capsule.direction = 1;
        capsule.center = Vector3.zero;
        capsule.isTrigger = true;

        detectionCollider = capsule;
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (!_detectionEnabled || isGrabbingLedge || isLerpingToLedge || _player.IsGrounded)
            return;

        CheckForLedgeOverlap();
    }

    private void LateUpdate()
    {
        if (!isGrabbingLedge || isClimbing)
            return;

        EvaluateLookingBack();
        HandleLedgeExitInput();

        if (!isLookingBack)
            HandleLateralMovement();
    }

    #endregion

    #region Active Overlap Detection

    private void CheckForLedgeOverlap()
    {
        Vector3 capsuleCenter = detectionCollider.transform.TransformPoint(detectionCollider.center);
        float radius = detectionCollider.radius;

        Collider[] hits = Physics.OverlapSphere(capsuleCenter, radius, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            LedgeAnchor ledge = hit.GetComponent<LedgeAnchor>();
            if (ledge != null)
            {
                OnLedgeDetected(ledge);
                return;
            }
        }
    }

    #endregion

    #region ILedgeGrabbable — Entry Point

    public void OnLedgeDetected(LedgeAnchor ledge)
    {
        if (!_detectionEnabled || isGrabbingLedge || isLerpingToLedge || _player.IsGrounded)
            return;

        StartCoroutine(LerpToLedgeCoroutine(ledge));
    }

    #endregion

    #region Ledge Entry

    private IEnumerator LerpToLedgeCoroutine(LedgeAnchor ledge)
    {
        PrepareForLedgeEntry();

        LedgeSnapTarget snapTarget = CalculateSnapTarget(ledge);

        yield return PerformSnapLerp(snapTarget);

        FinalizeLedgeEntry(ledge, snapTarget.NormalizedT);
        ProcessInputBufferedDuringLerp();
    }

    private void PrepareForLedgeEntry()
    {
        isLerpingToLedge = true;

        _player.SetLerpingToLedge(true);
        _player.SetMovementBlocked(true);
        _player.SetRotationBlocked(true);
        _player.SetVerticalVelocity(0f);
        _player.Animator.SetOnLedge(true);
    }

    private LedgeSnapTarget CalculateSnapTarget(LedgeAnchor ledge)
    {
        Vector3 closestPoint = ledge.GetClosestPointOnLedge(transform.position);
        float normalizedT = ledge.GetNormalizedPositionOf(closestPoint);
        Quaternion hangRotation = ledge.GetCharacterHangRotation();
        Vector3 snapPosition = closestPoint + hangRotation * characterSnapOffset;

        return new LedgeSnapTarget(snapPosition, hangRotation, normalizedT);
    }

    private IEnumerator PerformSnapLerp(LedgeSnapTarget target)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float progress = 0f;

        _player.CharacterController.enabled = false;

        while (progress < 1f)
        {
            progress += Time.deltaTime * snapLerpSpeed;
            float t = Mathf.Clamp01(progress);

            transform.position = Vector3.Lerp(startPosition, target.WorldPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, target.Rotation, t);

            CaptureInputBufferDuringLerp();

            yield return null;
        }

        transform.position = target.WorldPosition;
        transform.rotation = target.Rotation;

        _player.CharacterController.enabled = true;
    }

    private void CaptureInputBufferDuringLerp()
    {
        if (InputManager.Instance.jumpButtonInput && !_jumpBufferedDuringLerp)
        {
            _jumpBufferedDuringLerp = true;
            InputManager.Instance.jumpButtonInput = false;
        }

        if (InputManager.Instance.actionButtonInput && !_dropBufferedDuringLerp)
        {
            _dropBufferedDuringLerp = true;
            InputManager.Instance.actionButtonInput = false;
        }
    }

    private void FinalizeLedgeEntry(LedgeAnchor ledge, float normalizedT)
    {
        _currentLedge = ledge;
        _currentNormalizedT = normalizedT;
        isLerpingToLedge = false;
        isGrabbingLedge = true;

        _player.SetLerpingToLedge(false);
        _player.SetOnLedge(true);
        _player.SetVerticalVelocity(0f);
        _player.Animator.SetOnLedge(true);
        _player.Animator.SetJump(false);
        _player.Animator.SetFreeFall(false);
    }

    private void ProcessInputBufferedDuringLerp()
    {
        if (_jumpBufferedDuringLerp)
        {
            _jumpBufferedDuringLerp = false;
            HandleJumpInputOnLedge();
            return;
        }

        if (_dropBufferedDuringLerp)
        {
            _dropBufferedDuringLerp = false;
            ExecuteDropFromLedge();
        }
    }

    #endregion

    #region Looking Back

    /// Evalúa si el input del jugador apunta hacia atrás (fuera del cono frontal del ledge).
    /// Si es así, activa LookingBack y bloquea el movimiento lateral.
    private void EvaluateLookingBack()
    {
        Vector2 input = InputManager.Instance.movementInput;

        if (input.sqrMagnitude < 0.0001f)
        {
            SetLookingBackState(false, 0f);
            return;
        }

        Vector3 inputWorldDir = CalculateWorldDirectionFromInput(input);
        Vector3 ledgeBack = -transform.forward;
        float halfAngle = lookingBackConeAngle * 0.5f;
        float dotThreshold = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        bool lookingBack = Vector3.Dot(ledgeBack, inputWorldDir) >= dotThreshold;

        float sideValue = 0f;
        if (lookingBack)
        {
            Vector3 ledgeRight = Vector3.Cross(Vector3.up, ledgeBack);
            sideValue = Mathf.Clamp(Vector3.Dot(inputWorldDir, ledgeRight), -1f, 1f);
        }

        SetLookingBackState(lookingBack, sideValue);
    }

    private void SetLookingBackState(bool lookingBack, float sideValue)
    {
        isLookingBack = lookingBack;

        _lookingBackSideLerped = Mathf.Lerp(
            _lookingBackSideLerped,
            sideValue,
            Time.deltaTime * lookingBackSideLerpSpeed
        );

        _player.Animator.SetLookingBack(isLookingBack);
        _player.Animator.SetLookingBackSide(_lookingBackSideLerped);
    }

    #endregion

    #region Lateral Movement

    private void HandleLateralMovement()
    {
        Vector2 input = InputManager.Instance.movementInput;

        if (input.sqrMagnitude < 0.01f)
        {
            StopLateralMovement();
            return;
        }

        Vector3 inputWorldDir = CalculateWorldDirectionFromInput(input);
        bool movingRight = IsMovingTowardsRight(inputWorldDir);
        bool movingLeft = IsMovingTowardsLeft(inputWorldDir);

        if (!movingRight && !movingLeft)
        {
            StopLateralMovement();
            return;
        }
       // if (movingRight) _previousLedgeFromRight= null;
       // if (movingLeft) _previousLedgeFromLeft = null;

        MoveLaterallyAlongLedge(movingRight, movingLeft);
        ApplyPositionOnLedge();

        currentNormalizedTDebug = _currentNormalizedT;
    }

    private bool IsMovingTowardsRight(Vector3 inputWorldDir)
    {
        return Vector3.Angle(inputWorldDir, transform.right) <= lateralInputConeAngle;
    }

    private bool IsMovingTowardsLeft(Vector3 inputWorldDir)
    {
        return Vector3.Angle(inputWorldDir, -transform.right) <= lateralInputConeAngle;
    }

    private void MoveLaterallyAlongLedge(bool movingRight, bool movingLeft)
    {
        float ledgeLength = _currentLedge.GetLedgeLength();

        if (ledgeLength < Mathf.Epsilon)
            return;

        float deltaT = (lateralMoveSpeed * Time.deltaTime) / ledgeLength;

        if (movingRight)
        {
            
            _currentNormalizedT += deltaT;
            _player.Animator.SetMoveSidewaysRight(true);
            _player.Animator.SetMoveSidewaysLeft(false);
        }
        else
        {
            _currentNormalizedT -= deltaT;
            _player.Animator.SetMoveSidewaysRight(false);
            _player.Animator.SetMoveSidewaysLeft(true);
        }

        if (TryTransitionToAdjacentLedge(movingRight))
            return;

        _currentNormalizedT = Mathf.Clamp01(_currentNormalizedT);
    }

    /// Comprueba si el jugador ha llegado al extremo del borde en la dirección
    /// de movimiento y hay un ledge adyacente al que transicionar.
    /// Devuelve true si se inició la transición, false si no hay vecino.
    private bool TryTransitionToAdjacentLedge(bool movingRight)
    {
        if (isLookingBack) return false;
        if (movingRight && _currentNormalizedT >= 1f)
        {
            LedgeAnchor nextLedge = _currentLedge.NextRight;
            if (nextLedge != null )
            {
                _previousLedgeFromLeft = _currentLedge;
                TransitionToAdjacentLedge(nextLedge);
                return true;
            }
        }
        else if (!movingRight && _currentNormalizedT <= 0f)
        {
            LedgeAnchor nextLedge = _currentLedge.NextLeft;
            if (nextLedge != null )
            {
                _previousLedgeFromRight = _currentLedge;
                TransitionToAdjacentLedge(nextLedge);
                return true;
            }
        }

        return false;
    }

    private void TransitionToAdjacentLedge(LedgeAnchor targetLedge)
    {
        isGrabbingLedge = false;
        isLookingBack = false;
        StopLateralMovement();
        _player.SetOnLedge(false);
        StartCoroutine(LerpToLedgeCoroutine(targetLedge));
    }

    private void ApplyPositionOnLedge()
    {
        Vector3 ledgePoint = _currentLedge.GetWorldPositionAtNormalizedT(_currentNormalizedT);
        Quaternion hangRotation = _currentLedge.GetCharacterHangRotation();

        transform.position = ledgePoint + hangRotation * characterSnapOffset;
        transform.rotation = hangRotation;
    }

    private void StopLateralMovement()
    {
        _player.Animator.SetMoveSidewaysRight(false);
        _player.Animator.SetMoveSidewaysLeft(false);
    }

    /// Convierte input 2D de pantalla a dirección 3D en world space usando la cámara.
    private Vector3 CalculateWorldDirectionFromInput(Vector2 input)
    {
        Camera cam = _player.MainCamera;
        Vector3 camForward = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;
        Vector3 camRight = new Vector3(cam.transform.right.x, 0f, cam.transform.right.z).normalized;

        return (camRight * input.x + camForward * input.y).normalized;
    }

    #endregion

    #region Ledge Exit Input

    private void HandleLedgeExitInput()
    {
        if (InputManager.Instance.jumpButtonInput)
        {
            InputManager.Instance.jumpButtonInput = false;
            HandleJumpInputOnLedge();
            return;
        }

        if (InputManager.Instance.actionButtonInput)
        {
            InputManager.Instance.actionButtonInput = false;
            ExecuteDropFromLedge();
        }
    }

    private void HandleJumpInputOnLedge()
    {
        if (_currentLedge != null && _currentLedge.IsClimbable && CanClimbFromCurrentInput())
            StartCoroutine(ExecuteClimbCoroutine());
        else
            ExecuteJumpFromLedge();
    }

    private bool CanClimbFromCurrentInput()
    {
        Vector2 input = InputManager.Instance.movementInput;

        if (input.sqrMagnitude < 0.01f)
            return true;

        return !isLookingBack
            && !IsMovingTowardsRight(CalculateWorldDirectionFromInput(input))
            && !IsMovingTowardsLeft(CalculateWorldDirectionFromInput(input));
    }

    #endregion

    #region Climb

    private IEnumerator ExecuteClimbCoroutine()
    {
        isClimbing = true;
        _player.Animator.SetClimbLedge(true);
        _player.CharacterController.enabled = false;

        Vector3 climbDestination = CalculateClimbDestination();

        while (Vector3.Distance(transform.position, climbDestination) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                climbDestination,
                climbMoveSpeed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = climbDestination;
        _player.CharacterController.enabled = true;

        FinishClimb();
    }

    /// Destino del climb: adelante y arriba desde la posición colgada.
    /// El personaje mira hacia la pared, por lo que forward apunta hacia ella
    /// y el personaje sube por encima del borde.
    public Vector3 CalculateClimbDestination()
    {
        return transform.position
             + transform.forward * climbForwardOffset
             + transform.up * climbUpOffset;
    }

    private void FinishClimb()
    {
        isClimbing = false;

        _player.Animator.SetClimbLedge(false);
        _player.Animator.SetOnLedge(false);

        ExitLedgeState();

        _player.SetVerticalVelocity(-2f);
    }

    #endregion

    #region Ledge Exit Execution

    [SerializeField] private float lookingBackJumpHeight = 1.5f;

    private void ExecuteJumpFromLedge()
    {
        ExitLedgeState();
        _player.SetVerticalVelocity(CalculateJumpVelocity());
        _player.Animator.SetJump(true);
    }

    private float CalculateJumpVelocity()
    {
        float height = isLookingBack ? lookingBackJumpHeight : jumpHeightFromLedge;
        return Mathf.Sqrt(height * -2f * gravityForJumpCalculation);
    }

    private void ExecuteDropFromLedge()
    {
        ExitLedgeState();
        _player.SetVerticalVelocity(0f);
    }

    private void ExitLedgeState()
    {
        isGrabbingLedge = false;
        isLerpingToLedge = false;
        isLookingBack = false;
        _currentLedge = null;

        _player.SetLerpingToLedge(false);
        _player.SetOnLedge(false);
        _player.SetMovementBlocked(false);
        _player.SetRotationBlocked(false);
        _player.Animator.ResetLedgeAnimations();

        ((PlayerController)_player).TemporarilyDisableGroundCheck(groundCheckDisableDuration);

        StartCoroutine(DisableLedgeDetectionTemporarily());
    }

    private IEnumerator DisableLedgeDetectionTemporarily()
    {
        _detectionEnabled = false;
        yield return new WaitForSeconds(ledgeDetectionCooldown);
        _detectionEnabled = true;
    }

    #endregion

    #region Public API

    public LedgeAnchor CurrentLedge => _currentLedge;
    public float CurrentNormalizedT => _currentNormalizedT;
    public bool IsOnLedge => isGrabbingLedge;

    public void SetNormalizedPosition(float normalizedT)
    {
        _currentNormalizedT = Mathf.Clamp01(normalizedT);
    }

    public Vector3 GetSnapOffsetForPreview() => characterSnapOffset;

    #endregion

    #region Private Data Structures

    private readonly struct LedgeSnapTarget
    {
        public readonly Vector3 WorldPosition;
        public readonly Quaternion Rotation;
        public readonly float NormalizedT;

        public LedgeSnapTarget(Vector3 worldPosition, Quaternion rotation, float normalizedT)
        {
            WorldPosition = worldPosition;
            Rotation = rotation;
            NormalizedT = normalizedT;
        }
    }

    #endregion
}