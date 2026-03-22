/// <summary>
/// Contrato que implementa cualquier entidad capaz de agarrarse a un LedgeAnchor.
/// LedgeAnchor lo llama desde OnTriggerEnter sin necesitar referenciar PlayerController.
/// </summary>
public interface ILedgeGrabbable
{
    /// Llamado por el LedgeAnchor cuando el jugador entra en su trigger.
    void OnLedgeDetected(LedgeAnchor ledge);
}