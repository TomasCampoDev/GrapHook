using UnityEngine;

/// <summary>
/// Adapta el GrapplingHookController al sistema de equipamiento.
/// Vive en el mismo GameObject que GrapplingHookController y GrapplingHookVisualizer.
///
/// OnEquip  → habilita el visualizador y la lógica del gancho.
/// OnUnequip → suelta el gancho con su inercia normal (igual que soltar el botón)
///             y deshabilita el visualizador.
/// </summary>
[RequireComponent(typeof(GrapplingHookController))]
public class GrapplingHookEquipment : MonoBehaviour, IEquipment
{
    #region References

    private GrapplingHookController _hook;
    private GrapplingHookVisualizer _visualizer;

    #endregion

    #region Initialization

    private void Awake()
    {
        _hook       = GetComponent<GrapplingHookController>();
        _visualizer = GetComponent<GrapplingHookVisualizer>();
    }

    #endregion

    #region IEquipment

    public string DisplayName => "Grappling Hook";

    public void OnEquip()
    {
        _hook.enabled = true;

        if (_visualizer != null)
            _visualizer.enabled = true;
    }

    public void OnUnequip()
    {
        if (_hook.hookIsActive)
            _hook.ReleaseHookWithInertia();

        _hook.enabled = false;

        if (_visualizer != null)
            _visualizer.enabled = false;
    }

    #endregion
}
