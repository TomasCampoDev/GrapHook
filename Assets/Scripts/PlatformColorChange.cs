using UnityEngine;
using UnityEngine.SceneManagement;

public class PlatformColorChange : MonoBehaviour
{
    [SerializeField] private DissolveController dissolveController;
    [SerializeField] private Color activatedColor = Color.green;

    private bool isGreen = false;

    private void Awake()
    {
        dissolveController = GetComponent<DissolveController>();
        if (dissolveController == null)
        {
            dissolveController = transform.parent.GetComponent<DissolveController>();

        }
    }

    private void Update()
    {
        //    if(isGreen) return;
        //    bool playerIsOnPlatform = GetComponentsInChildren<PlayerController>().Length > 0;
        //
        //    if (playerIsOnPlatform && !isGreen)
        //    {
        //        isGreen = true;
        //        StartCoroutine(dissolveController.ChangeColorWithDissolve(activatedColor));
        //    }
    }
    public void ChangeColorOfPlatformFromPlayerSignal()
    {
        if (isGreen) return;
        if (!isGreen)
        {
            StartCoroutine(dissolveController.ChangeColorWithDissolve(activatedColor));
            ScoreManager.Instance?.AddScore();
            isGreen = true;
        }


    }
}