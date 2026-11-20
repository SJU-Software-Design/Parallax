using UnityEngine;
using ShadeLink;

public partial class ParallaxGameShell : MonoBehaviour
{
    static readonly string[] AdminTravelStages = { "Tutorial", "Gallery", "Beach", "Eclipse" };
    string adminTravelStage;
    int adminTravelParentSelection;

    bool CanUseAdminTravel
    {
        get { return Administrator && InGameplayScene && GameProgressRuntime.Paused &&
            !GameProgressRuntime.Transitioning && !LoadingPresentationActive && !settingsOpen; }
    }
    public bool AdminTravelMenuOpen { get { return CanUseAdminTravel && pausePrompt == "travel"; } }
    public bool AdminTravelSectionsOpen { get { return AdminTravelMenuOpen && adminTravelStage != null; } }
    public string AdminTravelStage { get { return AdminTravelSectionsOpen ? adminTravelStage : null; } }
    public int AdminTravelSelection { get { return AdminTravelMenuOpen ? pausePromptSelection : -1; } }
    public int AdminTravelOptionCount
    {
        get { return !AdminTravelMenuOpen ? 0 : adminTravelStage == null ? AdminTravelStages.Length + 1 :
            AdminStageSections.ForStage(adminTravelStage).Count + 1; }
    }
    public string AdminTravelOptionLabel(int index)
    {
        int count = AdminTravelOptionCount;
        if (index < 0 || index >= count) return "";
        if (index == count - 1) return "돌아가기";
        return adminTravelStage == null ? AdminStageSections.StageLabel(AdminTravelStages[index]) :
            AdminStageSections.ForStage(adminTravelStage)[index].Label;
    }

    void ResetAdminTravelMenu()
    { adminTravelStage = null; adminTravelParentSelection = 0; }

    // These are the same actions used by the rendered controls and keyboard.
    // Navigating either level keeps the game paused; only a section starts a load.
    public bool OpenAdminTravelMenu()
    {
        if (!CanUseAdminTravel || (pausePrompt != null && pausePrompt != "travel")) return false;
        ResetAdminTravelMenu(); pausePrompt = "travel"; pausePromptSelection = 0; pauseSelection = 3;
        PlayUi("confirm"); return true;
    }
    public bool SelectAdminTravelOption(int index)
    {
        if (!AdminTravelMenuOpen || index < 0 || index >= AdminTravelOptionCount) return false;
        if (index == AdminTravelOptionCount - 1) return BackAdminTravelMenu();
        pausePromptSelection = index;
        if (adminTravelStage == null)
        {
            adminTravelParentSelection = index;
            adminTravelStage = AdminTravelStages[index];
            pausePromptSelection = 0; pointerMode = false;
            PlayUi("confirm"); return true;
        }
        var section = AdminStageSections.ForStage(adminTravelStage)[index];
        PlayUi("confirm");
        return GameProgressRuntime.AdminTravel(adminTravelStage, section.Index);
    }
    public bool BackAdminTravelMenu()
    {
        if (!AdminTravelMenuOpen) return false;
        if (adminTravelStage != null)
        {
            adminTravelStage = null;
            pausePromptSelection = adminTravelParentSelection;
        }
        else { pausePrompt = null; ResetAdminTravelMenu(); }
        pointerMode = false;
        PlayUi("back"); return true;
    }
    public bool MoveAdminTravelSelection(int direction)
    {
        int count = AdminTravelOptionCount;
        if (count == 0 || direction == 0) return false;
        int delta = direction % count;
        pausePromptSelection = (pausePromptSelection + delta + count) % count;
        pointerMode = false;
        if (Event.current != null) GUI.FocusControl(null);
        return true;
    }
    public bool HandleAdminTravelKey(KeyCode keyCode, bool shift = false)
    {
        if (!AdminTravelMenuOpen) return false;
        if (keyCode == KeyCode.UpArrow) return MoveAdminTravelSelection(-1);
        if (keyCode == KeyCode.DownArrow) return MoveAdminTravelSelection(1);
        if (keyCode == KeyCode.Tab) return MoveAdminTravelSelection(shift ? -1 : 1);
        if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter || keyCode == KeyCode.Space)
            return SelectAdminTravelOption(pausePromptSelection);
        if (keyCode == KeyCode.Escape) return BackAdminTravelMenu();
        return false;
    }

    public Rect AdminTravelPanelBounds
    {
        get
        {
            if (!AdminTravelMenuOpen) return Rect.zero;
            float height = Mathf.Min(950, 344 + AdminTravelOptionCount * 86);
            return new Rect(410, (ReferenceHeight - height) * .5f, 1100, height);
        }
    }
    public Rect AdminTravelOptionBounds(int index)
    {
        if (!AdminTravelMenuOpen || index < 0 || index >= AdminTravelOptionCount) return Rect.zero;
        Rect panel = AdminTravelPanelBounds;
        return new Rect(505, panel.y + 225 + index * 86, 910, 70);
    }
    void DrawAdminTravelMenu()
    {
        if (!AdminTravelMenuOpen) return;
        Rect panel = AdminTravelPanelBounds;
        PaintBrush(panel, new Color(.025f, .025f, .035f, .99f));
        DrawText(new Rect(505, panel.y + 34, 910, 38), "PARALLAX · ADMIN", 23, Muted, TextAnchor.MiddleCenter, true);
        string title = adminTravelStage == null ? "맵 이동" : AdminStageSections.StageLabel(adminTravelStage) + "  /  구간 선택";
        DrawText(new Rect(485, panel.y + 82, 950, 72), title, 46, Foreground, TextAnchor.MiddleCenter);
        DrawText(new Rect(505, panel.y + 163, 910, 38), adminTravelStage == null ?
            "이동할 맵을 선택하세요." : "시작할 구간을 선택하세요.", 25, Muted, TextAnchor.MiddleCenter);
        int count = AdminTravelOptionCount;
        for (int i = 0; i < count; i++)
        {
            Rect rect = AdminTravelOptionBounds(i);
            if (pointerMode && rect.Contains(Pointer)) pausePromptSelection = i;
            string id = "admin-travel-" + (adminTravelStage ?? "maps") + "-" + i;
            if (Button(rect, AdminTravelOptionLabel(i), id, pausePromptSelection == i, true, 32))
            { SelectAdminTravelOption(i); return; }
        }
        DrawText(new Rect(485, panel.yMax - 69, 950, 38), "↑↓ 선택  ·  Enter 이동  ·  Esc 돌아가기", 23, Muted, TextAnchor.MiddleCenter);
        Event key = Event.current;
        // Escape is polled once in UpdateInGameShell. Consuming it again here
        // would back out of both submenu levels on the same physical press.
        if (key != null && key.type == EventType.KeyDown && key.keyCode != KeyCode.Escape &&
            HandleAdminTravelKey(key.keyCode, key.shift)) key.Use();
    }
}
