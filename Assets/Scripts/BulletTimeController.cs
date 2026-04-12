using UnityEngine;

public class BulletTimeController : MonoBehaviour
{
    [Header("Settings")]
    public bool bulletTimeEnabled = true;
    public KeyCode bulletTimeKey = KeyCode.Q;

    [Range(0f, 1f)]
    public float slowMotionScale = 0.2f;

    [Tooltip("Velocidad de transición (unscaled). Valores altos = más instantáneo.")]
    public float transitionSpeed = 5f;

    [Header("Debug — read only")]
    public bool bulletTimeActive;

    private float _targetTimeScale = 1f;

    private void Update()
    {
        if (bulletTimeEnabled && Input.GetKeyDown(bulletTimeKey))
        {
            bulletTimeActive = !bulletTimeActive;
            _targetTimeScale = bulletTimeActive ? slowMotionScale : 1f;
        }

        Time.timeScale = Mathf.MoveTowards(
            Time.timeScale,
            _targetTimeScale,
            transitionSpeed * Time.unscaledDeltaTime
        );

        // El fixedDeltaTime ha de seguir al timeScale para que la física sea consistente
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    private void OnDisable()
    {
        // Garantiza que al desactivar el componente no se quede el juego a cámara lenta
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        bulletTimeActive = false;
    }
}