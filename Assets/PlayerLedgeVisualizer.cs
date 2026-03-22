using UnityEngine;

/// <summary>
/// Añade este componente al mismo GameObject que LedgeGrabController.
/// Solo dibuja gizmos en el editor, cero coste en build.
///
/// Muestra en todo momento:
///   - El trigger de detección de cada LedgeAnchor en escena
///   - El eje del borde y sus extremos (verde = climbable, rojo = not climbable)
///   - La dirección hacia la pared (forward del ledge)
///   - Las conexiones entre ledges adyacentes
///   - El punto de snap (donde irá el personaje al agarrarse)
///   - El punto de aterrizaje del climb (donde aterrizará al trepar)
///   - El estado actual del jugador (ledge activo, T normalizada)
/// </summary>
public class PlayerLedgeVisualizer : MonoBehaviour
{
    #region Inspector

    [Header("Secciones visibles")]
    [SerializeField] private bool showAllLedgesInScene = true;
    [SerializeField] private bool showSnapPreview = true;
    [SerializeField] private bool showClimbDestination = true;
    [SerializeField] private bool showAdjacentLinks = true;
    [SerializeField] private bool showTriggerBox = true;
    [SerializeField] private bool showPlayerState = true;

    [Header("Tamaños")]
    [SerializeField] private float edgeMarkerRadius = 0.08f;
    [SerializeField] private float snapMarkerRadius = 0.1f;
    [SerializeField] private float climbMarkerRadius = 0.12f;
    [SerializeField] private float forwardArrowLength = 0.6f;

    #endregion

    #region Colors

    private static readonly Color ColorClimbable = new Color(0.2f, 1f, 0.2f, 1f);
    private static readonly Color ColorNotClimbable = new Color(1f, 0.2f, 0.2f, 1f);
    private static readonly Color ColorEdgeMarker = new Color(1f, 0.9f, 0f, 1f);
    private static readonly Color ColorWallForward = new Color(0.2f, 0.4f, 1f, 1f);
    private static readonly Color ColorSnap = new Color(0f, 1f, 1f, 1f);
    private static readonly Color ColorSnapLine = new Color(0f, 1f, 1f, 0.35f);
    private static readonly Color ColorClimbDest = new Color(1f, 0.6f, 0f, 1f);
    private static readonly Color ColorClimbDestLine = new Color(1f, 0.6f, 0f, 0.35f);
    private static readonly Color ColorAdjacentRight = new Color(0f, 1f, 1f, 0.7f);
    private static readonly Color ColorAdjacentLeft = new Color(1f, 0f, 1f, 0.7f);
    private static readonly Color ColorTriggerFill = new Color(1f, 1f, 0f, 0.1f);
    private static readonly Color ColorTriggerWire = new Color(1f, 1f, 0f, 0.55f);
    private static readonly Color ColorPlayerOnLedge = new Color(0f, 1f, 0.5f, 1f);
    private static readonly Color ColorCurrentT = new Color(1f, 0.5f, 0f, 1f);

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

        if (showAllLedgesInScene)
            DrawAllLedgesInScene();

        if (showPlayerState)
            DrawPlayerState();
    }

    private void DrawAllLedgesInScene()
    {
        LedgeAnchor[] allLedges = FindObjectsByType<LedgeAnchor>(FindObjectsSortMode.None);

        foreach (LedgeAnchor ledge in allLedges)
            DrawSingleLedge(ledge);
    }

    private void DrawSingleLedge(LedgeAnchor ledge)
    {
        if (!LedgeHasValidEdges(ledge))
        {
            DrawMissingEdgesWarning(ledge);
            return;
        }

        bool isActive = _ledgeGrabController != null
                        && _ledgeGrabController.CurrentLedge == ledge;

        DrawLedgeAxis(ledge, isActive);
        DrawEdgeMarkers(ledge);
        DrawWallForwardArrow(ledge);

        if (showSnapPreview)
            DrawSnapPreview(ledge);

        if (showClimbDestination && ledge.IsClimbable)
            DrawClimbDestinationPreview(ledge);

        if (showAdjacentLinks)
            DrawAdjacentLinks(ledge);

        if (showTriggerBox)
            DrawTriggerBox(ledge);
    }

    // ?? Eje del borde ?????????????????????????????????????????????????????????

    private void DrawLedgeAxis(LedgeAnchor ledge, bool isActive)
    {
        Color color = isActive ? Color.white
                    : ledge.IsClimbable ? ColorClimbable
                    : ColorNotClimbable;

        Gizmos.color = color;
        Gizmos.DrawLine(
            ledge.GetWorldPositionAtNormalizedT(0f),
            ledge.GetWorldPositionAtNormalizedT(1f)
        );

        string label = ledge.IsClimbable ? "CLIMBABLE" : "NOT CLIMBABLE";
        Vector3 labelPos = ledge.GetWorldPositionAtNormalizedT(0.5f) + Vector3.up * 0.18f;
        DrawLabel(labelPos, label, color);
    }

    // ?? Extremos L / R ????????????????????????????????????????????????????????

    private void DrawEdgeMarkers(LedgeAnchor ledge)
    {
        Gizmos.color = ColorEdgeMarker;

        Vector3 left = ledge.GetWorldPositionAtNormalizedT(0f);
        Vector3 right = ledge.GetWorldPositionAtNormalizedT(1f);

        Gizmos.DrawWireSphere(left, edgeMarkerRadius);
        Gizmos.DrawWireSphere(right, edgeMarkerRadius);

        DrawLabel(left + Vector3.up * 0.12f, "L", ColorEdgeMarker);
        DrawLabel(right + Vector3.up * 0.12f, "R", ColorEdgeMarker);
    }

    // ?? Flecha hacia la pared ?????????????????????????????????????????????????

    private void DrawWallForwardArrow(LedgeAnchor ledge)
    {
        Vector3 center = ledge.GetWorldPositionAtNormalizedT(0.5f);

        Gizmos.color = ColorWallForward;
        Gizmos.DrawRay(center, ledge.transform.forward * forwardArrowLength);
        Gizmos.DrawWireSphere(center + ledge.transform.forward * forwardArrowLength, 0.04f);
    }

    // ?? Punto de snap (donde irá el personaje al agarrarse) ???????????????????

    private void DrawSnapPreview(LedgeAnchor ledge)
    {
        LedgeGrabController grabController = _ledgeGrabController != null
            ? _ledgeGrabController
            : FindFirstObjectByType<LedgeGrabController>();

        if (grabController == null)
            return;

        Vector3 closestPoint = ledge.GetClosestPointOnLedge(transform.position);
        Quaternion hangRotation = ledge.GetCharacterHangRotation();

        // Accedemos al offset via reflection-free: el visualizador tiene su propia copia serializada
        // para no acoplar al controlador. Pero como están en el mismo GO, lo leemos directamente.
        Vector3 snapOffset = grabController.GetSnapOffsetForPreview();
        Vector3 snapPosition = closestPoint + hangRotation * snapOffset;

        Gizmos.color = ColorSnapLine;
        Gizmos.DrawLine(closestPoint, snapPosition);

        Gizmos.color = ColorSnap;
        Gizmos.DrawWireSphere(snapPosition, snapMarkerRadius);
    }

    // ?? Punto de aterrizaje del climb ?????????????????????????????????????????

    private void DrawClimbDestinationPreview(LedgeAnchor ledge)
    {
        LedgeGrabController grabController = _ledgeGrabController != null
            ? _ledgeGrabController
            : FindFirstObjectByType<LedgeGrabController>();

        if (grabController == null)
            return;

        // Solo tiene sentido mostrarlo en el ledge activo — en los demás
        // no sabemos exactamente desde qué posición treparía el jugador.
        bool isActiveLedge = grabController.CurrentLedge == ledge;

        if (!isActiveLedge && Application.isPlaying)
            return;

        // En edición lo mostramos siempre usando la posición actual del personaje
        // para que puedas ajustar los offsets sin entrar en Play.
        Vector3 climbDestination = grabController.CalculateClimbDestination();

        Gizmos.color = ColorClimbDestLine;
        Gizmos.DrawLine(transform.position, climbDestination);

        Gizmos.color = ColorClimbDest;
        Gizmos.DrawWireSphere(climbDestination, climbMarkerRadius);
        Gizmos.DrawSphere(climbDestination, climbMarkerRadius * 0.4f);

        DrawLabel(climbDestination + Vector3.up * 0.15f, "CLIMB TARGET", ColorClimbDest);
    }

    // ?? Conexiones a ledges adyacentes ????????????????????????????????????????

    private void DrawAdjacentLinks(LedgeAnchor ledge)
    {
        Vector3 center = ledge.GetWorldPositionAtNormalizedT(0.5f);

        if (ledge.NextRight != null)
        {
            Gizmos.color = ColorAdjacentRight;
            Gizmos.DrawLine(center, ledge.NextRight.GetWorldPositionAtNormalizedT(0.5f));
            DrawLabel(Vector3.Lerp(center, ledge.NextRight.transform.position, 0.5f), "?", ColorAdjacentRight);
        }

        if (ledge.NextLeft != null)
        {
            Gizmos.color = ColorAdjacentLeft;
            Gizmos.DrawLine(center, ledge.NextLeft.GetWorldPositionAtNormalizedT(0.5f));
            DrawLabel(Vector3.Lerp(center, ledge.NextLeft.transform.position, 0.5f), "?", ColorAdjacentLeft);
        }
    }

    // ?? Caja trigger del BoxCollider ??????????????????????????????????????????

    private void DrawTriggerBox(LedgeAnchor ledge)
    {
        BoxCollider box = ledge.GetComponent<BoxCollider>();

        if (box == null)
        {
            DrawLabel(ledge.transform.position + Vector3.up * 0.35f, "? SIN BOXCOLLIDER", Color.red);
            return;
        }

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = ledge.transform.localToWorldMatrix;

        Gizmos.color = ColorTriggerFill;
        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = ColorTriggerWire;
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = prev;

        if (!box.isTrigger)
            DrawLabel(ledge.transform.TransformPoint(box.center) + Vector3.up * 0.35f,
                      "? NO ES TRIGGER", Color.red);
    }

    // ?? Estado del jugador en el ledge activo ?????????????????????????????????

    private void DrawPlayerState()
    {
        if (_ledgeGrabController == null || !_ledgeGrabController.IsOnLedge)
            return;

        LedgeAnchor current = _ledgeGrabController.CurrentLedge;
        if (current == null)
            return;

        float t = _ledgeGrabController.CurrentNormalizedT;
        Vector3 pointOnLedge = current.GetWorldPositionAtNormalizedT(t);

        Gizmos.color = ColorPlayerOnLedge;
        Gizmos.DrawLine(transform.position, pointOnLedge);
        Gizmos.DrawWireSphere(pointOnLedge, 0.1f);

        DrawLabel(pointOnLedge + Vector3.up * 0.2f, $"T = {t:F2}", ColorCurrentT);

        Color stateColor = current.IsClimbable ? ColorClimbable : ColorNotClimbable;
        string stateText = current.IsClimbable ? "ON LEDGE — CLIMBABLE" : "ON LEDGE — NOT CLIMBABLE";
        DrawLabel(transform.position + Vector3.up * 2.3f, stateText, stateColor);
    }

    #endregion

    #region Helpers

    private bool LedgeHasValidEdges(LedgeAnchor ledge)
    {
        try
        {
            ledge.GetWorldPositionAtNormalizedT(0f);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void DrawMissingEdgesWarning(LedgeAnchor ledge)
    {
        DrawLabel(ledge.transform.position + Vector3.up * 0.3f,
                  "? FALTA LeftEdge o RightEdge", Color.red);
    }

    private void DrawLabel(Vector3 worldPosition, string text, Color color)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(worldPosition, text);
#endif
    }

    #endregion
}