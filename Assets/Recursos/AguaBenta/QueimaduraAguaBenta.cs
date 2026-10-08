using System.Collections;
using UnityEngine;

// Efeito no vampiro morto por água benta: o corpo escurece (carbonizado), os olhos apagam e sai vapor.
public class QueimaduraAguaBenta : MonoBehaviour
{
    public Color corQueimada = new Color(0.16f, 0.14f, 0.13f);
    public float duracao = 1.5f;
    public float duracaoDoVapor = 10f;

    private static readonly int idCor = Shader.PropertyToID("_BaseColor");
    private static readonly int idEmissao = Shader.PropertyToID("_EmissionColor");

    private IEnumerator Start()
    {
        CriarVapor();

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        var bloco = new MaterialPropertyBlock();
        for (float t = 0f; t < 1f; t += Time.deltaTime / duracao)
        {
            foreach (Renderer r in renderers)
            {
                if (r is ParticleSystemRenderer || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(idCor)) continue;
                r.GetPropertyBlock(bloco);
                bloco.SetColor(idCor, Color.Lerp(r.sharedMaterial.GetColor(idCor), corQueimada, t));
                bloco.SetColor(idEmissao, Color.Lerp(r.sharedMaterial.GetColor(idEmissao), Color.black, t));
                r.SetPropertyBlock(bloco);
            }
            yield return null;
        }
    }

    private void CriarVapor()
    {
        var prefab = Resources.Load<ParticleSystem>("VaporAguaBenta");
        Animator anim = GetComponent<Animator>();
        if (prefab == null || anim == null) return;

        ParticleSystem vapor = Instantiate(prefab, anim.GetBoneTransform(HumanBodyBones.Chest));
        vapor.transform.localPosition = Vector3.zero;
        var main = vapor.main;
        main.duration = duracaoDoVapor;
        vapor.Play();
        Destroy(vapor.gameObject, duracaoDoVapor + 3f);
    }
}
