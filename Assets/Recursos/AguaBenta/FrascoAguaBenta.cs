using UnityEngine;

// Frasco arremessado: quebra ao bater em qualquer coisa e cria a explosão de água benta.
[RequireComponent(typeof(Rigidbody))]
public class FrascoAguaBenta : MonoBehaviour
{
    public float tempoMaximo = 6f;

    private ExplosaoAguaBenta prefabExplosao;
    private ConfigExplosao config;
    private bool quebrou;

    public void Lancar(Vector3 velocidade, ExplosaoAguaBenta explosao, ConfigExplosao configuracao, Collider[] ignorar)
    {
        prefabExplosao = explosao;
        config = configuracao;

        Collider meu = GetComponent<Collider>();
        foreach (Collider c in ignorar) Physics.IgnoreCollision(meu, c);

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.velocity = velocidade;
        rb.angularVelocity = Random.insideUnitSphere * 10f;
        Invoke(nameof(QuebrarNoAr), tempoMaximo);
    }

    private void OnCollisionEnter(Collision colisao)
    {
        ContactPoint contato = colisao.GetContact(0);
        // Afasta um pouco da superfície para os raios de oclusão não começarem dentro dela
        Quebrar(contato.point + contato.normal * 0.15f);
    }

    private void QuebrarNoAr()
    {
        Quebrar(transform.position);
    }

    private void Quebrar(Vector3 ponto)
    {
        if (quebrou) return;
        quebrou = true;
        ExplosaoAguaBenta.Criar(prefabExplosao, ponto, config);
        Destroy(gameObject);
    }
}
