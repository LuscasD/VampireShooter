using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BloodSplaterSurface : MonoBehaviour
{
    public GameObject bloodDecalHorizontal; // Prefab da poça no chão
    public GameObject bloodDecalVertical;   // Prefab da poça na parede

    private ParticleSystem ps;
    private List<ParticleCollisionEvent> collisionEvents;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        collisionEvents = new List<ParticleCollisionEvent>();
    }

    void OnParticleCollision(GameObject other)
    {
        int numCollisionEvents = ps.GetCollisionEvents(other, collisionEvents);

        for (int i = 0; i < numCollisionEvents; i++)
        {
            Vector3 pos = collisionEvents[i].intersection; // Ponto de impacto
            Vector3 normal = collisionEvents[i].normal;    // Normal da superfície

            // Descobre se é mais "horizontal" ou "vertical"
            float dot = Vector3.Dot(normal.normalized, Vector3.up);

            GameObject prefabToSpawn;

            if (Mathf.Abs(dot) > 0.7f)
            {
                // Superfície horizontal (chão/teto)
                prefabToSpawn = bloodDecalHorizontal;
            }
            else
            {
                // Superfície vertical (parede)
                prefabToSpawn = bloodDecalVertical;
            }

            if (prefabToSpawn != null)
            {
                // Alinha a rotação com a superfície
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);

                GameObject decal = Instantiate(prefabToSpawn, pos, rot);

                // Evita z-fighting
                decal.transform.position += normal * 0.01f;

            }
        }
    }
}
