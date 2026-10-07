using System;
using System.Collections.Generic;
using UnityEngine;

// Barulhos que os inimigos podem ouvir (tiros, passos correndo, quedas)
public static class Barulho
{
    public static event Action<Vector3, float> Emitido;

    // Últimos barulhos, para os gizmos desenharem no editor
    public struct Registro
    {
        public Vector3 posicao;
        public float raio;
        public float tempo;
    }
    public static readonly List<Registro> Recentes = new List<Registro>();

    public static void Emitir(Vector3 posicao, float raio)
    {
        Emitido?.Invoke(posicao, raio);

        if (Application.isEditor)
        {
            Recentes.Add(new Registro { posicao = posicao, raio = raio, tempo = Time.time });
            if (Recentes.Count > 16) Recentes.RemoveAt(0);
        }
    }
}
