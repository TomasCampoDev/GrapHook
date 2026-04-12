using UnityEngine;

/// <summary>
/// Equipamiento: Pistola.
/// Implementa IEquipment — el EquipmentController la descubre automáticamente.
///
/// OnEquip  → habilita la mesh y activa la lógica de disparo.
/// OnUnequip → deshabilita la mesh e interrumpe cualquier acción en curso.
///
/// La lógica de disparo irá en Update, protegida por _isEquipped.
/// </summary>
public class PistolEquipment : MonoBehaviour, IEquipment
{
    #region Inspector
    [Header("Visuals")]
    [Tooltip("GameObject que contiene la mesh de la pistola. " +
             "Se habilita al equipar y se deshabilita al desequipar.")]
    [SerializeField] private GameObject pistolMesh;
    [SerializeField] private GameObject currentPistolMesh;
    [SerializeField] private GameObject pistolMeshHandTransform;
    [SerializeField] private GameObject pistolMeshHandTransformPosition;
    [SerializeField] private GameObject pistolMeshHandTransformPositionRotation;
    [SerializeField] private GameObject pistolMeshBackTransform;
    [SerializeField] private GameObject pistolMeshBackTransformOffsetPosition;
    [SerializeField] private GameObject pistolMeshBackTransformOffsetRotation;

    [Header("Aim Rotation")]
    [Tooltip("Velocidad a la que el personaje rota para alinear su forward con la cámara al apuntar.")]
    [SerializeField] private float aimRotationSpeed = 10f;
    #endregion

    #region Private State
    private bool _isEquipped;
    private PlayerController _player;
    #endregion

    #region IEquipment
    public string DisplayName => "Pistola";

    public void OnEquip()
    {
        _isEquipped = true;
        _player = GetComponentInParent<PlayerController>();

        if (pistolMesh != null)
            pistolMesh.SetActive(true);
    }

    public void OnUnequip()
    {
        _isEquipped = false;

        _player.SetRotationBlocked(false);

        if (pistolMesh != null)
            pistolMesh.SetActive(false);
    }
    #endregion

    #region Pistol Logic
    public void Update()
    {
        if (_isEquipped)
        {
            HandleAimRotation();
        }
        else
        {
            // Desactivar / deshacer lógica activa aquí si fuera necesario
        }
    }

    /// <summary>
    /// Cuando el jugador apunta, rota el personaje suavemente para que su
    /// forward quede alineado con el forward horizontal de la cámara.
    /// </summary>
    private void HandleAimRotation()
    {
        if (_player == null || !_player.IsAiming) return;
        _player.SetRotationBlocked(true);

        // Forward de la cámara proyectado al plano horizontal (sin pitch)
        Vector3 camForward = Camera.main.transform.forward;
        camForward.y = 0f;

        // Evitar LookRotation con vector cero si la cámara mira recto arriba/abajo
        if (camForward.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(camForward.normalized);

        // Lerp suave — no instantáneo
        _player.transform.rotation = Quaternion.Lerp(
            _player.transform.rotation,
            targetRotation,
            Time.deltaTime * aimRotationSpeed
        );
    }
    #endregion
}