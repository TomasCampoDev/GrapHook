using UnityEngine;

/// <summary>
/// Coloca este componente en cada GameObject de borde del nivel.
///
/// El transform define toda la orientación del ledge:
///   transform.forward ? dirección hacia la pared (el personaje mirará aquí)
///   transform.right   ? eje de movimiento lateral a lo largo del borde
///   transform.up      ? arriba del ledge (permite bordes inclinados)
///
/// Requiere dos hijos Transform (LeftEdge y RightEdge) en los extremos del borde.
/// Requiere un BoxCollider en modo Trigger para detectar al jugador.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class LedgeAnchor : MonoBehaviour
{
    #region Inspector

    [Header("Edge Markers")]
    [SerializeField] private Transform leftEdge;
    [SerializeField] private Transform rightEdge;

    [Header("Gameplay")]
    [SerializeField] private bool isClimbable = true;

    [Header("Adjacent Ledges")]
    [SerializeField] private LedgeAnchor nextLedgeToTheRight;
    [SerializeField] private LedgeAnchor nextLedgeToTheLeft;

    #endregion

    #region Public Read-Only Properties

    public bool IsClimbable => isClimbable;
    public LedgeAnchor NextRight => nextLedgeToTheRight;
    public LedgeAnchor NextLeft => nextLedgeToTheLeft;

    #endregion

    #region Spatial Queries

    /// Punto del borde más cercano a una posición en world space.
    /// El resultado vive sobre la línea LeftEdge–RightEdge, sin salirse de los extremos.
    public Vector3 GetClosestPointOnLedge(Vector3 worldPosition)
    {
        Vector3 ledgeStart = leftEdge.position;
        Vector3 ledgeDirection = rightEdge.position - leftEdge.position;
        float ledgeLength = ledgeDirection.magnitude;

        if (ledgeLength < Mathf.Epsilon)
            return ledgeStart;

        Vector3 toPlayer = worldPosition - ledgeStart;
        float projectedDist = Vector3.Dot(toPlayer, ledgeDirection / ledgeLength);
        float clampedDist = Mathf.Clamp(projectedDist, 0f, ledgeLength);

        return ledgeStart + ledgeDirection.normalized * clampedDist;
    }

    /// Convierte un punto en world space a t normalizado [0,1]
    /// donde 0 = LeftEdge y 1 = RightEdge.
    public float GetNormalizedPositionOf(Vector3 worldPoint)
    {
        float ledgeLength = GetLedgeLength();

        if (ledgeLength < Mathf.Epsilon)
            return 0f;

        Vector3 toPoint = worldPoint - leftEdge.position;
        return Mathf.Clamp01(Vector3.Dot(toPoint, GetLedgeAxis()) / ledgeLength);
    }

    /// Punto en world space correspondiente a un t normalizado [0,1].
    public Vector3 GetWorldPositionAtNormalizedT(float normalizedT)
    {
        return Vector3.Lerp(leftEdge.position, rightEdge.position, normalizedT);
    }

    /// Rotación que debe tener el personaje colgado: mira hacia transform.forward.
    public Quaternion GetCharacterHangRotation()
    {
        return Quaternion.LookRotation(transform.forward, transform.up);
    }

    /// Longitud total del borde en world space.
    public float GetLedgeLength()
    {
        return Vector3.Distance(leftEdge.position, rightEdge.position);
    }

    /// Dirección normalizada del eje del borde de izquierda a derecha.
    public Vector3 GetLedgeAxis()
    {
        return (rightEdge.position - leftEdge.position).normalized;
    }

    public bool IsAtRightEdge(float normalizedT) => normalizedT >= 1f - EdgeProximityThreshold;
    public bool IsAtLeftEdge(float normalizedT) => normalizedT <= EdgeProximityThreshold;

    #endregion

    #region Trigger Detection

    private void OnTriggerEnter(Collider other)
    {
        ILedgeGrabbable grabbable = other.GetComponent<ILedgeGrabbable>();
        Debug.Log($"[LedgeAnchor] OnTriggerEnter — collider: {other.name} | grabbable encontrado: {grabbable != null}");
        grabbable?.OnLedgeDetected(this);
    }

    private void OnTriggerStay(Collider other)
    {
        ILedgeGrabbable grabbable = other.GetComponent<ILedgeGrabbable>();
        if (grabbable != null)
            Debug.Log($"[LedgeAnchor] OnTriggerStay — collider: {other.name}");
        grabbable?.OnLedgeDetected(this);
    }

    #endregion

    #region Private

    private const float EdgeProximityThreshold = 0.05f;

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (leftEdge == null || rightEdge == null)
            return;

        DrawLedgeAxis();
        DrawEdgeMarkers();
        DrawAdjacentLedgeConnections();
        DrawWallForwardIndicator();
    }

    private void DrawLedgeAxis()
    {
        Gizmos.color = isClimbable ? Color.green : Color.red;
        Gizmos.DrawLine(leftEdge.position, rightEdge.position);
    }

    private void DrawEdgeMarkers()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(leftEdge.position, 0.08f);
        Gizmos.DrawWireSphere(rightEdge.position, 0.08f);
    }

    private void DrawAdjacentLedgeConnections()
    {
        Vector3 center = Vector3.Lerp(leftEdge.position, rightEdge.position, 0.5f);

        if (nextLedgeToTheRight != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(center, nextLedgeToTheRight.transform.position);
        }

        if (nextLedgeToTheLeft != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(center, nextLedgeToTheLeft.transform.position);
        }
    }

    private void DrawWallForwardIndicator()
    {
        Vector3 center = Vector3.Lerp(leftEdge.position, rightEdge.position, 0.5f);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(center, transform.forward * 0.5f);
    }

    #endregion
}