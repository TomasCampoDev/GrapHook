using UnityEngine;

/// <summary>
/// Centraliza todas las llamadas al Animator del personaje.
/// Ningún otro script llama a animator.SetBool/SetFloat/SetTrigger directamente.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorBridge : MonoBehaviour
{
    #region Constants — Parameter Names

    private const string PARAM_MOVE_AMOUNT = "moveAmount";
    private const string PARAM_GROUNDED = "Grounded";
    private const string PARAM_FREE_FALL = "FreeFall";
    private const string PARAM_JUMP = "Jump";
    private const string PARAM_JUMP_TRIGGER = "JumpTrigger";
    private const string PARAM_ON_LEDGE = "OnLedge";
    private const string PARAM_MOVE_RIGHT = "MoveSidewaysRight";
    private const string PARAM_MOVE_LEFT = "MoveSidewaysLeft";
    private const string PARAM_LOOKING_BACK = "LookingBackOnLedge";
    private const string PARAM_LOOKING_BACK_SIDE = "LookingBackSide";
    private const string PARAM_CLIMB_LEDGE = "ClimbLedge";

    #endregion

    #region References

    private Animator _animator;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    #endregion

    #region Movement Parameters

    public void SetMoveAmount(float amount) => _animator.SetFloat(PARAM_MOVE_AMOUNT, amount);
    public void SetGrounded(bool grounded) => _animator.SetBool(PARAM_GROUNDED, grounded);
    public void SetFreeFall(bool freeFall) => _animator.SetBool(PARAM_FREE_FALL, freeFall);

    #endregion

    #region Jump Parameters

    public void SetJump(bool jumping) => _animator.SetBool(PARAM_JUMP, jumping);
    public void TriggerJump() => _animator.SetTrigger(PARAM_JUMP_TRIGGER);

    #endregion

    #region Ledge Parameters

    public void SetOnLedge(bool onLedge) => _animator.SetBool(PARAM_ON_LEDGE, onLedge);
    public void SetMoveSidewaysRight(bool active) => _animator.SetBool(PARAM_MOVE_RIGHT, active);
    public void SetMoveSidewaysLeft(bool active) => _animator.SetBool(PARAM_MOVE_LEFT, active);
    public void SetLookingBack(bool lookingBack) => _animator.SetBool(PARAM_LOOKING_BACK, lookingBack);
    public void SetLookingBackSide(float side) => _animator.SetFloat(PARAM_LOOKING_BACK_SIDE, side);
    public void SetClimbLedge(bool climbing) => _animator.SetBool(PARAM_CLIMB_LEDGE, climbing);

    public void ResetLedgeAnimations()
    {
        SetOnLedge(false);
        SetMoveSidewaysRight(false);
        SetMoveSidewaysLeft(false);
        SetLookingBack(false);
        SetClimbLedge(false);
        SetJump(false);
        SetFreeFall(false);
    }

    #endregion

    #region Accessors — para módulos que necesitan leer estado del Animator

    public bool GetBool(string paramName) => _animator.GetBool(paramName);
    public float GetFloat(string paramName) => _animator.GetFloat(paramName);

    /// Acceso directo al Animator para casos excepcionales (ej: GetBoneTransform).
    public Animator Raw => _animator;

    #endregion
}