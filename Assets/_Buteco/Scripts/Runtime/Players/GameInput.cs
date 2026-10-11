using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Buteco.Players
{
    /// <summary>Entrada de um quadro, já agregada entre os dispositivos permitidos.</summary>
    public struct FrameInput
    {
        public float aimRotate;     // -1..1, positivo = gira para a direita
        public float aimFine;       // -1..1, ajuste fino
        public float mouseDeltaX;   // pixels
        public bool hasPointer;
        public Vector2 pointer;     // posição do mouse na tela
        public bool pointerMoved;
        public bool fineModifier;   // shift
        public bool chargeHeld;     // espaço / clique / A
        public float triggerPower;  // gatilho analógico (0..1)
        public bool confirm;        // apertou neste quadro
        public bool cancel;
        public bool toggleView;
        public bool pause;
        public Vector2 move;        // posicionar a branca
        public float zoom;          // >0 aproxima
        public bool usedGamepad;
        public bool usedKeyboard;
    }

    /// <summary>Entrada do modo passeio (primeira pessoa).</summary>
    public struct ExploreInput
    {
        public Vector2 move;        // WASD / analógico esq.
        public Vector2 look;        // graus neste quadro (x = guinada, y = arfagem)
        public bool run;
        public bool interact;       // E / A
        public bool useHeld;        // clique / RT (segurando)
        public bool usePressed;     // clique / RT (neste quadro)
        public bool back;           // levantar / largar: Q, botão dir. / B
        public bool pause;
        public bool usedGamepad;
        public bool usedKeyboard;
    }

    public static class GameInput
    {
        public static float MouseSensitivity = 0.09f;   // graus por pixel
        public static float StickLookSpeed = 160f;      // graus por segundo
        static bool triggerWasDown;

        const float Dead = 0.18f;

        static float DZ(float v) => Mathf.Abs(v) < Dead ? 0f : (v - Mathf.Sign(v) * Dead) / (1f - Dead);
        static Vector2 DZ(Vector2 v) => v.magnitude < Dead ? Vector2.zero : v.normalized * ((v.magnitude - Dead) / (1f - Dead));

        static bool Allowed(IReadOnlyList<InputDevice> devices, InputDevice d)
        {
            if (d == null) return false;
            if (devices == null) return true;
            for (int i = 0; i < devices.Count; i++) if (devices[i] == d) return true;
            return false;
        }

        public static FrameInput Read(IReadOnlyList<InputDevice> devices)
        {
            var f = new FrameInput();

            var kb = Keyboard.current;
            if (Allowed(devices, kb))
            {
                float r = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) r -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) r += 1f;
                f.aimRotate += r;
                f.fineModifier |= kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                f.chargeHeld |= kb.spaceKey.isPressed;
                f.confirm |= kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
                f.cancel |= kb.backspaceKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame;
                f.toggleView |= kb.tabKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame;
                f.pause |= kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
                var mv = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1;
                f.move += mv;
                if (kb.qKey.isPressed) f.zoom -= 1f;
                if (kb.eKey.isPressed) f.zoom += 1f;
                if (kb.anyKey.isPressed) f.usedKeyboard = true;

                var mouse = Mouse.current;
                if (mouse != null)
                {
                    f.hasPointer = true;
                    f.pointer = mouse.position.ReadValue();
                    var d = mouse.delta.ReadValue();
                    f.mouseDeltaX += d.x;
                    f.pointerMoved |= d.sqrMagnitude > 0.25f;
                    f.chargeHeld |= mouse.leftButton.isPressed;
                    f.confirm |= mouse.leftButton.wasPressedThisFrame;
                    f.cancel |= mouse.rightButton.wasPressedThisFrame;
                    f.zoom += Mathf.Clamp(mouse.scroll.ReadValue().y, -1f, 1f) * 6f;
                    if (d.sqrMagnitude > 0.25f || mouse.leftButton.isPressed) f.usedKeyboard = true;
                }
            }

            foreach (var g in Gamepad.all)
            {
                if (!Allowed(devices, g)) continue;
                var ls = DZ(g.leftStick.ReadValue());
                var rs = g.rightStick.ReadValue();
                f.aimRotate += ls.x;
                f.aimFine += DZ(rs.x);
                f.move += ls + DZ(g.dpad.ReadValue());
                f.chargeHeld |= g.buttonSouth.isPressed;
                f.triggerPower = Mathf.Max(f.triggerPower, g.rightTrigger.ReadValue());
                f.confirm |= g.buttonSouth.wasPressedThisFrame;
                f.cancel |= g.buttonEast.wasPressedThisFrame;
                f.toggleView |= g.buttonNorth.wasPressedThisFrame;
                f.pause |= g.startButton.wasPressedThisFrame;
                f.fineModifier |= g.leftTrigger.ReadValue() > 0.5f;
                if (g.rightShoulder.isPressed) f.zoom += 1f;
                if (g.leftShoulder.isPressed) f.zoom -= 1f;
                if (ls != Vector2.zero || g.buttonSouth.isPressed || g.rightTrigger.ReadValue() > 0.05f || DZ(rs.x) != 0f)
                    f.usedGamepad = true;
            }

            f.aimRotate = Mathf.Clamp(f.aimRotate, -1f, 1f);
            f.aimFine = Mathf.Clamp(f.aimFine, -1f, 1f);
            f.move = Vector2.ClampMagnitude(f.move, 1f);
            return f;
        }

        public static ExploreInput ReadExplore(float dt)
        {
            var f = new ExploreInput();
            var kb = Keyboard.current;
            if (kb != null)
            {
                var mv = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1;
                f.move += mv;
                f.run |= kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                f.interact |= kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
                f.back |= kb.qKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
                f.pause |= kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
                f.useHeld |= kb.spaceKey.isPressed;
                f.usePressed |= kb.spaceKey.wasPressedThisFrame;
                if (kb.anyKey.isPressed) f.usedKeyboard = true;
            }
            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                f.look += mouse.delta.ReadValue() * MouseSensitivity * GameSettings.MouseSens;
                f.useHeld |= mouse.leftButton.isPressed;
                f.usePressed |= mouse.leftButton.wasPressedThisFrame;
                f.back |= mouse.rightButton.wasPressedThisFrame;
                if (mouse.delta.ReadValue().sqrMagnitude > 0.25f || mouse.leftButton.isPressed) f.usedKeyboard = true;
            }
            bool triggerDown = false;
            foreach (var g in Gamepad.all)
            {
                var ls = DZ(g.leftStick.ReadValue());
                var rs = DZ(g.rightStick.ReadValue());
                f.move += ls;
                f.look += new Vector2(rs.x, rs.y * 0.75f) * StickLookSpeed * GameSettings.StickSens * dt;
                f.run |= g.leftStickButton.isPressed || g.leftTrigger.ReadValue() > 0.5f;
                f.interact |= g.buttonSouth.wasPressedThisFrame;
                f.back |= g.buttonEast.wasPressedThisFrame;
                f.pause |= g.startButton.wasPressedThisFrame;
                bool rt = g.rightTrigger.ReadValue() > 0.4f || g.buttonWest.isPressed;
                f.useHeld |= rt;
                triggerDown |= rt;
                if (ls != Vector2.zero || rs != Vector2.zero || rt || g.buttonSouth.isPressed) f.usedGamepad = true;
            }
            f.usePressed |= triggerDown && !triggerWasDown;
            triggerWasDown = triggerDown;
            if (GameSettings.InvertY) f.look.y = -f.look.y;
            f.move = Vector2.ClampMagnitude(f.move, 1f);
            return f;
        }

        /// <summary>Algum dispositivo pediu para entrar na partida? (tela de "aperte para entrar").</summary>
        public static InputDevice JoinPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return kb;
            foreach (var g in Gamepad.all)
                if (g.buttonSouth.wasPressedThisFrame) return g;
            return null;
        }

        /// <summary>Enter no teclado ou Start em algum controle.</summary>
        public static InputDevice StartPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return kb;
            foreach (var g in Gamepad.all)
                if (g.startButton.wasPressedThisFrame) return g;
            return null;
        }

        public static InputDevice LeavePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backspaceKey.wasPressedThisFrame) return kb;
            foreach (var g in Gamepad.all)
                if (g.buttonEast.wasPressedThisFrame) return g;
            return null;
        }

        public static string DeviceLabel(InputDevice d)
        {
            if (d is Keyboard) return "Teclado + mouse";
            if (d is Gamepad g)
            {
                int i = 0;
                foreach (var gp in Gamepad.all)
                {
                    i++;
                    if (gp == g) break;
                }
                return $"Controle {i}";
            }
            return d?.displayName ?? "?";
        }
    }
}
