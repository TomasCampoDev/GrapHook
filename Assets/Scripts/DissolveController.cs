using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla el efecto de disolución para hacer aparecer/desaparecer objetos.
/// Soporta meshes (SkinnedMeshRenderer / MeshRenderer) y ParticleSystems.
/// Si el objeto tiene un ParticleSystem, toda la lógica de mesh se omite.
/// </summary>
public class DissolveController : MonoBehaviour
{
    [SerializeField] private float dissolveDuration = 1f;

    // --- Modo Mesh ---
    public List<Material> dissolveMaterials = new List<Material>();
    public List<Material> standardMaterials = new List<Material>();
    public Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
    public Dictionary<Material, Color> originalBaseColors = new Dictionary<Material, Color>();
    private bool isMeshInitialized = false;
    private bool meshMaterialsInstanced = false;

    // --- Modo Particle System ---
    private bool isParticleSystem = false;
    private Material particleMaterial;
    private ParticleSystemRenderer particleSystemRenderer;

    // ?????????????????????????????????????????????
    //  INICIALIZACIÓN
    // ?????????????????????????????????????????????

    private void Awake()
    {
        isParticleSystem = GetComponentInChildren<ParticleSystem>(true) != null;

        if (isParticleSystem)
        {
            InitializeParticleSystem();
        }
        else
        {
            CreateMeshMaterialInstances();
            InitializeMeshMaterials();
            SaveMeshMaterialColors();
        }

        SetInitialInvisibleState();
    }

    // ?????????????????????????????????????????????
    //  PARTICLE SYSTEM
    // ?????????????????????????????????????????????

    private void InitializeParticleSystem()
    {
        particleSystemRenderer = GetComponentInChildren<ParticleSystemRenderer>(true);

        if (particleSystemRenderer == null)
        {
            Debug.LogError($"DissolveController en '{gameObject.name}': se detectó ParticleSystem pero no se encontró ParticleSystemRenderer.");
            return;
        }

        // Instanciar el material para no modificar el asset compartido
        particleMaterial = new Material(particleSystemRenderer.sharedMaterial);
        particleSystemRenderer.material = particleMaterial;

        Debug.Log($"DissolveController inicializado en '{gameObject.name}' en modo ParticleSystem: material '{particleMaterial.name}'.");
    }

    private void SetInitialInvisibleState()
    {
        if (isParticleSystem)
        {
            particleMaterial?.SetFloat("_DissolveAmount", 1f);
        }
        else
        {
            SetDissolveAmountOnMaterials(1f, dissolveMaterials);
            SetDissolveAmountOnMaterials(1f, standardMaterials);
        }
    }

    // ?????????????????????????????????????????????
    //  APPEAR / DISAPPEAR
    // ?????????????????????????????????????????????

    public IEnumerator AppearWithDissolve()
    {
        if (isParticleSystem)
        {
            yield return LerpParticleDissolveAmount(1f, 0f);
        }
        else
        {
            yield return AppearMeshWithDissolve();
        }
    }

    public IEnumerator DisappearWithDissolve()
    {
        if (isParticleSystem)
        {
            yield return LerpParticleDissolveAmount(0f, 1f);
        }
        else
        {
            yield return DisappearMeshWithDissolve();
        }
    }

    // ?????????????????????????????????????????????
    //  PARTICLE LERP
    // ?????????????????????????????????????????????

    private IEnumerator LerpParticleDissolveAmount(float fromValue, float toValue)
    {
        if (particleMaterial == null)
        {
            Debug.LogError($"DissolveController en '{gameObject.name}': particleMaterial es null.");
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < dissolveDuration)
        {
            elapsedTime += Time.deltaTime;
            float currentValue = Mathf.Lerp(fromValue, toValue, elapsedTime / dissolveDuration);
            particleMaterial.SetFloat("_DissolveAmount", currentValue);
            yield return null;
        }

        particleMaterial.SetFloat("_DissolveAmount", toValue);
    }

    // ?????????????????????????????????????????????
    //  MESH APPEAR / DISAPPEAR
    // ?????????????????????????????????????????????

    private IEnumerator AppearMeshWithDissolve()
    {
        if (!isMeshInitialized) InitializeMeshMaterials();

        if (dissolveMaterials.Count == 0)
        {
            Debug.LogWarning($"No se encontraron materiales Dissolve en '{gameObject.name}'. El objeto aparecerá instantáneamente.");
            SetDissolveAmountOnMaterials(0f, standardMaterials);
            yield break;
        }

        SetDissolveAmountOnMaterials(1f, standardMaterials);
        SetDissolveAmountOnMaterials(1f, dissolveMaterials);

        yield return null;

        yield return LerpMeshDissolveAmount(1f, 0f, standardMaterials);
    }

    private IEnumerator DisappearMeshWithDissolve()
    {
        if (!isMeshInitialized) InitializeMeshMaterials();

        if (dissolveMaterials.Count == 0)
        {
            Debug.LogWarning($"No se encontraron materiales Dissolve en '{gameObject.name}'. El objeto desaparecerá instantáneamente.");
            SetDissolveAmountOnMaterials(1f, standardMaterials);
            yield break;
        }

        List<Material> allMeshMaterials = new List<Material>(dissolveMaterials);
        allMeshMaterials.AddRange(standardMaterials);

        yield return LerpMeshDissolveAmount(0f, 1f, allMeshMaterials);
    }

    // ?????????????????????????????????????????????
    //  MESH LERP
    // ?????????????????????????????????????????????

    private IEnumerator LerpMeshDissolveAmount(float fromValue, float toValue, List<Material> targetMaterials)
    {
        float elapsedTime = 0f;

        while (elapsedTime < dissolveDuration)
        {
            elapsedTime += Time.deltaTime;
            float currentValue = Mathf.Lerp(fromValue, toValue, elapsedTime / dissolveDuration);
            SetDissolveAmountOnMaterials(currentValue, targetMaterials);
            yield return null;
        }

        SetDissolveAmountOnMaterials(toValue, targetMaterials);
    }

    private void SetDissolveAmountOnMaterials(float dissolveAmount, List<Material> targetMaterials)
    {
        foreach (Material mat in targetMaterials)
        {
            if (mat != null)
                mat.SetFloat("_DissolveAmount", dissolveAmount);
        }
    }

    // ?????????????????????????????????????????????
    //  MESH INICIALIZACIÓN
    // ?????????????????????????????????????????????

    private void CreateMeshMaterialInstances()
    {
        if (meshMaterialsInstanced) return;

        foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            CreateInstancesForRenderer(renderer);

        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            CreateInstancesForRenderer(renderer);

        meshMaterialsInstanced = true;

        if (dissolveMaterials.Count == 0 && standardMaterials.Count == 0)
            Debug.LogWarning($"DissolveController en '{gameObject.name}' no encontró ningún material de mesh.");
    }

    private void CreateInstancesForRenderer(Renderer renderer)
    {
        if (renderer == null) return;

        Material[] originalMaterials = renderer.sharedMaterials;
        Material[] instancedMaterials = new Material[originalMaterials.Length];

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            if (originalMaterials[i] != null)
                instancedMaterials[i] = new Material(originalMaterials[i]);
        }

        renderer.materials = instancedMaterials;
    }

    public void Initialize()
    {
        if (isMeshInitialized) return;
        dissolveMaterials.Clear();
        standardMaterials.Clear();
        InitializeMeshMaterials();
    }

    private void InitializeMeshMaterials()
    {
        foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            ClassifyMeshMaterials(renderer.materials);

        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            ClassifyMeshMaterials(renderer.materials);

        isMeshInitialized = true;

        Debug.Log($"DissolveController inicializado en '{gameObject.name}': {dissolveMaterials.Count} materiales Dissolve, {standardMaterials.Count} materiales estándar.");
    }

    private void ClassifyMeshMaterials(Material[] materials)
    {
        foreach (Material mat in materials)
        {
            if (mat == null) continue;

            bool isDissolvemat = mat.name.Contains("Dissolve") || mat.name.Contains("dissolve");

            if (isDissolvemat)
            {
                if (!dissolveMaterials.Contains(mat))
                    dissolveMaterials.Add(mat);
            }
            else
            {
                if (!standardMaterials.Contains(mat))
                    standardMaterials.Add(mat);
            }
        }
    }

    private void SaveMeshMaterialColors()
    {
        originalColors.Clear();
        originalBaseColors.Clear();

        foreach (Material mat in dissolveMaterials)
        {
            if (mat != null)
            {
                originalColors[mat] = mat.GetColor("_Color");
                originalBaseColors[mat] = mat.GetColor("_ToonColor");
            }
        }
    }

    // ?????????????????????????????????????????????
    //  UTILIDADES PÚBLICAS
    // ?????????????????????????????????????????????

    public void SetDissolveLerpDuration(float duration)
    {
        dissolveDuration = duration;
    }

    public void SetStandardMaterialsColor(Color color)
    {
        foreach (Material mat in standardMaterials)
        {
            if (mat != null)
                mat.SetColor("_ToonColor", color);
        }
    }

    public IEnumerator ChangeColorWithDissolve(Color newColor)
    {
        float previousDissolveDuration = dissolveDuration;
        SetDissolveLerpDuration(0.5f);

        foreach (Material mat in dissolveMaterials)
        {
            if (mat != null)
            {
                mat.SetColor("_Color", newColor);
                mat.SetColor("_ToonColor", newColor);
            }
        }

        SetDissolveAmountOnMaterials(0f, dissolveMaterials);
        yield return LerpMeshDissolveAmount(0f, 1f, standardMaterials);

        foreach (Material mat in standardMaterials)
        {
            if (mat != null)
            {
                mat.SetColor("_Color", newColor);
                mat.SetColor("_ToonColor", newColor);
            }
        }

        SetDissolveLerpDuration(previousDissolveDuration);
    }

    public void SetParticleMaterialColor(string colorProperty, Color newColor)
    {
        if (!isParticleSystem)
        {
            Debug.LogWarning($"DissolveController en '{gameObject.name}': SetParticleMaterialColor llamado en modo Mesh.");
            return;
        }

        if (colorLerpCoroutine != null) StopCoroutine(colorLerpCoroutine);
        colorLerpCoroutine = StartCoroutine(LerpParticleMaterialColor(colorProperty, newColor));
    }

    private Coroutine colorLerpCoroutine;

    private IEnumerator LerpParticleMaterialColor(string colorProperty, Color targetColor)
    {
        Color startColor = particleMaterial.GetColor(colorProperty);
        float elapsedTime = 0f;

        while (elapsedTime < (dissolveDuration / 2))
        {
            elapsedTime += Time.deltaTime;
            Color currentColor = Color.Lerp(startColor, targetColor, elapsedTime / (dissolveDuration / 2));
            particleMaterial.SetColor(colorProperty, currentColor);
            yield return null;
        }

        particleMaterial.SetColor(colorProperty, targetColor);
    }

    // ?????????????????????????????????????????????
    //  LIMPIEZA
    // ?????????????????????????????????????????????

    private void OnDestroy()
    {
        if (isParticleSystem)
        {
            if (particleMaterial != null) Destroy(particleMaterial);
        }
        else
        {
            foreach (Material mat in dissolveMaterials)
                if (mat != null) Destroy(mat);

            foreach (Material mat in standardMaterials)
                if (mat != null) Destroy(mat);

            dissolveMaterials.Clear();
            standardMaterials.Clear();
        }
    }

    // ?????????????????????????????????????????????
    //  DEBUG
    // ?????????????????????????????????????????????

    [ContextMenu("Debug: Show Materials")]
    private void DebugShowMaterials()
    {
        Debug.Log($"=== DissolveController en '{gameObject.name}' | Modo: {(isParticleSystem ? "ParticleSystem" : "Mesh")} ===");

        if (isParticleSystem)
        {
            Debug.Log($"Particle Material: {(particleMaterial != null ? particleMaterial.name : "NULL")}");
            return;
        }

        Debug.Log($"Dissolve Materials ({dissolveMaterials.Count}):");
        foreach (Material mat in dissolveMaterials)
            Debug.Log($"  - {mat.name} (ID: {mat.GetInstanceID()})");

        Debug.Log($"Standard Materials ({standardMaterials.Count}):");
        foreach (Material mat in standardMaterials)
            Debug.Log($"  - {mat.name} (ID: {mat.GetInstanceID()})");
    }
}