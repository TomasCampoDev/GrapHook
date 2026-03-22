using System.Collections;
using UnityEngine;

/// <summary>
/// Módulo encargado de toda la lógica de ledge grab del personaje:
/// detección, lerp de entrada, estado colgado, climb y salidas (salto y drop).
///
/// Espacio ? sube si IsClimbable, salta hacia atrás si no.
/// F (actionButton) ? suelta el ledge.
///
/// Crea automáticamente un CapsuleCollider hijo para la detección si no existe.
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

    private bool _detectionEnabled = true;
    private bool _jumpBufferedDuringLerp;
    private bool _dropBufferedDuringLerp;

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

        HandleLedgeExitInput();
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

        while (progress < 1f)
        {
            progress += Time.deltaTime * snapLerpSpeed;

            transform.position = Vector3.Lerp(startPosition, target.WorldPosition, progress);
            transform.rotation = Quaternion.Slerp(startRotation, target.Rotation, progress);

            CaptureInputBufferDuringLerp();

            yield return null;
        }
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

    /// Decide qué hacer con el salto según si el ledge es trepable o no.
    private void HandleJumpInputOnLedge()
    {
        if (_currentLedge != null && _currentLedge.IsClimbable)
            StartCoroutine(ExecuteClimbCoroutine());
        else
            ExecuteJumpFromLedge();
    }

    #endregion

    #region Climb

    /// Mueve al personaje hacia adelante y arriba hasta el punto de aterrizaje,
    /// luego libera el estado de ledge.
    private IEnumerator ExecuteClimbCoroutine()
    {
        isClimbing = true;

        _player.Animator.SetClimbLedge(true);

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

        FinishClimb();
    }

    /// El destino del climb: adelante en transform.forward y arriba en transform.up.
    /// El personaje mira hacia la pared, así que forward apunta hacia la pared
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

    private void ExecuteJumpFromLedge()
    {
        float jumpVelocity = Mathf.Sqrt(jumpHeightFromLedge * -2f * gravityForJumpCalculation);
        ExitLedgeState();
        _player.SetVerticalVelocity(jumpVelocity);
        _player.Animator.SetJump(true);
    }

    private void ExecuteDropFromLedge()
    {
        ExitLedgeState();
        _player.SetVerticalVelocity(0f);
    }

    private void ExitLedgeState()
    {
        isGrabbingLedge = false;
        _currentLedge = null;

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
    public Vector3 GetSnapOffsetForPreview() => characterSnapOffset;
    #endregion
}