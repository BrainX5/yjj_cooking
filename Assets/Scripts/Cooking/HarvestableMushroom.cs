using System.Collections;
using UnityEngine;

public class HarvestableMushroom : MonoBehaviour
{
    [SerializeField] private float highlightScaleMultiplier = 1.18f;
    [SerializeField] private float pickupDuration = 0.18f;
    [SerializeField] private float pickupRiseDistance = 0.22f;

    private Collider[] cachedColliders;
    private Vector3 baseScale;
    private Vector3 basePosition;
    private bool selected;
    private bool picked;

    public bool IsPicked => picked;

    private void Awake()
    {
        baseScale = transform.localScale;
        basePosition = transform.localPosition;
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    public void SetSelected(bool isSelected)
    {
        if (selected == isSelected)
        {
            return;
        }

        if (picked && isSelected)
        {
            return;
        }

        selected = isSelected;
        transform.localScale = selected
            ? baseScale * highlightScaleMultiplier
            : baseScale;
    }

    public void SetIndicatorVisible(bool isVisible)
    {
    }

    public void FaceIndicatorTo(Camera targetCamera)
    {
    }

    public bool TryPick()
    {
        if (picked)
        {
            return false;
        }

        picked = true;
        SetSelected(false);
        CookingAudioController.Instance?.PlayPickup();

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        for (var i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = false;
            }
        }

        StartCoroutine(PlayPickupRoutine());
        return true;
    }

    private IEnumerator PlayPickupRoutine()
    {
        var elapsed = 0f;
        var startScale = transform.localScale;
        var startPosition = transform.localPosition;
        var targetPosition = startPosition + Vector3.up * pickupRiseDistance;

        while (elapsed < pickupDuration)
        {
            elapsed += Time.deltaTime;
            var normalized = Mathf.Clamp01(elapsed / pickupDuration);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, normalized);
            transform.localPosition = Vector3.Lerp(startPosition, targetPosition, normalized);
            yield return null;
        }

        gameObject.SetActive(false);
        transform.localScale = baseScale;
        transform.localPosition = basePosition;
    }
}
