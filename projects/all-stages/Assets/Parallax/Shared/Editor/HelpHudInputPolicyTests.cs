using System;
using System.Collections.Generic;
using ShadeLink;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class HelpHudInputPolicyTests
    {
        static int checks;
        static void Require(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception("HELP_HUD_INPUT_POLICY_FAILED: " + message);
        }
        public static void Run()
        {
            checks = 0;
            // Rebinding must not make help clicks reset/cancel/launch a stage.
            for (int button = 0; button <= 6; button++)
            {
                KeyCode key = (KeyCode)((int)KeyCode.Mouse0 + button);
                Require(!PlayerControls.FilterActionInput(key, true, true), "blocked mouse " + button);
                Require(PlayerControls.FilterActionInput(key, true, false), "restored mouse " + button);
                Require(!PlayerControls.FilterActionInput(key, false, false), "no synthetic mouse input " + button);
                Require(!PlayerControls.FilterActionInput(key, true, false, true), "closing click stays consumed " + button);
                Require(!PlayerControls.FilterActionInput(key, false, false, true), "consumed click never creates input " + button);
            }
            foreach (KeyCode key in new[] { KeyCode.W, KeyCode.R, KeyCode.Backspace, KeyCode.LeftArrow, KeyCode.Joystick1Button0 })
            {
                Require(PlayerControls.FilterActionInput(key, true, true), "non-pointer input preserved " + key);
                Require(PlayerControls.FilterActionInput(key, true, false, true), "closing click preserves non-pointer input " + key);
            }
            Require(PlayerControls.IsReservedBindingKey(KeyCode.K), "K reserved for help");
            Require(PlayerControls.IsReservedBindingKey(KeyCode.Escape), "Esc still reserved");

            // Check real binding validation, then restore the previous runtime map.
            var original = new KeyCode[11];
            for (int i = 0; i < original.Length; i++) original[i] = PlayerControls.KeyFor((PlayerControls.Action)i);
            try
            {
                var conflict = PlayerControls.DefaultBindings(); conflict[(int)PlayerControls.Action.Reset] = KeyCode.K;
                bool rejected = false;
                try { PlayerControls.ApplyBindings(conflict); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "runtime rejects a fresh K binding");
                Require(PlayerControls.KeyFor(PlayerControls.Action.Reset) == original[(int)PlayerControls.Action.Reset], "rejection preserves runtime bindings");
            }
            finally { PlayerControls.ApplyBindings(original); }

            // A legacy settings file can use K for any action without losing the
            // user's other bindings, volumes or display/comfort choices.
            for (int action = 0; action < 11; action++)
            {
                var legacy = GameShellConfig.Defaults();
                legacy.masterVolume = .37f; legacy.textSize = 120; legacy.reducedFlicker = true;
                legacy.bindings[action] = KeyCode.K;
                var loaded = JsonUtility.FromJson<GameShellConfig>(JsonUtility.ToJson(legacy));
                Require(loaded.MigrateReservedHelpBinding() == action, "legacy action migrated " + action);
                Require(loaded.bindings[action] == GameShellConfig.DefaultBindings()[action], "free original default restored " + action);
                for (int other = 0; other < 11; other++)
                    if (other != action) Require(loaded.bindings[other] == legacy.bindings[other], "other binding preserved " + action + "/" + other);
                Require(Math.Abs(loaded.masterVolume - .37f) < .0001f && loaded.textSize == 120 && loaded.reducedFlicker, "other settings preserved " + action);
                Require(loaded.MigrateReservedHelpBinding() == -1, "migration idempotent " + action);
                Require(new HashSet<KeyCode>(loaded.bindings).Count == 11, "migrated keys stay unique " + action);
                try
                {
                    PlayerControls.ApplyBindings(loaded.bindings);
                    Require(PlayerControls.KeyFor((PlayerControls.Action)action) == loaded.bindings[action], "migrated settings accepted by runtime " + action);
                }
                finally { PlayerControls.ApplyBindings(original); }
            }
            var occupied = GameShellConfig.Defaults();
            occupied.bindings[(int)PlayerControls.Action.Forward] = KeyCode.R;
            occupied.bindings[(int)PlayerControls.Action.Reset] = KeyCode.K;
            Require(occupied.MigrateReservedHelpBinding() == (int)PlayerControls.Action.Reset, "occupied default migrated");
            Require(occupied.bindings[(int)PlayerControls.Action.Forward] == KeyCode.R, "occupied user binding preserved");
            Require(occupied.bindings[(int)PlayerControls.Action.Reset] == KeyCode.W, "unused key selected without stealing a binding");
            Require(new HashSet<KeyCode>(occupied.bindings).Count == 11, "fallback keys unique");
            var defaults = GameShellConfig.Defaults();
            Require(defaults.MigrateReservedHelpBinding() == -1, "default map unchanged");
            Require(PlayerControls.DefaultBindings()[(int)PlayerControls.Action.Hints] == KeyCode.H, "runtime keeps upstream H default");
            Require(GameShellConfig.DefaultBindings()[(int)PlayerControls.Action.Hints] == KeyCode.H, "settings keep upstream H default");
            var legacyHints = GameShellConfig.Defaults();
            legacyHints.bindings[(int)PlayerControls.Action.Hints] = KeyCode.J;
            Require(legacyHints.MigrateLegacyHintBinding(), "legacy J hint moves to free H");
            Require(legacyHints.bindings[(int)PlayerControls.Action.Hints] == KeyCode.H, "J hint migration uses H");
            Require(!legacyHints.MigrateLegacyHintBinding(), "J hint migration idempotent");

            var combined = GameShellConfig.Defaults();
            combined.bindings[(int)PlayerControls.Action.Hints] = KeyCode.J;
            combined.bindings[(int)PlayerControls.Action.Reset] = KeyCode.K;
            Require(combined.MigrateLegacyHintBinding(), "J migration accompanies reserved K");
            Require(combined.MigrateReservedHelpBinding() == (int)PlayerControls.Action.Reset, "K migration accompanies J");
            Require(combined.bindings[(int)PlayerControls.Action.Hints] == KeyCode.H, "combined migration keeps H hint");
            Require(combined.bindings[(int)PlayerControls.Action.Reset] == KeyCode.R, "combined migration restores reset");
            Require(new HashSet<KeyCode>(combined.bindings).Count == 11, "combined migration stays unique");

            var customH = GameShellConfig.Defaults();
            customH.bindings[(int)PlayerControls.Action.Forward] = KeyCode.H;
            customH.bindings[(int)PlayerControls.Action.Hints] = KeyCode.J;
            customH.bindings[(int)PlayerControls.Action.Reset] = KeyCode.K;
            Require(!customH.MigrateLegacyHintBinding(), "custom H blocks the J hint migration");
            Require(customH.MigrateReservedHelpBinding() == (int)PlayerControls.Action.Reset, "K still migrates with custom H");
            Require(customH.bindings[(int)PlayerControls.Action.Forward] == KeyCode.H, "custom H owner preserved");
            Require(customH.bindings[(int)PlayerControls.Action.Hints] == KeyCode.J, "custom H preserves J hint");
            Require(customH.bindings[(int)PlayerControls.Action.Reset] == KeyCode.R, "custom H preserves reset migration");
            Require(new HashSet<KeyCode>(customH.bindings).Count == 11, "custom H migration stays unique");

            var hintOnK = GameShellConfig.Defaults();
            hintOnK.bindings[(int)PlayerControls.Action.Forward] = KeyCode.H;
            hintOnK.bindings[(int)PlayerControls.Action.Hints] = KeyCode.K;
            Require(!hintOnK.MigrateLegacyHintBinding(), "K hint is not a former J default");
            Require(hintOnK.MigrateReservedHelpBinding() == (int)PlayerControls.Action.Hints, "K hint migrates with occupied H");
            Require(hintOnK.bindings[(int)PlayerControls.Action.Forward] == KeyCode.H, "K hint migration preserves custom H");
            Require(hintOnK.bindings[(int)PlayerControls.Action.Hints] == KeyCode.W, "K hint migration uses the free default");
            Require(new HashSet<KeyCode>(hintOnK.bindings).Count == 11, "K hint fallback stays unique");
            Debug.Log("HELP_HUD_INPUT_POLICY_TESTS_PASSED checks=" + checks);
        }
    }
}
