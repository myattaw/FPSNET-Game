using UnityEngine;
using UnityEngine.UI;

public class HitEffectFade : MonoBehaviour
{
    [Header("Fade Settings")]
    [Range(0f, 1f)] public float startAlpha = 1f;
    [Range(0f, 1f)] public float endAlpha = 0f;
    public float fadeDuration = 0.5f;

    private Image img;
    private float timer;

    void Start()
    {
        img = GetComponent<Image>();
        timer = 0f; // Timer counts up

        // Set initial alpha
        Color c = img.color;
        c.a = startAlpha;
        img.color = c;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = timer / fadeDuration;

        // Lerp alpha from start → end
        float a = Mathf.Lerp(startAlpha, endAlpha, t);

        Color c = img.color;
        c.a = a;
        img.color = c;

        if (timer >= fadeDuration)
        {
            Destroy(gameObject);
        }
    }
}