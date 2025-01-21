using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyOnTime : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(destroyOnTime());
    }

    IEnumerator destroyOnTime()
    {
        yield return new WaitForSeconds(30);
        Destroy(gameObject);
    }
}
