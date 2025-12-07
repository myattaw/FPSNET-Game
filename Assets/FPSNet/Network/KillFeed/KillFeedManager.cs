using UnityEngine;
using UnityEngine.UI;

namespace FPSNet.Network.KillFeed
{
    public class KillFeedManager : MonoBehaviour
    {
        public static KillFeedManager Instance;

        [Header("References")]
        public GameObject killFeedItemPrefab;
        public Transform killFeedHolder;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void ShowKill(string attackerName, string victimName)
        {
            if (killFeedItemPrefab == null || killFeedHolder == null) return;

            var item = Instantiate(killFeedItemPrefab, killFeedHolder, false);
            var text = item.GetComponentInChildren<TMPro.TMP_Text>();
            text.text = $"{attackerName} killed {victimName}";

            if (item.TryGetComponent<RectTransform>(out var rt))
            {
                rt.localScale = Vector3.one;
                rt.anchoredPosition = Vector2.zero;
            }

            var anim = item.GetComponent<KillFeedAnimation>();
            if (anim != null)
                anim.Play();
            else
                Destroy(item, 5f);

            if (killFeedHolder is RectTransform holderRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(holderRect);
        }
    }
}