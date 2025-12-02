using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FPSNet.Network.KillFeed
{
    public class KillFeedAnimation : MonoBehaviour
    {
        [Header("Animation Settings")] 
        public float slideDuration = 0.25f;
        public float fadeOutDelay = 3.5f;
        public float fadeOutDuration = 0.5f;

        private RectTransform rect;
        private CanvasGroup canvasGroup;

        private Vector2 startPos;
        private Vector2 endPos;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();

            // Add a CanvasGroup if missing
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Play()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)rect.parent);

            // Now Unity finally knows the true final anchoredPosition
            endPos = rect.anchoredPosition;
            startPos = endPos + new Vector2(200f, 0f);

            rect.anchoredPosition = startPos;
            canvasGroup.alpha = 0f;

            StartCoroutine(AnimationRoutine());
        }

        private IEnumerator AnimationRoutine()
        {
            float t = 0f;
            while (t < slideDuration)
            {
                t += Time.deltaTime;
                float p = t / slideDuration;

                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
                canvasGroup.alpha = p;

                yield return null;
            }

            yield return new WaitForSeconds(fadeOutDelay);

            t = 0f;
            float startAlpha = canvasGroup.alpha;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                float p = t / fadeOutDuration;

                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}