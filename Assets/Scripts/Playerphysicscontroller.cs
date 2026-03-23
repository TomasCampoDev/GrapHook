using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona toda la f?sica vertical del personaje: gravedad, salto simple,
/// doble salto y coyote time. Lee y escribe VerticalVelocity a trav?s de IPlayerContext.
/// Requiere que el mismo GameObject tenga un PlayerController.
/// </summary>
public class PlayerPhysicsController : MonoBehaviour
{
    #region Serialized

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private int maxJumps = 2;

    [Header("Gravity")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float terminalVelocity = -15f;

    [Header("Coyote Time")]
    [SerializeField] private float coyoteTimeDuration = 0.3f;

    [Header("Debug ? read only")]
    [SerializeField] private int remainingJumps;
    [SerializeField] private float currentVerticalVelocity;

    #endregion

    #region Private ? State

    private IPlayerContext _player;
    private InputManager _input;

    private float _verticalVelocity;
    private float _coyoteTimeCounter;
    private bool _coyoteTimeActive;

    private const float SMALL_DOWNWARD_FORCE = -2f;
    private const float GROUNDED_CHECK_DELAY = 0.2f;

    #endregion

    #region Initialization

    private void Awake()
    {
        _player = GetComponent<IPlayerContext>();
        _input = InputManager.Instance;
        remainingJumps = maxJumps;
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        ApplyGravity();
        currentVerticalVelocity = _verticalVelocity;
    }

    private void LateUpdate()
    {
        UpdateCoyoteTime();
        HandleJumpInput();
    }

    #endregion

    #region Gravity

    private void ApplyGravity()
    {
        if (_player.IsOnLedge || _player.IsHookActive || _player.IsLerpingToLedge)
        {
            _verticalVelocity = 0f;
            _player.SetVerticalVelocity(0f);
            return;
        }

        if (_player.IsGrounded)
        {
            if (_verticalVelocity < 0f)
                _verticalVelocity = SMALL_DOWNWARD_FORCE;
        }
        else
        {
            if (_verticalVelocity > terminalVelocity)
            {
                _verticalVelocity += gravity * Time.deltaTime;
                _verticalVelocity = Mathf.Max(_verticalVelocity, terminalVelocity);
            }
        }

        _player.SetVerticalVelocity(_verticalVelocity);
    }

    #endregion

    #region Coyote Time

    private void UpdateCoyoteTime()
    {
        if (_player.IsGrounded)
        {
            _coyoteTimeCounter = coyoteTimeDuration;
            _coyoteTimeActive = true;
            remainingJumps = maxJumps;
        }
        else if (_coyoteTimeCounter > 0f)
        {
            _coyoteTimeCounter -= Time.deltaTime;
        }
        else
        {
            _coyoteTimeActive = false;
        }
    }

    private bool CanJumpWithCoyoteTime()
        => _player.IsGrounded || (_coyoteTimeActive && _coyoteTimeCounter > 0f);

    #endregion

    #region Jump Input

    private void HandleJumpInput()
    {
        if (!_input.jumpButtonInput)
            return;

        if (_player.IsOnLedge)
            return; // El LedgeGrabController gestiona el salto desde ledge

        if (_player.IsHookActive)
            return; // El GrapplingHookController gestiona la entrada de salto durante el swing

        if (CanJumpWithCoyoteTime())
        {
            PerformGroundJump();
            return;
        }

        if (!_player.IsGrounded && !CanJumpWithCoyoteTime() && remainingJumps > 0)
        {
            PerformDoubleJump();
        }
    }

    private void PerformGroundJump()
    {
        _coyoteTimeActive = false;
        _coyoteTimeCounter = 0f;
        remainingJumps = maxJumps - 1;

        _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        _player.SetVerticalVelocity(_verticalVelocity);
        _player.Animator.SetJump(true);
        _input.jumpButtonInput = false;

        ((PlayerController)_player).TemporarilyDisableGroundCheck(GROUNDED_CHECK_DELAY);
        _player.ForceUngrounded();
    }

    private void PerformDoubleJump()
    {
        remainingJumps--;
        _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        _player.SetVerticalVelocity(_verticalVelocity);
        _player.Animator.TriggerJump();
        _input.jumpButtonInput = false;
    }

    #endregion

    #region Public API ? para que otros m?dulos puedan lanzar al personaje

    /// Permite a LedgeGrabController aplicar un salto desde el saliente.
    public void ApplyJumpVelocity(float jumpHeightOverride = -1f)
    {
        float h = jumpHeightOverride > 0f ? jumpHeightOverride : jumpHeight;
        _verticalVelocity = Mathf.Sqrt(h * -2f * gravity);
        _player.SetVerticalVelocity(_verticalVelocity);
    }

    public void ResetVerticalVelocity()
    {
        _verticalVelocity = SMALL_DOWNWARD_FORCE;
        _player.SetVerticalVelocity(_verticalVelocity);
    }

    public void ResetJumpCount() => remainingJumps = maxJumps;

    #endregion
}