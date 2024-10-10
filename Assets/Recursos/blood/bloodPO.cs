using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;
using UnityEngine.VFX;

public class bloodPO : MonoBehaviour
{
    ParticleSystemRenderer particle;

    // Start is called before the first frame update
    void Start()
    {
        particle = GetComponent<ParticleSystemRenderer>();
    }

    private void OnParticleCollision(GameObject other)
    {
        if (other.gameObject.tag == "wall")
        {
            particle.renderMode = ParticleSystemRenderMode.Billboard;
        }

        if (other.gameObject.tag == "cenario")
        {
            particle.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        }
    }

}
