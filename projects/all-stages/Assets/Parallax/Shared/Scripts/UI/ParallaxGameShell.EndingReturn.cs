using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink;

public partial class ParallaxGameShell
{
    const float EndingTitleStart = 4f;
    public const float EndingTitleRevealSeconds = IntroLength - EndingTitleStart;
    bool endingReturnArmed;
    public bool EndingTitleReplayActive { get; private set; }
    public float EndingTitleReplayElapsed { get; private set; }
    public IntroFrame CurrentIntroFrame => intro;
    public bool EndingTitleRevealActive => endingReturnArmed || EndingTitleReplayActive;
    public float EndingTitleRevealAlpha => endingReturnArmed ? 1 :
        EndingTitleReplayActive ? 1 - intro.background : 0;
    public string[] SoundDispatch => soundTrace.ToArray();

    public void ArmEndingTitleReveal()
    { endingReturnArmed = true; EndingTitleReplayActive = false; EndingTitleReplayElapsed = 0; }
    public void CancelEndingTitleReveal()
    { endingReturnArmed = false; EndingTitleReplayActive = false; EndingTitleReplayElapsed = 0; }
    void ObserveEndingTitleScene(Scene scene)
    {
        if (!endingReturnArmed || scene.name != "MainMenu") return;
        endingReturnArmed = false; EndingTitleReplayActive = true; EndingTitleReplayElapsed = 0;
        if (humSource != null) humSource.Stop();
        if (lampSource != null) lampSource.Stop();
        humStarted = switchPlayed = startupPlayed = firstRattlePlayed = false;
        flickerAt = -100; pendingRattleAt = -1; nextFlickerAt = 0;
        selectedMenu = 0; pointerMode = true;
        startAt = Now - EndingTitleStart; intro = EvaluateIntro(EndingTitleStart);
    }
    // The same presentation clock is used by live Update and isolated native checks.
    public void TickEndingTitlePresentation(float dt)
    {
        if (!EndingTitleReplayActive || !IsTitle || GameProgressRuntime.Transitioning) return;
        EndingTitleReplayElapsed = Mathf.Min(EndingTitleRevealSeconds,
            EndingTitleReplayElapsed + Mathf.Clamp(dt, 0, .25f));
        float elapsed = EndingTitleStart + EndingTitleReplayElapsed;
        startAt = Now - elapsed; UpdateIntro(elapsed);
        if (EndingTitleReplayElapsed >= EndingTitleRevealSeconds) EndingTitleReplayActive = false;
    }
    void ConsumeEndingTitleInput()
    {
        if (!endingReturnArmed || Event.current == null) return;
        if (Event.current.isKey || Event.current.isMouse || Event.current.type == EventType.ScrollWheel) Event.current.Use();
    }
    void DrawEndingTitleReveal()
    {
        // On MainMenu the original title timeline draws the black background and
        // reveals the photograph; no second mask covers the fading title.
        if (!endingReturnArmed) return;
        Matrix4x4 matrix = GUI.matrix; Color color = GUI.color;
        GUI.matrix = Matrix4x4.identity; GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.matrix = matrix; GUI.color = color;
    }
}
