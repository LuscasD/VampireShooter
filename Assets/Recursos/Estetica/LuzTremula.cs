using UnityEngine;

// Faz a luz tremular como a chama de um lampião: varia a intensidade e mexe um pouco a posição
// (as sombras "dançam"). Se tiver uma chama/vidro com emissão, ela pisca junto.
[RequireComponent(typeof(Light))]
public class LuzTremula : MonoBehaviour
{
    [Range(0f, 1f)] public float variacao = 0.2f;
    public float velocidade = 7f;
    public float tremidoPosicao = 0.03f;
    public Renderer chama;

    private Light luz;
    private float intensidadeBase;
    private Vector3 posicaoBase;
    private float semente;
    private MaterialPropertyBlock bloco;
    private Color emissaoBase;
    private static readonly int idEmissao = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        luz = GetComponent<Light>();
        intensidadeBase = luz.intensity;
        posicaoBase = transform.localPosition;
        semente = Random.value * 100f;

        if (chama != null)
        {
            bloco = new MaterialPropertyBlock();
            emissaoBase = chama.sharedMaterial.GetColor(idEmissao);
        }
    }

    private void Update()
    {
        float t = Time.time * velocidade + semente;
        float ruido = Mathf.PerlinNoise(t, semente) * 0.7f + Mathf.PerlinNoise(t * 2.7f, semente + 7f) * 0.3f;
        float fator = Mathf.Lerp(1f - variacao, 1f + variacao, ruido);

        luz.intensity = intensidadeBase * fator;
        transform.localPosition = posicaoBase + new Vector3(
            Mathf.PerlinNoise(t * 0.8f, semente + 3f) - 0.5f,
            Mathf.PerlinNoise(t * 0.8f, semente + 9f) - 0.5f,
            Mathf.PerlinNoise(t * 0.8f, semente + 13f) - 0.5f) * tremidoPosicao;

        if (chama != null)
        {
            bloco.SetColor(idEmissao, emissaoBase * fator);
            chama.SetPropertyBlock(bloco);
        }
    }
}
