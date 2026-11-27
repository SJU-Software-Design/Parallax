namespace GlassTrial
{
    public static class NikeHintGuide
    {
        public static int Stage(bool artworkComplete, bool doorUnlocked) => doorUnlocked ? 2 : artworkComplete ? 1 : 0;
        public static string Text(int stage, int page) => HintCatalogue.Text("nike." + stage, page);
    }
}
