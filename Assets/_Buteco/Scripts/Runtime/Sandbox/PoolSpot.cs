using UnityEngine;

namespace Buteco.Sandbox
{
    /// <summary>A mesa de sinuca no modo passeio: chega perto e começa uma partida.</summary>
    public class PoolSpot : Interactable
    {
        public override string Prompt(ButecoPlayer player) => player.CurrentSeat != null ? null : "Jogar sinuca";

        public override void Interact(ButecoPlayer player) => player.sandbox?.EnterPool();
    }
}
