using UnityEngine;

public class ZeroGravityManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject startPanel;

    [Header("Floating Objects")]
    public GameObject floatingGroup;

    [Header("Effects")]
    public AudioSource windAudio;

    public ParticleSystem windParticle;

    private bool started = false;

    public void StartZeroGravity()
    {
        if(started) return;

        started = true;

        Debug.Log("进入失重模式");

        // 关闭提示框
        startPanel.SetActive(false);

        // 开启漂浮物
        floatingGroup.SetActive(true);

        // 风声
        if(windAudio != null)
        {
            windAudio.Play();
        }

        // 粒子
        if(windParticle != null)
        {
            windParticle.Play();
        }
    }
}