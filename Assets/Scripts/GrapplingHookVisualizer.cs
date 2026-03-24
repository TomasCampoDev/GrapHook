using UnityEngine;

[RequireComponent(typeof(GrapplingHookController))]
public class GrapplingHookVisualizer : MonoBehaviour
{
    #region Serialized — Aim

    [Header("Aim Raycast Colors")]
    public Color aimRaycastColorNoTarget = Color.red;
    public Color aimRaycastColorValidHit = Color.green;

    [Header("Aim Assist Rays")]
    public Color aimAssistRayColor = new Color(0.2f, 0.5f, 1f, 1f);   // azul normal
    public Color aimAssistRayHitColor = new Color(0.2f, 1f, 0.4f, 0.6f); // verde suave = hit inválido
    public Color aimAssistRaySelectedColor = Color.cyan;                       // cian = el elegido
    public float aimAssistHitSphereRadius = 0.08f;

    #endregion

    #region Serialized — Cable

    [Header("Hook Cable")]
    public Color hookCableColor = new Color(1f, 0.4f, 0.8f, 1f);

    #endregion

    #region Serialized — Impact

    [Header("Impact Sphere")]
    public Color impactSphereColor = Color.blue;
    public float impactSphereRadius = 0.15f;

    #endregion

    #region Serialized — Arc Circle

    [Header("Arc Circle")]
    public Color arcCircleColor = new Color(0.3f, 0.6f, 1f, 1f);
    public Color verticalArcCircleColor = Color.green;
    public int arcCircleSegments = 48;
    public bool drawFullSphere = false;

    #endregion

    #region Serialized — Perpendicular Lines

    [Header("Perpendicular Limit Lines")]
    public Color perpendicularLinesColor = Color.yellow;
    public float perpendicularLinesLength = 4f;

    #endregion

    #region Private — References

    private GrapplingHookController _hook;

    #endregion

    #region Initialization

    private void Awake()
    {
        _hook = GetComponent<GrapplingHookController>();
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (_hook == null)
            return;

        DrawAimRaycast();
        DrawAimAssistRays();

        if (_hook.hookIsActive)
        {
            DrawHookCable();
            DrawImpactSphere();
            DrawLateralArcCircle();
            DrawVerticalArcCircle();
            DrawPerpendicularLimitLines();
        }
    }

    #endregion

    #region Aim Raycast

    private void DrawAimRaycast()
    {
        bool validHit = _hook.isAimingAtValidSurface;

        Gizmos.color = validHit ? aimRaycastColorValidHit : aimRaycastColorNoTarget;
        Gizmos.DrawLine(_hook.GetAimRaycastOrigin(), _hook.GetAimRaycastEndPoint());

        if (validHit || _hook.AimRaycastHitSomething())
            Gizmos.DrawWireSphere(_hook.GetAimRaycastEndPoint(), 0.05f);
    }

    #endregion

    #region Hook Cable

    private void DrawHookCable()
    {
        Gizmos.color = hookCableColor;
        Gizmos.DrawLine(_hook.GetAimRaycastOrigin(), _hook.hookImpactPoint);
    }

    #endregion

    #region Impact Sphere

    private void DrawImpactSphere()
    {
        Gizmos.color = impactSphereColor;
        Gizmos.DrawSphere(_hook.hookImpactPoint, impactSphereRadius);
    }

    #endregion

    #region Arc Circle

    private void DrawLateralArcCircle()
    {
        Vector3 impactPoint = _hook.hookImpactPoint;
        Vector3 playerPosition = _hook.GetAimRaycastOrigin();
        float radius = _hook.currentDistanceToImpact;

        Vector3 cableDirection = (impactPoint - playerPosition).normalized;

        if (cableDirection == Vector3.zero)
            return;

        Vector3 upProjected = Vector3.ProjectOnPlane(Vector3.up, cableDirection).normalized;

        if (upProjected == Vector3.zero)
            upProjected = Vector3.ProjectOnPlane(Vector3.forward, cableDirection).normalized;

        Vector3 tangentA = upProjected;
        Vector3 tangentB = Vector3.Cross(cableDirection, tangentA).normalized;

        Gizmos.color = arcCircleColor;
        DrawCircleWithTangents(impactPoint, tangentA, tangentB, radius, arcCircleSegments);

        if (drawFullSphere)
            DrawWireSphereFull(impactPoint, radius, arcCircleSegments / 3);
    }

    #endregion

    #region Vertical Arc Circle

    // Círculo vertical: mismo radio que el cable, orientado en el plano
    // que contiene el cable y el eje Up del mundo.
    // Representa la trayectoria del arco vertical (balanceo hacia arriba/abajo).
    private void DrawVerticalArcCircle()
    {
        Vector3 impactPoint = _hook.hookImpactPoint;
        Vector3 playerPosition = _hook.GetAimRaycastOrigin();
        float radius = _hook.currentDistanceToImpact;

        Vector3 cableDirection = (impactPoint - playerPosition).normalized;

        if (cableDirection == Vector3.zero)
            return;

        // Usar la normal fija del controller si está activa, para que el círculo
        // sea consistente con la trayectoria real del balanceo
        Vector3 planeNormal = _hook.IsSwingingVertically
            ? _hook.GetVerticalPlaneNormal()
            : Vector3.Cross(cableDirection, Vector3.up).normalized;

        if (planeNormal == Vector3.zero)
            planeNormal = Vector3.Cross(cableDirection, Vector3.forward).normalized;

        Vector3 tangentA = cableDirection;
        Vector3 tangentB = Vector3.Cross(planeNormal, cableDirection).normalized;

        Gizmos.color = verticalArcCircleColor;
        DrawCircleWithTangents(impactPoint, tangentA, tangentB, radius, arcCircleSegments);
    }

    #endregion

    #region Perpendicular Limit Lines

    private void DrawPerpendicularLimitLines()
    {
        Vector3 impactPoint = _hook.hookImpactPoint;
        Vector3 initialDirection = _hook.GetInitialCableDirection();

        if (initialDirection == Vector3.zero)
            return;

        Vector3 hInitial = new Vector3(initialDirection.x, 0f, initialDirection.z).normalized;

        if (hInitial == Vector3.zero)
            return;

        Vector3 upOnPlane = Vector3.up;
        Vector3 rightOnPlane = Vector3.Cross(hInitial, Vector3.up).normalized;

        Gizmos.color = perpendicularLinesColor;

        Gizmos.DrawLine(impactPoint - upOnPlane * perpendicularLinesLength,
                        impactPoint + upOnPlane * perpendicularLinesLength);
        Gizmos.DrawLine(impactPoint - rightOnPlane * perpendicularLinesLength,
                        impactPoint + rightOnPlane * perpendicularLinesLength);
    }

    #endregion

    #region Geometry Helpers

    private void DrawCircleWithTangents(Vector3 center, Vector3 tangentA, Vector3 tangentB, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 previousPoint = center + tangentA * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 nextPoint = center
                              + tangentA * Mathf.Cos(angle) * radius
                              + tangentB * Mathf.Sin(angle) * radius;

            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }

    private void DrawWireSphereFull(Vector3 center, float radius, int segments)
    {
        DrawCircleWithTangents(center, Vector3.up, Vector3.Cross(Vector3.forward, Vector3.up).normalized, radius, segments);
        DrawCircleWithTangents(center, Vector3.up, Vector3.Cross(Vector3.right, Vector3.up).normalized, radius, segments);
        DrawCircleWithTangents(center, Vector3.forward, Vector3.Cross(Vector3.right, Vector3.forward).normalized, radius, segments);
    }

    #endregion

    #region Aim Assist Rays

    private void DrawAimAssistRays()
    {
        if (!_hook.aimAssistEnabled)
            return;

        var assistRays = _hook.lastAimAssistRays;
        if (assistRays == null || assistRays.Length == 0)
            return;

        foreach (var ray in assistRays)
        {
            if (ray.selected)
            {
                Gizmos.color = aimAssistRaySelectedColor;
                Gizmos.DrawLine(ray.origin, ray.end);
                Gizmos.DrawWireSphere(ray.end, aimAssistHitSphereRadius * 1.5f);
            }
            else if (ray.hitValid)
            {
                Gizmos.color = aimAssistRayHitColor;
                Gizmos.DrawLine(ray.origin, ray.end);
                Gizmos.DrawWireSphere(ray.end, aimAssistHitSphereRadius);
            }
            else
            {
                Gizmos.color = aimAssistRayColor;
                Gizmos.DrawLine(ray.origin, ray.end);
            }
        }
    }

    #endregion


}