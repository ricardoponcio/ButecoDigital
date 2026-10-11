using System.Collections.Generic;
using System.Linq;

namespace Buteco.Pool
{
    public enum BallGroup { None, Solids, Stripes }

    public struct ShotReport
    {
        public bool isBreak;
        public int firstHit;          // número da primeira bola tocada pela branca (-1 = nenhuma)
        public List<int> pocketed;    // na ordem em que caíram
    }

    public struct ShotOutcome
    {
        public bool foul;
        public bool keepTurn;
        public int winnerTeam;        // -1 = jogo segue
        public bool respotEight;
        public string message;
    }

    /// <summary>Bola 8 "de buteco": lisas x listradas, sem cantar caçapa, falta = bola na mão.</summary>
    public class PoolRules
    {
        public readonly BallGroup[] teamGroup = new BallGroup[2];
        public readonly HashSet<int> pocketed = new();

        public bool TableOpen => teamGroup[0] == BallGroup.None;

        public static BallGroup GroupOf(int n) =>
            n >= 1 && n <= 7 ? BallGroup.Solids : n >= 9 && n <= 15 ? BallGroup.Stripes : BallGroup.None;

        public static BallGroup Other(BallGroup g) =>
            g == BallGroup.Solids ? BallGroup.Stripes : g == BallGroup.Stripes ? BallGroup.Solids : BallGroup.None;

        public static string GroupName(BallGroup g) =>
            g == BallGroup.Solids ? "Lisas" : g == BallGroup.Stripes ? "Listradas" : "Mesa aberta";

        public IEnumerable<int> RemainingBalls(BallGroup g)
        {
            for (int n = 1; n <= 15; n++)
                if (n != 8 && GroupOf(n) == g && !pocketed.Contains(n)) yield return n;
        }

        public int Remaining(BallGroup g) => RemainingBalls(g).Count();

        public bool OnEight(int team) => teamGroup[team] != BallGroup.None && Remaining(teamGroup[team]) == 0;

        public void Reset()
        {
            teamGroup[0] = teamGroup[1] = BallGroup.None;
            pocketed.Clear();
        }

        public ShotOutcome Evaluate(int team, ShotReport r)
        {
            var o = new ShotOutcome { winnerTeam = -1 };
            bool onEight = OnEight(team);
            bool cueIn = r.pocketed.Contains(0);
            bool eightIn = r.pocketed.Contains(8);
            var objs = r.pocketed.Where(n => n != 0 && n != 8).ToList();
            foreach (var n in objs) pocketed.Add(n);

            var g = teamGroup[team];
            string foul = null;
            if (r.firstHit < 0) foul = "Falta! Não acertou nenhuma bola";
            else if (cueIn) foul = "Falta! A branca caiu";
            else if (!r.isBreak)
            {
                if (g == BallGroup.None)
                {
                    if (r.firstHit == 8) foul = "Falta! Tocou na 8 primeiro";
                }
                else if (onEight)
                {
                    if (r.firstHit != 8) foul = "Falta! Tinha que tocar na 8";
                }
                else if (GroupOf(r.firstHit) != g)
                {
                    foul = "Falta! Tocou primeiro na bola do adversário";
                }
            }
            if (r.firstHit >= 0 && cueIn) foul = "Falta! A branca caiu";

            if (eightIn)
            {
                if (r.isBreak)
                {
                    o.respotEight = true;
                    o.message = "A 8 caiu na saída — volta pra mesa";
                }
                else
                {
                    bool win = onEight && foul == null;
                    o.winnerTeam = win ? team : 1 - team;
                    o.message = win ? "Matou a 8!" : onEight ? "A 8 caiu, mas com falta..." : "A 8 caiu antes da hora!";
                    o.foul = !win;
                    return o;
                }
            }

            o.foul = foul != null;
            if (o.foul)
            {
                o.message = foul + " — bola na mão pro adversário";
                return o;
            }

            if (g == BallGroup.None && !r.isBreak && objs.Count > 0)
            {
                g = GroupOf(objs[0]);
                teamGroup[team] = g;
                teamGroup[1 - team] = Other(g);
                o.message = $"Ficou com as {GroupName(g).ToLower()}";
            }

            bool legal = r.isBreak || teamGroup[team] == BallGroup.None
                ? objs.Count > 0
                : objs.Any(n => GroupOf(n) == g);
            o.keepTurn = legal;
            return o;
        }
    }
}
