using System;
using System.Collections.Generic;
using UnityEngine;
using ShadeLink.Gallery;
using ShadeLink.Beach;

namespace GlassTrial
{
    // Text is editable in Resources/HintText.json; selection only reads puzzle state.
    public static class HintCatalogue
    {
        [Serializable] sealed class FileData { public Entry[] entries; }
        [Serializable] sealed class Entry { public string id; public string[] pages; }
        static Dictionary<string, string[]> entries;
        static void EnsureLoaded()
        {
            if (entries != null) return;
            entries = new Dictionary<string, string[]>();
            var asset = Resources.Load<TextAsset>("HintText");
            if (asset == null) { Debug.LogError("HintText.json is missing"); return; }
            try { var data = JsonUtility.FromJson<FileData>(asset.text); if (data?.entries != null) foreach (var e in data.entries) if (e != null && !string.IsNullOrEmpty(e.id) && e.pages != null) entries[e.id] = e.pages; }
            catch (Exception e) { Debug.LogError("HintText.json: " + e.Message); }
        }
        public static string Text(string id, int page)
        {
            EnsureLoaded();
            return id != null && entries.TryGetValue(id, out var pages) && pages.Length > 0 ? pages[Mathf.Clamp(page - 1, 0, pages.Length - 1)] : "";
        }
        public static string Gallery(GalleryGame game)
        {
            if (!game.conceptLayout || game.exhibitionPreview) return "gallery.classic." + Mathf.Clamp(game.checkpoint, 0, 3);
            if (game.bottleBalance != null && game.bottleBalance.InRoom)
            {
                var b = game.bottleBalance;
                if (!b.Extracted) return "gallery.bottle.extract";
                if (!b.Solved) return "gallery.bottle.balance";
                return b.Reward != null && b.Reward.Unlocked ? "gallery.bottle.exit" : "gallery.bottle.sword";
            }
            if (game.NikeStepHints) return "nike." + game.NikeHintStage;
            if (game.checkpoint == 0) return "gallery.first";
            if (game.checkpoint == 1) return "gallery.nike.alternate";
            if (game.checkpoint == 2) return game.portraitGate != null && game.portraitGate.Opened ? "gallery.portrait.exit" : "gallery.portrait";
            if (game.checkpoint == 3) return game.orcusExit != null && game.orcusExit.IsReadyForEntry ? "gallery.hands.exit" : "gallery.hands";
            return null;
        }
        public static string Beach(BeachGame game)
        {
            switch (game.checkpoint)
            {
                case 0: return game.lightGate != null && game.lightGate.unlocked ? "beach.receivers.exit" : "beach.receivers";
                case 1: return "beach.bridges";
                case 2:
                    if (game.boat != null && (game.boat.Arrived || game.boat.AtBerth)) return "beach.boat.exit";
                    if (game.boat != null && game.boat.Departed) return "beach.boat.travel";
                    if (game.shadowHarbor != null && game.shadowHarbor.Solved) return "beach.boat.prepare";
                    return "beach.boat.shadow";
                case 3: return game.counterweightFinale != null && game.counterweightFinale.Ready ? "beach.lift.exit" : "beach.lift";
                default: return null;
            }
        }
    }
}
