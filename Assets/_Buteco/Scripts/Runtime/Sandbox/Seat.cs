using UnityEngine;

namespace Buteco.Sandbox
{
    /// <summary>Cadeira ou banqueta onde o freguês pode sentar.</summary>
    public class Seat : Interactable
    {
        [Tooltip("Posição/direção dos olhos de quem senta.")]
        public Transform eye;
        [Tooltip("Onde a pessoa fica de pé ao levantar.")]
        public Transform standPoint;
        public string label = "Sentar";
        public float lookPitch = 5f;
        [Tooltip("Cadeira da roda de pagode: sentado aqui dá pra bater palma com a roda.")]
        public bool atRoda;

        public bool Occupied { get; set; }

        public override string Prompt(ButecoPlayer player)
        {
            if (Occupied || player.CurrentSeat != null) return null;
            return label;
        }

        public override void Interact(ButecoPlayer player) => player.Sit(this);
    }
}
