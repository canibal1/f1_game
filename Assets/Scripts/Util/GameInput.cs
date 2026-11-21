using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace F1Game
{
    public enum GameAction
    {
        Throttle, Brake, SteerLeft, SteerRight, ShiftUp, ShiftDown, Camera, Pause, PitMenu, ResetCar,
        LookBack, Confirm, Back, Option1, Option2, Option3, BoxRequest, DRS
    }

    /// <summary>
    /// Input abstraction that works with the new Input System package (keyboard + gamepad)
    /// and with the legacy Input Manager (keyboard) – whichever "Active Input Handling" is set.
    /// </summary>
    public static class GameInput
    {
        public static float SteerAxis
        {
            get
            {
                float v = 0f;
                if (Held(GameAction.SteerLeft)) v -= 1f;
                if (Held(GameAction.SteerRight)) v += 1f;
#if ENABLE_INPUT_SYSTEM
                var gp = Gamepad.current;
                if (gp != null)
                {
                    float s = gp.leftStick.ReadValue().x;
                    if (Mathf.Abs(s) > 0.08f) v = Mathf.Sign(s) * Mathf.Pow((Mathf.Abs(s) - 0.08f) / 0.92f, 1.4f);
                }
#endif
                return Mathf.Clamp(v, -1f, 1f);
            }
        }

        public static bool UsingAnalogSteering
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var gp = Gamepad.current;
                return gp != null && Mathf.Abs(gp.leftStick.ReadValue().x) > 0.08f;
#else
                return false;
#endif
            }
        }

        public static float ThrottleAxis
        {
            get
            {
                float v = Held(GameAction.Throttle) ? 1f : 0f;
#if ENABLE_INPUT_SYSTEM
                var gp = Gamepad.current;
                if (gp != null) v = Mathf.Max(v, gp.rightTrigger.ReadValue());
#endif
                return v;
            }
        }

        public static float BrakeAxis
        {
            get
            {
                float v = Held(GameAction.Brake) ? 1f : 0f;
#if ENABLE_INPUT_SYSTEM
                var gp = Gamepad.current;
                if (gp != null) v = Mathf.Max(v, gp.leftTrigger.ReadValue());
#endif
                return v;
            }
        }

        public static bool Held(GameAction a)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                foreach (var k in Keys(a)) if (kb[k].isPressed) return true;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                var b = Button(gp, a);
                if (b != null && b.isPressed) return true;
            }
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            foreach (var k in LegacyKeys(a)) if (Input.GetKey(k)) return true;
            return false;
#else
            return false;
#endif
        }

        public static bool Down(GameAction a)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                foreach (var k in Keys(a)) if (kb[k].wasPressedThisFrame) return true;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                var b = Button(gp, a);
                if (b != null && b.wasPressedThisFrame) return true;
            }
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            foreach (var k in LegacyKeys(a)) if (Input.GetKeyDown(k)) return true;
            return false;
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static readonly Key[] none = new Key[0];

        static Key[][] keyTable;

        static Key[] Keys(GameAction a)
        {
            if (keyTable == null)
            {
                var values = (GameAction[])System.Enum.GetValues(typeof(GameAction));
                keyTable = new Key[values.Length][];
                foreach (var v in values) keyTable[(int)v] = BuildKeys(v);
            }
            return keyTable[(int)a];
        }

        static Key[] BuildKeys(GameAction a)
        {
            switch (a)
            {
                case GameAction.Throttle: return new[] { Key.W, Key.UpArrow };
                case GameAction.Brake: return new[] { Key.S, Key.DownArrow, Key.Space };
                case GameAction.SteerLeft: return new[] { Key.A, Key.LeftArrow };
                case GameAction.SteerRight: return new[] { Key.D, Key.RightArrow };
                case GameAction.ShiftUp: return new[] { Key.E, Key.LeftShift };
                case GameAction.ShiftDown: return new[] { Key.Q, Key.LeftCtrl };
                case GameAction.Camera: return new[] { Key.C };
                case GameAction.Pause: return new[] { Key.Escape };
                case GameAction.PitMenu: return new[] { Key.P, Key.Tab };
                case GameAction.ResetCar: return new[] { Key.R };
                case GameAction.LookBack: return new[] { Key.V };
                case GameAction.Confirm: return new[] { Key.Enter, Key.NumpadEnter };
                case GameAction.Back: return new[] { Key.Backspace };
                case GameAction.Option1: return new[] { Key.Digit1, Key.Numpad1 };
                case GameAction.Option2: return new[] { Key.Digit2, Key.Numpad2 };
                case GameAction.Option3: return new[] { Key.Digit3, Key.Numpad3 };
                case GameAction.BoxRequest: return new[] { Key.B };
                case GameAction.DRS: return new[] { Key.F };
            }
            return none;
        }

        static UnityEngine.InputSystem.Controls.ButtonControl Button(Gamepad gp, GameAction a)
        {
            switch (a)
            {
                case GameAction.ShiftUp: return gp.rightShoulder;
                case GameAction.ShiftDown: return gp.leftShoulder;
                case GameAction.Camera: return gp.buttonNorth;
                case GameAction.Pause: return gp.startButton;
                case GameAction.PitMenu: return gp.selectButton;
                case GameAction.ResetCar: return gp.buttonWest;
                case GameAction.LookBack: return gp.rightStickButton;
                case GameAction.Confirm: return gp.buttonSouth;
                case GameAction.Back: return gp.buttonEast;
                case GameAction.Option1: return gp.dpad.left;
                case GameAction.Option2: return gp.dpad.up;
                case GameAction.Option3: return gp.dpad.right;
                case GameAction.BoxRequest: return gp.dpad.down;
                case GameAction.DRS: return gp.buttonSouth;
            }
            return null;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        static readonly KeyCode[] noneLegacy = new KeyCode[0];

        static KeyCode[][] legacyTable;

        static KeyCode[] LegacyKeys(GameAction a)
        {
            if (legacyTable == null)
            {
                var values = (GameAction[])System.Enum.GetValues(typeof(GameAction));
                legacyTable = new KeyCode[values.Length][];
                foreach (var v in values) legacyTable[(int)v] = BuildLegacyKeys(v);
            }
            return legacyTable[(int)a];
        }

        static KeyCode[] BuildLegacyKeys(GameAction a)
        {
            switch (a)
            {
                case GameAction.Throttle: return new[] { KeyCode.W, KeyCode.UpArrow };
                case GameAction.Brake: return new[] { KeyCode.S, KeyCode.DownArrow, KeyCode.Space };
                case GameAction.SteerLeft: return new[] { KeyCode.A, KeyCode.LeftArrow };
                case GameAction.SteerRight: return new[] { KeyCode.D, KeyCode.RightArrow };
                case GameAction.ShiftUp: return new[] { KeyCode.E, KeyCode.LeftShift, KeyCode.JoystickButton5 };
                case GameAction.ShiftDown: return new[] { KeyCode.Q, KeyCode.LeftControl, KeyCode.JoystickButton4 };
                case GameAction.Camera: return new[] { KeyCode.C, KeyCode.JoystickButton3 };
                case GameAction.Pause: return new[] { KeyCode.Escape, KeyCode.JoystickButton7 };
                case GameAction.PitMenu: return new[] { KeyCode.P, KeyCode.Tab, KeyCode.JoystickButton6 };
                case GameAction.ResetCar: return new[] { KeyCode.R, KeyCode.JoystickButton2 };
                case GameAction.LookBack: return new[] { KeyCode.V };
                case GameAction.Confirm: return new[] { KeyCode.Return, KeyCode.KeypadEnter, KeyCode.JoystickButton0 };
                case GameAction.Back: return new[] { KeyCode.Backspace, KeyCode.JoystickButton1 };
                case GameAction.Option1: return new[] { KeyCode.Alpha1, KeyCode.Keypad1 };
                case GameAction.Option2: return new[] { KeyCode.Alpha2, KeyCode.Keypad2 };
                case GameAction.Option3: return new[] { KeyCode.Alpha3, KeyCode.Keypad3 };
                case GameAction.BoxRequest: return new[] { KeyCode.B };
                case GameAction.DRS: return new[] { KeyCode.F };
            }
            return noneLegacy;
        }
#endif
    }
}
