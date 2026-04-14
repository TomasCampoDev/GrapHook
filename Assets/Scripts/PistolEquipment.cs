using UnityEngine;

public class PistolEquipment : MonoBehaviour, IEquipment
{
    #region Inspector

    [Header("Visuals")]
    [SerializeField] private GameObject HandPistolMesh;
    [SerializeField] private GameObject BackPistolMesh;



    [Header("Aim Rotation")]
    [SerializeField] private float aimRotationSpeed = 10f;

    [Header("Strafe Animation")]
    [SerializeField] private float strafeAnimationBlendRate = 10f;

    #endregion

    #region Private State

    private bool _isEquipped;
    private PlayerController _player;
    private PlayerAnimatorBridge _playerAnimatorBridge;

    private float _currentStrafeX;
    private float _currentStrafeZ;

    #endregion

    #region IEquipment

    public string DisplayName => "Pistola";
    public void Start()
    {
        _player = GetComponentInParent<PlayerController>();
        _playerAnimatorBridge = GetComponentInParent<PlayerAnimatorBridge>();
    }

    public void OnEquip()
    {
        _isEquipped = true;


        _playerAnimatorBridge?.SetAimingGunLayerActive(true);

        HandPistolMesh.SetActive(true);
        BackPistolMesh.SetActive(false);

    }

    public void OnUnequip()
    {
        _isEquipped = false;

        ResetStrafeAnimation();
        _playerAnimatorBridge?.SetAimingGunLayerActive(false);

        HandPistolMesh.SetActive(false);
        BackPistolMesh.SetActive(true);

    }

    #endregion

    #region Unity Lifecycle

    public void Update()
    {
        if (_isEquipped)
        {
            if (_player.IsAiming)
            {
                HandleAimRotation();
                HandleStrafeAnimation();
                _playerAnimatorBridge?.SetAimingGunLayerActive(true);

            }
            else
            {
                //ResetStrafeAnimation();
                _player.SetRotationBlocked(false);

                _playerAnimatorBridge?.SetAimingGunLayerActive(false);
            }
        }
        else
        {

            _player.SetRotationBlocked(false);

            // Limpieza adicional si fuera necesaria
        }
    }

    #endregion

    #region Aim Rotation

    private void HandleAimRotation()
    {
        if (_player == null || !_player.IsAiming) return;

        Vector3 camForward = _player.MainCamera.transform.forward;
        camForward.y = 0f;

        if (camForward.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(camForward.normalized);

        _player.transform.rotation = Quaternion.Lerp(
            _player.transform.rotation,
            targetRotation,
            Time.deltaTime * aimRotationSpeed
        );
    }

    #endregion

    #region Strafe Animation

    private void HandleStrafeAnimation()
    {
        if (_player == null || _playerAnimatorBridge == null) return;

        if (!_player.IsAiming)
        {
            ResetStrafeAnimation();
            _player.SetRotationBlocked(false);
            return;
        }

        _player.SetRotationBlocked(true);
        _playerAnimatorBridge.SetMoveAmount(0f);

        Vector2 rawInput = GetMovementInput();

        float t = Time.deltaTime * strafeAnimationBlendRate;
        _currentStrafeX = Mathf.Lerp(_currentStrafeX, rawInput.x, t);
        _currentStrafeZ = Mathf.Lerp(_currentStrafeZ, rawInput.y, t);

        _playerAnimatorBridge.SetMoveAmountX(_currentStrafeX);
        _playerAnimatorBridge.SetMoveAmountZ(_currentStrafeZ);
    }

    private void ResetStrafeAnimation()
    {
        _currentStrafeX = 0f;
        _currentStrafeZ = 0f;

        if (_playerAnimatorBridge == null) return;

        _playerAnimatorBridge.SetMoveAmountX(0f);
        _playerAnimatorBridge.SetMoveAmountZ(0f);
    }

    // Accedemos al input a través del InputManager para no acoplar a PlayerController
    private Vector2 GetMovementInput() => InputManager.Instance.movementInput;

    #endregion
}