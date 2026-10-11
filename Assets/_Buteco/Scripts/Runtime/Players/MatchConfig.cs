using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Buteco.Players
{
    public enum ControlMode
    {
        /// <summary>Qualquer teclado/controle comanda quem está na vez (passa o controle).</summary>
        Shared,
        /// <summary>Cada jogador tem seu próprio dispositivo.</summary>
        PerDevice,
    }

    public class PlayerSlot
    {
        public string name;
        public int team;
        /// <summary>Dispositivos deste jogador. Teclado implica mouse junto.</summary>
        public readonly List<InputDevice> devices = new();

        public bool UsesKeyboard
        {
            get
            {
                foreach (var d in devices) if (d is Keyboard) return true;
                return false;
            }
        }
    }

    public class MatchConfig
    {
        public int teamSize = 1;
        public ControlMode mode = ControlMode.Shared;

        /// <summary>Ordem de jogo: A1, B1, A2, B2 — alterna os times a cada vez.</summary>
        public readonly List<PlayerSlot> players = new();

        public static readonly string[] TeamNames1v1 = { "Amarelo", "Vermelho" };
        public static readonly string[] TeamNames2v2 = { "Dupla Amarela", "Dupla Vermelha" };
        public static readonly Color[] TeamColors = { new(1f, 0.80f, 0.15f), new(0.95f, 0.30f, 0.25f) };

        public string TeamName(int team) =>
            teamSize == 1 ? players[team].name : TeamNames2v2[team];

        public static MatchConfig Create(int teamSize, ControlMode mode)
        {
            var cfg = new MatchConfig { teamSize = teamSize, mode = mode };
            int total = teamSize * 2;
            for (int i = 0; i < total; i++)
            {
                cfg.players.Add(new PlayerSlot { name = $"Jogador {i + 1}", team = i % 2 });
            }
            return cfg;
        }

        /// <summary>Dispositivos que comandam o jogador; null = todos (modo compartilhado).</summary>
        public IReadOnlyList<InputDevice> DevicesFor(int playerIndex) =>
            mode == ControlMode.Shared ? null : players[playerIndex].devices;

        public bool AllJoined()
        {
            foreach (var p in players) if (p.devices.Count == 0) return false;
            return true;
        }
    }
}
