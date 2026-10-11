using UnityEngine;

namespace Buteco.Sandbox
{
    public enum Service { Gelada, Coxinha, PagarConta }

    /// <summary>Pontos do balcão: pedir gelada, pedir coxinha na estufa, pagar a conta no caixa.</summary>
    public class CounterService : Interactable
    {
        public Service service;
        public Bartender bartender;

        public override string Prompt(ButecoPlayer player)
        {
            if (service == Service.PagarConta)
                return player.Conta > 0f ? $"Pagar a conta (R$ {player.Conta:0},00)" : null;
            if (bartender == null) return null;
            if (bartender.Busy) return $"{bartender.displayName} tá atendendo…";
            if (player.hands.Item != HeldItem.None && !player.hands.BottleEmpty) return null;
            return service == Service.Gelada
                ? $"Pedir uma gelada (R$ {ButecoPlayer.PrecoGelada:0})"
                : $"Pedir uma coxinha (R$ {ButecoPlayer.PrecoCoxinha:0})";
        }

        public override void Interact(ButecoPlayer player)
        {
            if (service == Service.PagarConta)
            {
                player.sandbox?.Toast($"Conta paga: R$ {player.Conta:0},00. Valeu, volte sempre!");
                player.PayTab();
                return;
            }
            if (bartender == null || bartender.Busy) return;
            if (player.hands.Item != HeldItem.None && !player.hands.BottleEmpty) return;
            var item = service == Service.Gelada ? HeldItem.Gelada : HeldItem.Coxinha;
            if (bartender.Order(item, player))
                player.sandbox?.Toast(item == HeldItem.Gelada ? $"\"Desce uma gelada, {bartender.displayName}!\"" : $"\"Me vê uma coxinha, {bartender.displayName}!\"");
        }
    }
}
