using System;
using System.Collections;
using UnityEngine;

public partial class ParallaxGameShell : MonoBehaviour
{
    public enum LoadingTransitionPhase { Idle, FadingOut, Loading, FadingIn, Completed }
    public const string LoadingTip = "개발 중 가장 오래 걸린 작업은 사운드 수집과 선정이었습니다. “하나만 더 들어볼까요”가 이렇게 무서운 말인 줄 몰랐습니다.";
    public const float LoadingFadeSeconds = .65f, MinimumLoadingSeconds = 2.8f;
    const string LoadingTipLines = "개발 중 가장 오래 걸린 작업은 사운드 수집과 선정이었습니다.\n“하나만 더 들어볼까요”가 이렇게 무서운 말인 줄 몰랐습니다.";
    // Pick once per loading request. Fixed title starts retain the approved anecdote.
    static readonly string[] continueTips = {
        LoadingTip,
        "시작화면 주사위는 움직이지 않습니다. 대신 메뉴의 위치가 여러 번 움직였습니다.",
        "검을 꽂는 시간은 1.5초지만, 마음에 드는 검을 찾는 데에는 1.5시간이 걸렸습니다.",
        "막혔다면 물체의 크기와 그림자를 함께 살펴보세요.",
        "진행 상황은 리스폰 지점에서 자동으로 저장됩니다.",
        "환경설정에서 조작키와 마우스 감도를 바꿀 수 있습니다."
    };
    public static string[] ContinueTipsSnapshot() { return (string[])continueTips.Clone(); }
    public const int SwordTipIndex = 2;
    public static int[] EligibleContinueTipIndices(bool swordEncountered)
    {
        var eligible = new System.Collections.Generic.List<int>();
        for (int i = 0; i < continueTips.Length; i++)
            if (i != SwordTipIndex || swordEncountered) eligible.Add(i);
        return eligible.ToArray();
    }
    public static int SelectContinueTipIndex(int previousIndex, int randomSlot, bool swordEncountered = true)
    {
        int[] eligible = EligibleContinueTipIndices(swordEncountered);
        if (eligible.Length == 0) return 0;
        int previousSlot = Array.IndexOf(eligible, previousIndex);
        bool excludePrevious = eligible.Length > 1 && previousSlot >= 0;
        int available = eligible.Length - (excludePrevious ? 1 : 0);
        int slot = Mathf.Clamp(randomSlot, 0, available - 1);
        if (excludePrevious && slot >= previousSlot) slot++;
        return eligible[slot];
    }
    public string CurrentLoadingTip { get; private set; } = LoadingTip;
    public int CurrentLoadingTipIndex { get; private set; }
    public bool LoadingTipRandomized { get; private set; }
    public bool LoadingSwordTipUnlocked { get; private set; }
    int lastShownTipIndex = -1;
    void SelectLoadingTip(bool randomTip, bool swordEncountered)
    {
        LoadingTipRandomized = randomTip;
        LoadingSwordTipUnlocked = swordEncountered;
        int[] eligible = EligibleContinueTipIndices(swordEncountered);
        bool excludePrevious = eligible.Length > 1 && Array.IndexOf(eligible, lastShownTipIndex) >= 0;
        int available = eligible.Length - (excludePrevious ? 1 : 0);
        CurrentLoadingTipIndex = randomTip ? SelectContinueTipIndex(lastShownTipIndex,
            UnityEngine.Random.Range(0, Mathf.Max(1, available)), swordEncountered) : 0;
        CurrentLoadingTip = continueTips[CurrentLoadingTipIndex];
        lastShownTipIndex = CurrentLoadingTipIndex;
    }
    public LoadingTransitionPhase LoadingPhase { get; private set; }
    public bool LoadingPresentationActive { get { return LoadingPhase != LoadingTransitionPhase.Idle; } }
    public float LoadingOverlayAlpha { get; private set; }
    public float LoadingActualProgress { get; private set; }
    public float LoadingVisibleProgress { get; private set; }
    public double LoadingStartedAt { get; private set; }
    public double LoadingOpaqueAt { get; private set; }
    public double LoadingReadyAt { get; private set; }
    public double LoadingEndedAt { get; private set; }
    public string LoadingTarget { get; private set; }
    public int LoadingSequenceId { get; private set; }
    public bool LastLoadingCancelled { get; private set; }
    float loadingTargetProgress, loadingHumGain, loadingLampGain, loadingPhaseAge, loadingOpaqueAge;
    int loadingEpoch; bool loadingHumWasPlaying;

    public IEnumerator BeginLoadingTransition(string target, bool randomTip = false, bool swordEncountered = false)
    {
        if (LoadingPresentationActive) CancelLoadingTransition();
        int epoch = ++loadingEpoch;
        LoadingSequenceId++;
        LoadingTarget = target;
        SelectLoadingTip(randomTip, swordEncountered);
        LastLoadingCancelled = false;
        LoadingStartedAt = Time.realtimeSinceStartupAsDouble;
        LoadingOpaqueAt = LoadingReadyAt = LoadingEndedAt = 0;
        LoadingActualProgress = LoadingVisibleProgress = loadingTargetProgress = LoadingOverlayAlpha = 0;
        loadingPhaseAge = loadingOpaqueAge = 0;
        loadingHumWasPlaying = humSource != null && humSource.isPlaying;
        loadingHumGain = humSource == null ? 0 : humSource.volume;
        loadingLampGain = lampSource == null ? 1 : lampSource.volume;
        LoadingPhase = LoadingTransitionPhase.FadingOut;
        Debug.Log("PLAYER_LOADING_BEGIN id=" + LoadingSequenceId + " target=" + target);
        while (loadingPhaseAge < LoadingFadeSeconds)
        {
            if (epoch != loadingEpoch) yield break;
            LoadingOverlayAlpha = Smooth(loadingPhaseAge / LoadingFadeSeconds);
            yield return null;
        }
        if (epoch != loadingEpoch) yield break;
        LoadingOverlayAlpha = 1;
        LoadingPhase = LoadingTransitionPhase.Loading;
        loadingPhaseAge = loadingOpaqueAge = 0;
        LoadingOpaqueAt = Time.realtimeSinceStartupAsDouble;
        Debug.Log("PLAYER_LOADING_OPAQUE id=" + LoadingSequenceId);
        // Give the fully black presentation a frame before destination Awake can block.
        yield return null;
    }
    public void ReportLoadingProgress(float progress)
    {
        if (!LoadingPresentationActive || float.IsNaN(progress) || float.IsInfinity(progress)) return;
        LoadingActualProgress = Mathf.Max(LoadingActualProgress, Mathf.Clamp01(progress));
        // Scene activation and checkpoint restoration own the remaining ten percent.
        if (LoadingReadyAt <= 0) loadingTargetProgress = Mathf.Max(loadingTargetProgress, LoadingActualProgress * .9f);
    }
    void UpdateLoadingPresentation()
    {
        if (!LoadingPresentationActive || (!validationMode && !hadFocus)) return;
        // Count displayed frames, so an Alt-Tab pause cannot consume the Tip/fade.
        float delta = Mathf.Min(Time.unscaledDeltaTime, .05f);
        loadingPhaseAge += delta;
        if (LoadingPhase == LoadingTransitionPhase.Loading)
        {
            loadingOpaqueAge += delta;
            LoadingVisibleProgress = Mathf.MoveTowards(LoadingVisibleProgress, loadingTargetProgress, delta * .55f);
        }
        if (LoadingPhase == LoadingTransitionPhase.FadingOut)
        {
            if (humSource != null) humSource.volume = loadingHumGain * (1 - LoadingOverlayAlpha);
            if (lampSource != null) lampSource.volume = loadingLampGain * (1 - LoadingOverlayAlpha);
        }
    }
    public IEnumerator FinishLoadingTransition()
    {
        if (!LoadingPresentationActive) yield break;
        int epoch = loadingEpoch;
        LoadingActualProgress = 1;
        LoadingReadyAt = Time.realtimeSinceStartupAsDouble;
        loadingTargetProgress = 1;
        while (loadingOpaqueAge < MinimumLoadingSeconds || LoadingVisibleProgress < .9999f)
        {
            if (epoch != loadingEpoch) yield break;
            yield return null;
        }
        if (epoch != loadingEpoch) yield break;
        LoadingVisibleProgress = 1;
        // A complete bar remains visible before the world begins to appear.
        yield return null;
        if (epoch != loadingEpoch) yield break;
        LoadingPhase = LoadingTransitionPhase.FadingIn;
        loadingPhaseAge = 0;
        while (loadingPhaseAge < LoadingFadeSeconds)
        {
            if (epoch != loadingEpoch) yield break;
            LoadingOverlayAlpha = 1 - Smooth(loadingPhaseAge / LoadingFadeSeconds);
            yield return null;
        }
        if (epoch != loadingEpoch) yield break;
        LoadingOverlayAlpha = 0;
        LoadingEndedAt = Time.realtimeSinceStartupAsDouble;
        LoadingPhase = LoadingTransitionPhase.Completed;
        Debug.Log("PLAYER_LOADING_COMPLETE id=" + LoadingSequenceId + " visible=" + (LoadingEndedAt - LoadingOpaqueAt).ToString("F3"));
    }
    public void EndLoadingTransition()
    {
        LoadingOverlayAlpha = 0;
        LoadingPhase = LoadingTransitionPhase.Idle;
        if (LoadingSequenceId > 0 && lampSource != null) lampSource.volume = loadingLampGain;
    }
    public void CancelLoadingTransition()
    {
        bool wasActive = LoadingPresentationActive;
        loadingEpoch++;
        LastLoadingCancelled = true;
        LoadingEndedAt = Time.realtimeSinceStartupAsDouble;
        LoadingOverlayAlpha = 0;
        LoadingPhase = LoadingTransitionPhase.Idle;
        if (wasActive && humSource != null)
        {
            humSource.volume = loadingHumGain;
            if (loadingHumWasPlaying && !humSource.isPlaying && !InGameplayScene && !validationMode && hadFocus) humSource.Play();
        }
        if (wasActive && lampSource != null) lampSource.volume = loadingLampGain;
        Debug.Log("PLAYER_LOADING_CANCELLED id=" + LoadingSequenceId);
    }
    void DrawSceneLoadingPresentation()
    {
        Matrix4x4 matrix = GUI.matrix;
        Color color = GUI.color;
        float alpha = Mathf.Clamp01(LoadingOverlayAlpha);
        // Cover the physical window, including ultrawide/4:3 letterbox margins.
        GUI.matrix = Matrix4x4.identity;
        GUI.color = new Color(0, 0, 0, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.matrix = matrix;
        GUI.color = color;
        if (LoadingPhase == LoadingTransitionPhase.FadingOut || alpha <= 0) return;
        Color white = Foreground; white.a = alpha;
        Color muted = new Color(.72f, .72f, .74f, alpha);
        DrawText(new Rect(300, 420, 1320, 90), "Loading...", 48, white, TextAnchor.MiddleCenter);
        Rect bar = new Rect(360, 560, 1200, 6);
        Fill(bar, new Color(.24f, .24f, .26f, alpha));
        if (LoadingVisibleProgress > 0) Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(LoadingVisibleProgress), bar.height), white);
        DrawText(new Rect(300, 802, 1320, 38), "Tip.", 22, muted, TextAnchor.MiddleCenter);
        DrawText(new Rect(300, 850, 1320, 150), CurrentLoadingTip == LoadingTip ? LoadingTipLines : CurrentLoadingTip, 27, white, TextAnchor.MiddleCenter);
    }
}