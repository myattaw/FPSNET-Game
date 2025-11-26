using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    
    public static AudioManager instance;
    
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
    
    public void PlaySound(AudioClip clip, float volume = 1.0f)
    {
        StartCoroutine(PlaySoundCoroutine(clip, volume));
    }
    
    IEnumerator PlaySoundCoroutine(AudioClip clip, float volume)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.Play();
        
        yield return new WaitForSeconds(clip.length);
        Destroy(source);
    }
 
}
