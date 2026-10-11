using UnityEngine;

namespace Buteco.Sandbox
{
    /// <summary>Algo no buteco com que dá pra interagir olhando e apertando E / A.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Tooltip("Distância máxima (da câmera) para a interação aparecer.")]
        public float reach = 2.2f;

        /// <summary>Texto da ação ("Pedir uma gelada"); null = nada a fazer agora.</summary>
        public abstract string Prompt(ButecoPlayer player);

        public abstract void Interact(ButecoPlayer player);
    }
}
