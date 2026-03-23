using UnityEngine;

/// <summary>
/// Añade este componente al mismo GameObject que LedgeGrabController.
/// Solo dibuja gizmos en el editor, cero coste en build.
///
/// Muestra en todo momento cuando el personaje está en un ledge:
///   - Cono naranja  → zona de "mirando hacia atrás" (lookingBackConeAngle)
///   - Cono púrpura  → zonas laterales izquierda y derecha (lateralInputConeAngle)
///   - Punto cyan    → posición actual en el borde con etiqueta T
///   - Línea blanca  → del personaje al punto en el borde
/// </summary>
public class LedgeGrabVisualizer : MonoBehaviour
{
    #region Inspector

    [Header("Toggles")]
    [SerializeField] private bool showLookingBackCone  = true;
    [SerializeField] private bool showLateralCones     = true;
    [SerializeField] private bool showCurrentPosition  = true;

    [Header("Cone Display")]
    [SerializeField] private float coneRayLength = 1.8f;
    [SerializeField] private int   coneRayCount  = 16;

    [Header("Colors")]
    [SerializeField] private Color lookingBackConeColor  = new Color(1f,   0.5f, 0f,   1f);
    [SerializeField] private Color lateralConeColor      = new Color(0.6f, 0f,   0.8f, 1f);
    [SerializeField] private Color currentPositionColor  = Color.cyan;

    #endregion

    #region References

    private LedgeGrabController _ledgeGrabController;

    private void Awake()
    {
        _ledgeGrabController = GetComponent<LedgeGrabController>();
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (_ledgeGrabController == null)
            _ledgeGrabController = GetComponent<LedgeGrabController>();

        if (_ledgeGrabController == null)
            return;

        if (!_ledgeGrabController.IsOnLedge)
            return;

        Vector3 origin = transform.position + Vector3.up * 1.2f;

        if (showLookingBackCone)
            DrawLookingBackCone(origin);

        if (showLateralCones)
            DrawLateralCones(origin);

        if (showCurrentPosition)
            DrawCurrentPositionOnLedge();
    }

    #endregion

    #region Cone Drawing

    /// Dibuja el cono naranja hacia atrás (zona de LookingBack).
    private void DrawLookingBackCone(Vector3 origin)
    {
        float halfAngle = GetLookingBackHalfAngle();
        Vector3 coneAxis = -transform.forward;

        DrawConeRays(origin, coneAxis, halfAngle, lookingBackConeColor);
        DrawConeEdgeLines(origin, coneAxis, halfAngle, lookingBackConeColor);
        DrawAxisRay(origin, coneAxis, lookingBackConeColor);
    }

    /// Dibuja dos conos púrpura en los laterales (zonas de movimiento lateral).
    private void DrawLateralCones(Vector3 origin)
    {
        float halfAngle = GetLateralHalfAngle();

        DrawConeRays(origin,  transform.right, halfAngle, lateralConeColor);
        DrawConeEdgeLines(origin,  transform.right, halfAngle, lateralConeColor);
        DrawAxisRay(origin,  transform.right, lateralConeColor);

        DrawConeRays(origin, -transform.right, halfAngle, lateralConeColor);
        DrawConeEdgeLines(origin, -transform.right, halfAngle, lateralConeColor);
        DrawAxisRay(origin, -transform.right, lateralConeColor);
    }

    /// Dibuja los rayos del borde del cono distribuidos angularmente alrededor del eje.
    private void DrawConeRays(Vector3 origin, Vector3 axis, float halfAngle, Color color)
    {
        Gizmos.color = color;

        Quaternion baseRotation = Quaternion.LookRotation(axis);

        for (int i = 0; i < coneRayCount; i++)
        {
            float   azimuth     = (360f / coneRayCount) * i;
            Vector3 localDir    = Quaternion.Euler(halfAngle, 0f, 0f) * Vector3.forward;
            Vector3 rotatedDir  = baseRotation * Quaternion.AngleAxis(azimuth, Vector3.forward) * localDir;

            Gizmos.DrawRay(origin, rotatedDir * coneRayLength);
        }
    }

    /// Dibuja las líneas del límite del cono en los ejes up/right/down/left para que sea legible.
    private void DrawConeEdgeLines(Vector3 origin, Vector3 axis, float halfAngle, Color color)
    {
        Gizmos.color = color;

        Quaternion baseRotation = Quaternion.LookRotation(axis);

        Vector3[] edgeDirs = new Vector3[]
        {
            baseRotation * Quaternion.AngleAxis(0f,   Vector3.forward) * (Quaternion.Euler(halfAngle, 0f, 0f) * Vector3.forward),
            baseRotation * Quaternion.AngleAxis(90f,  Vector3.forward) * (Quaternion.Euler(halfAngle, 0f, 0f) * Vector3.forward),
            baseRotation * Quaternion.AngleAxis(180f, Vector3.forward) * (Quaternion.Euler(halfAngle, 0f, 0f) * Vector3.forward),
            baseRotation * Quaternion.AngleAxis(270f, Vector3.forward) * (Quaternion.Euler(halfAngle, 0f, 0f) * Vector3.forward),
        };

        foreach (Vector3 dir in edgeDirs)
            Gizmos.DrawRay(origin, dir * coneRayLength);
    }

    /// Dibuja el rayo central del eje del cono.
    private void DrawAxisRay(Vector3 origin, Vector3 axis, Color color)
    {
        Color axisColor = new Color(color.r, color.g, color.b, 1f);
        Gizmos.color = axisColor;
        Gizmos.DrawRay(origin, axis * coneRayLength);
        Gizmos.DrawWireSphere(origin + axis * coneRayLength, 0.04f);
    }

    #endregion

    #region Current Position On Ledge

    private void DrawCurrentPositionOnLedge()
    {
        LedgeAnchor ledge = _ledgeGrabController.CurrentLedge;

        if (ledge == null)
            return;

        float   t             = _ledgeGrabController.CurrentNormalizedT;
        Vector3 pointOnLedge  = ledge.GetWorldPositionAtNormalizedT(t);

        Gizmos.color = currentPositionColor;
        Gizmos.DrawLine(transform.position, pointOnLedge);
        Gizmos.DrawWireSphere(pointOnLedge, 0.08f);
        Gizmos.DrawSphere(pointOnLedge, 0.03f);

        DrawLabel(pointOnLedge + Vector3.up * 0.15f, $"T = {t:F2}", currentPositionColor);
    }

    #endregion

    #region Angle Accessors

    /// Lee el ángulo del cono trasero directamente desde LedgeGrabController via reflection-free:
    /// expone los valores serializados como propiedades públicas de lectura.
    private float GetLookingBackHalfAngle()
    {
        return _ledgeGrabController.LookingBackConeHalfAngle;
    }

    private float GetLateralHalfAngle()
    {
        return _ledgeGrabController.LateralInputConeHalfAngle;
    }

    #endregion

    #region Label Helper

    private void DrawLabel(Vector3 worldPosition, string text, Color color)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(worldPosition, text);
#endif
    }

    #endregion
}
