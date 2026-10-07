using UnityEngine;

// Máscara para raycasts que não devem acertar o próprio player nem a arma
public static class Camadas
{
    public static int SemPlayer => ~LayerMask.GetMask("Player", "gun", "Ignore Raycast");
}
