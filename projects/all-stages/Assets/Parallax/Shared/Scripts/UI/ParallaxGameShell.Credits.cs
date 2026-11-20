using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ShadeLink;
using ShadeLink.Eclipse;

public partial class ParallaxGameShell
{
    static readonly Rect CreditsViewport = new Rect(420, 225, 1080, 650);
    const float CreditsContentWidth = 1036f;
    sealed class CreditBlock
    { public string text; public int size; public float top, height; public Color color; public bool title; }
    readonly List<CreditBlock> creditBlocks = new List<CreditBlock>();
    Vector2 creditScroll;
    float creditContentHeight, creditLayoutScale = -1;
    string creditContentText = "";
    int creditEntryCount;
    public bool CreditsViewerOpen { get; private set; }
    public float CreditsViewerScrollY => creditScroll.y;
    public float CreditsViewerMaxScroll => Mathf.Max(0, creditContentHeight - CreditsViewport.height);
    public int CreditsViewerEntryCount => creditEntryCount;
    public string CreditsViewerContentText => creditContentText;
    public int MainMenuItemCount => GameCompletionState.CreditsUnlocked ? 5 : 4;
    public string GetMainMenuLabel(int index)
    {
        if (index < 0 || index >= MainMenuItemCount) return "";
        if (MainMenuItemCount == 4 || index < 3) return menuLabels[index];
        return index == 3 ? "크레딧" : "게임 나가기";
    }
    public Rect GetMainMenuRect(int index)
    {
        if (index < 0 || index >= MainMenuItemCount) return Rect.zero;
        if (MainMenuItemCount == 4) return menuRects[index];
        float width = index == 0 ? 435 : index == 4 ? 410 : 330;
        return new Rect(128, 710 + index * 72, width, 68);
    }
    public bool OpenCreditsViewer()
    {
        if (!IsTitle || !GameCompletionState.CreditsUnlocked || intro.menu < .999f ||
            GameProgressRuntime.Transitioning || LoadingPresentationActive || settingsOpen || exitDialog) return false;
        CreditsViewerOpen = true; creditScroll = Vector2.zero; creditLayoutScale = -1;
        BuildCreditsViewerLayout();
        if (humSource != null) humSource.Stop();
        if (lampSource != null) lampSource.Stop();
        humStarted = false; flickerAt = -100; pendingRattleAt = -1;
        if (Event.current != null) GUI.FocusControl(null);
        return true;
    }
    public void CloseCreditsViewer()
    {
        if (!CreditsViewerOpen) return;
        CreditsViewerOpen = false; PlayUi("back");
        if (IsTitle && !GameProgressRuntime.Transitioning)
        {
            StartHum(); nextFlickerAt = Now + UnityEngine.Random.Range(10f, 15f);
            selectedMenu = 3;
        }
    }
    void ResetCreditsViewerForScene()
    { CreditsViewerOpen = false; creditScroll = Vector2.zero; }
    public void ScrollCreditsViewer(float delta)
    {
        if (!CreditsViewerOpen || float.IsNaN(delta) || float.IsInfinity(delta)) return;
        BuildCreditsViewerLayout();
        creditScroll.y = Mathf.Clamp(creditScroll.y + delta, 0, CreditsViewerMaxScroll);
    }
    void BuildCreditsViewerLayout()
    {
        if (Mathf.Approximately(creditLayoutScale, PreviewTextScale)) return;
        creditLayoutScale = PreviewTextScale; creditBlocks.Clear(); creditEntryCount = 0;
        creditContentHeight = 0; var all = new StringBuilder();
        var file = Resources.Load<TextAsset>("Eclipse/EndingCredits");
        EclipseCreditDocument document = null;
        try { if (file != null) document = JsonUtility.FromJson<EclipseCreditDocument>(file.text); }
        catch (Exception error) { Debug.LogWarning("Credits document could not be read: " + error.Message); }
        if (document == null || document.team == null || document.assets == null)
        { AddCreditBlock("크레딧을 불러올 수 없습니다.", 28, Foreground, false); return; }
        AddCreditBlock("만든 사람들", 38, Foreground, true);
        foreach (var entry in document.team)
        {
            AddCreditBlock(entry.title ?? "", 30, Foreground, false);
            AddCreditBlock(entry.role ?? entry.usage ?? "", 26, Muted, false);
            creditContentHeight += 24; creditEntryCount++;
            all.AppendLine(entry.title).AppendLine(entry.role ?? entry.usage).AppendLine();
        }
        creditContentHeight += 28;
        AddCreditBlock("사용 자료와 출처", 38, Foreground, true);
        foreach (var entry in document.assets)
        {
            string usage = string.IsNullOrWhiteSpace(entry.displayUsage) ? entry.usage ?? "" : entry.displayUsage;
            string heading = usage + " — " + (entry.title ?? "");
            AddCreditBlock(heading, 27, Foreground, false);
            var details = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(entry.creator)) details.Append(entry.creator);
            if (!string.IsNullOrWhiteSpace(entry.license))
            { if (details.Length > 0) details.Append(" · "); details.Append(entry.license); }
            if (!string.IsNullOrWhiteSpace(entry.url))
            { if (details.Length > 0) details.AppendLine(); details.Append(entry.url); }
            if (details.Length > 0) AddCreditBlock(details.ToString(), 23, Muted, false);
            creditContentHeight += 30; creditEntryCount++;
            all.AppendLine(heading).AppendLine(details.ToString()).AppendLine();
        }
        creditContentHeight += 20;
        AddCreditBlock("THANK YOU FOR PLAYING", 38, Foreground, true);
        creditContentHeight += 32; creditContentText = all.ToString();
        creditScroll.y = Mathf.Clamp(creditScroll.y, 0, CreditsViewerMaxScroll);
    }
    void AddCreditBlock(string text, int size, Color color, bool title)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        int actualSize = title ? size : Mathf.RoundToInt(size * PreviewTextScale);
        var style = new GUIStyle {
            font = title ? titleFont : bodyFont, fontSize = actualSize,
            wordWrap = true, richText = false, padding = new RectOffset(0, 0, 0, 0),
            alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Normal
        };
        float height = Mathf.Max(actualSize * 1.4f, style.CalcHeight(new GUIContent(text), CreditsContentWidth)) + 8;
        creditBlocks.Add(new CreditBlock { text = text, size = size, color = color, title = title,
            top = creditContentHeight, height = height });
        creditContentHeight += height + (title ? 24 : 8);
    }
    void HandleCreditsViewerInput()
    {
        Event e = Event.current;
        if (e == null) return;
        if (e.type == EventType.ScrollWheel && CreditsViewport.Contains(Pointer))
        { ScrollCreditsViewer(e.delta.y * 52); e.Use(); }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        { CloseCreditsViewer(); e.Use(); }
    }
    void DrawCreditsViewer()
    {
        BuildCreditsViewerLayout();
        PaintBrush(new Rect(340, 92, 1240, 900), new Color(.025f, .025f, .035f, .985f));
        DrawText(new Rect(420, 115, 1080, 40), "PARALLAX", 25, Muted, TextAnchor.MiddleLeft, true);
        DrawText(new Rect(420, 158, 1080, 58), "크레딧", 44, Foreground, TextAnchor.MiddleLeft);
        creditScroll = GUI.BeginScrollView(CreditsViewport, creditScroll,
            new Rect(0, 0, CreditsContentWidth, creditContentHeight), false, true);
        foreach (var block in creditBlocks)
        {
            if (block.top + block.height < creditScroll.y || block.top > creditScroll.y + CreditsViewport.height) continue;
            DrawText(new Rect(0, block.top, CreditsContentWidth, block.height), block.text,
                block.size, block.color, TextAnchor.UpperLeft, block.title);
        }
        GUI.EndScrollView();
        creditScroll.y = Mathf.Clamp(creditScroll.y, 0, CreditsViewerMaxScroll);
        DrawText(new Rect(420, 907, 720, 42), "마우스 휠 · 스크롤     Esc · 돌아가기", 23, Muted);
        if (Button(new Rect(1220, 901, 280, 62), "돌아가기", "credits-back", false, true, 28))
            CloseCreditsViewer();
    }
}
