using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Buteco.UI
{
    /// <summary>Seletor "‹ valor ›" das opções: esquerda/direita (teclado, d-pad, analógico) troca o valor.</summary>
    public class OptionStepper : MonoBehaviour, IMoveHandler
    {
        public Action<int> step;

        public void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) step?.Invoke(-1);
            else if (e.moveDir == MoveDirection.Right) step?.Invoke(1);
        }
    }
}
