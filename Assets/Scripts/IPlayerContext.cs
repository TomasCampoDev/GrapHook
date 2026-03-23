using UnityEngine;

/// <summary>
/// Contrato que expone el PlayerController al resto de módulos.
/// Ningún módulo dependiente referencia el MonoBehaviour concreto.
/// </summary>
public interface IPlayerContext
{
    #region Transform & Movement

    Transform PlayerTransform { get; }
    CharacterController CharacterController { get; }

    /// Velocidad vertical actual (gravedad / salto). Los módulos pueden leerla pero no escribirla.
    float VerticalVelocity { get; }

    bool IsGrounded { get; }
    bool IsOnLedge { get; }
    bool IsLerpingToLedge { get; }
    bool IsHookActive { get; }
    bool IsAiming { get; }

    #endregion

    #region Camera

    Camera MainCamera { get; }

    /// Posición de los ojos del personaje (para raycasts de apuntado).
    Vector3 EyePosition { get; }
    Vector3 CameraForward { get; }

    #endregion

    #region Animator

    PlayerAnimatorBridge Animator { get; }

    #endregion

    #region State Setters — sólo los módulos autorizados deben llamarlos

    /// El PhysicsController notifica cuándo el personaje toca el suelo.
    void SetGrounded(bool grounded, Transform ground);

    /// El LedgeGrabController notifica cuándo el personaje está en un saliente.
    void SetOnLedge(bool onLedge);

    /// El LedgeGrabController notifica cuándo está en medio del lerp de agarre,
    /// para que la gravedad quede suspendida durante el trayecto.
    void SetLerpingToLedge(bool lerping);

    /// El PhysicsController escribe la velocidad vertical para que el PlayerController
    /// la aplique en su Move final.
    void SetVerticalVelocity(float velocity);

    /// Permite a módulos externos (ledge, hook) bloquear el movimiento en suelo.
    void SetMovementBlocked(bool blocked);

    /// Permite a módulos externos bloquear la rotación del personaje.
    void SetRotationBlocked(bool blocked);

    /// El GrapplingHookController aplica inercia al soltar el gancho.
    void SetReceivedInertia(Vector3 inertia);

    /// El GrapplingHookController notifica si el gancho está activo
    /// para que la gravedad quede suspendida.
    void SetHookActive(bool active);

    /// Llamado por GrapplingHookController al soltar el gancho.
    /// Registra qué input raw estaba activo y en qué eje se estaba swingando,
    /// para suprimir ese input mientras el jugador siga pulsándolo.
    void CaptureSwingInputSnapshot(Vector2 rawInput, bool wasSwingingDown, bool wasSwingingUp, bool wasSwingingLateral);

    void ForceUngrounded();

    #endregion
}