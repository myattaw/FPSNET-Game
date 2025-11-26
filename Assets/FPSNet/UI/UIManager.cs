using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    public static UIManager instance;
    public GameObject hitUI;
    public TextMeshProUGUI ammoText;
    
    public Image healthBar;
    public TextMeshProUGUI healthText;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InstantiateHitUI()
    {
        Instantiate(hitUI, transform);
    }
    
    public void UpdateHealthBar(int health)
    {
        healthBar.fillAmount = health / 100f;
        healthText.text = health.ToString();
    }
    
}
