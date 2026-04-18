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

    [Header("Rig")]
    [SerializeField] private float rigBlendRate = 8f;

    [Header("Shooting")]
    [SerializeField] private float raycastLength = 50f;
    [SerializeField] private float recoilRiseSpeed = 15f;
    [SerializeField] private float recoilFallSpeed = 8f;
    [SerializeField] private float recoilFireThreshold = 0.2f;

    #endregion

    #region Private State

    private bool _isEquipped;
    private PlayerController _player;
    private PlayerAnimatorBridge _playerAnimatorBridge;
    private float _currentStrafeX;
    private float _currentStrafeZ;
    private float _currentRigWeight;
    private float _recoilNoise;
    private bool _isFiring;

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
        HandPistolMesh.SetActive(true);
        BackPistolMesh.SetActive(false);
        _playerAnimatorBridge?.SetAimingGunArmsLayerActive(true);
    }

    public void OnUnequip()
    {
        _isEquipped = false;
        ResetStrafeAnimation();
        _player?.SetRotationBlocked(false);
        _playerAnimatorBridge?.SetIsAiming(false);
        _playerAnimatorBridge?.SetAimingGunLayerActive(false);
        _playerAnimatorBridge?.SetAimingGunArmsLayerActive(false);
        _currentRigWeight = 0f;
        _recoilNoise = 0f;
        _isFiring = false;
        _playerAnimatorBridge?.SetRigWeights(0f);
        HandPistolMesh.SetActive(false);
        BackPistolMesh.SetActive(true);
    }

    #endregion

    #region Unity Lifecycle

    public void Update()
    {
        HandleCornerCases();
        if (!_isEquipped) return;

        bool isAiming = _player.IsAiming;

        if (!_player.IsOnLedge)
        {
            _playerAnimatorBridge?.SetAimingGunArmsLayerActive(true);
            _playerAnimatorBridge?.SetAimingGunLayerActive(isAiming);
            _playerAnimatorBridge?.SetIsAiming(isAiming);

            float targetRigWeight = isAiming ? 1f : 0f;
            _currentRigWeight = Mathf.Lerp(_currentRigWeight, targetRigWeight, Time.deltaTime * rigBlendRate);
            _playerAnimatorBridge?.SetRigWeights(_currentRigWeight);

            if (isAiming)
            {
                HandleAimRotation();
                HandleStrafeAnimation();
                HandleShooting();
            }
            else
            {
                ResetStrafeAnimation();
                _player.SetRotationBlocked(false);
            }
        }

        UpdateRecoil();
    }

    #endregion

    #region Shooting

    private bool CanFire => _recoilNoise <= recoilFireThreshold;

    private void HandleShooting()
    {
        DrawDebugRaycast();

        if (Input.GetMouseButtonDown(0) && CanFire)
        {
            Fire();
        }
    }

    private void Fire()
    {
        _isFiring = true;
    }

    private void UpdateRecoil()
    {
        if (_isFiring)
        {
            _recoilNoise = Mathf.MoveTowards(_recoilNoise, 1f, Time.deltaTime * recoilRiseSpeed);
            if (_recoilNoise >= 1f)
                _isFiring = false;
        }
        else
        {
            _recoilNoise = Mathf.MoveTowards(_recoilNoise, 0f, Time.deltaTime * recoilFallSpeed);
        }

        _playerAnimatorBridge?.SetRecoilNoise(_recoilNoise);
    }

    private void DrawDebugRaycast()
    {
        Transform cam = _player.MainCamera.transform;
        Color rayColor = CanFire ? Color.green : Color.red;
        Debug.DrawRay(cam.position, cam.forward * raycastLength, rayColor);
    }

    #endregion

    #region Aim Rotation

    private void HandleAimRotation()
    {
        Vector3 camForward = _player.MainCamera.transform.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude < 0.001f) return;

        _player.transform.rotation = Quaternion.Lerp(
            _player.transform.rotation,
            Quaternion.LookRotation(camForward.normalized),
            Time.deltaTime * aimRotationSpeed
        );
    }

    #endregion

    #region Strafe Animation

    private void HandleStrafeAnimation()
    {
        _player.SetRotationBlocked(true);
        _playerAnimatorBridge.SetMoveAmount(0f);

        Vector2 rawInput = InputManager.Instance.movementInput;
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
        _playerAnimatorBridge?.SetMoveAmountX(0f);
        _playerAnimatorBridge?.SetMoveAmountZ(0f);
    }

    #endregion

    #region Corner Cases

    private void HandleCornerCases()
    {
        if (_player.IsOnLedge)
        {
            ResetStrafeAnimation();
            _player?.SetRotationBlocked(false);
            _playerAnimatorBridge?.SetIsAiming(false);
            _playerAnimatorBridge?.SetAimingGunLayerActive(false);
            _playerAnimatorBridge?.SetAimingGunArmsLayerActive(false);
            _currentRigWeight = 0f;
            _playerAnimatorBridge?.SetRigWeights(0f);
        }
    }

    #endregion
}