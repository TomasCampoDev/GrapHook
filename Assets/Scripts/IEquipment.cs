/// <summary>
/// Contrato que implementa cualquier arma o equipamiento del personaje.
/// El EquipmentController descubre todos los IEquipment en los hijos
/// del personaje y gestiona cuál está activo.
///
/// Cada implementación es responsable de habilitar/deshabilitar
/// sus propias meshes y lógica en OnEquip/OnUnequip.
/// </summary>
public interface IEquipment
{
    string DisplayName { get; }
    void OnEquip();
    void OnUnequip();
}
