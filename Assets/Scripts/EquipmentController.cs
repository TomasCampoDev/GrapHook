using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona el ciclo de equipamiento del personaje.
/// Descubre todos los IEquipment en los hijos en Awake y gestiona cuál está activo.
/// Rueda del ratón o R2 (ChangeWeapon en el InputActionAsset) cicla entre armas.
/// Al cambiar, llama OnUnequip en el arma activa y OnEquip en la nueva.
/// </summary>
public class EquipmentController : MonoBehaviour
{
    #region Inspector

    [Header("Debug — Read Only")]
    [SerializeField] private string activeWeaponName;
    [SerializeField] private int    activeWeaponIndex;

    #endregion

    #region Private State

    private List<IEquipment> _equipment      = new();
    private int              _activeIndex    = 0;
    private InputAction      _changeWeaponAction;

    #endregion

    #region Initialization

    private void Awake()
    {
        DiscoverEquipment();

        InputActionAsset inputActions = Resources.Load<InputActionAsset>("Input/PlayerControls");
        _changeWeaponAction = inputActions?.FindAction("ChangeWeapon");
    }

    private void Start()
    {
        if (_equipment.Count > 0)
            EquipAtIndex(0);
    }

    private void OnEnable()
    {
        _changeWeaponAction?.Enable();
    }

    private void OnDisable()
    {
        _changeWeaponAction?.Disable();
    }

    private void DiscoverEquipment()
    {
        IEquipment[] found = GetComponentsInChildren<IEquipment>(includeInactive: true);
        _equipment = new List<IEquipment>(found);
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        HandleChangeWeaponInput();
        RefreshDebugInfo();
    }

    #endregion

    #region Input

    private void HandleChangeWeaponInput()
    {
        if (_changeWeaponAction == null || _equipment.Count <= 1)
            return;

        float scrollValue = _changeWeaponAction.ReadValue<float>();

        if (scrollValue > 0f)
            CycleWeapon(direction: 1);
        else if (scrollValue < 0f)
            CycleWeapon(direction: -1);
    }

    #endregion

    #region Weapon Cycling

    private void CycleWeapon(int direction)
    {
        int nextIndex = MathUtility.WrapIndex(_activeIndex + direction, _equipment.Count);
        EquipAtIndex(nextIndex);
    }

    private void EquipAtIndex(int index)
    {
        if (index < 0 || index >= _equipment.Count)
            return;

        if (_activeIndex != index && _equipment.Count > 0)
            _equipment[_activeIndex].OnUnequip();

        _activeIndex = index;
        _equipment[index].OnEquip();
    }

    #endregion

    #region Public API

    public IEquipment ActiveEquipment
        => _equipment.Count > 0 ? _equipment[_activeIndex] : null;

    #endregion

    #region Debug

    private void RefreshDebugInfo()
    {
        activeWeaponName  = ActiveEquipment?.DisplayName ?? "—";
        activeWeaponIndex = _activeIndex;
    }

    #endregion
}
