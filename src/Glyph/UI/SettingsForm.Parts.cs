using System.ComponentModel;
using System.Reflection;
using Glyph.Options;
using Glyph.Render;
using Glyph.Widgets;

namespace Glyph.UI;

/// The Design page's right side when a widget is picked: the part you clicked (its own options only), the frame that
/// part is drawn with (Look), and the widget's animations (tweens, with ready-made ones and a Play button).
internal sealed partial class SettingsForm
{
    enum Pane { Props, Look, Anims }

    Part? selPart;
    Pane pane = Pane.Props;
    AnimationsFile anims = new();
    bool dirtyAnims;

    readonly FlowLayoutPanel paneButtons = new() { Dock = DockStyle.Top, Height = 44, WrapContents = false, Padding = new Padding(0, 4, 0, 4) };
    readonly Panel propsPane = new() { Dock = DockStyle.Fill };
    readonly Panel lookPane = new() { Dock = DockStyle.Fill, Visible = false };
    readonly Panel animPane = new() { Dock = DockStyle.Fill, Visible = false };
    readonly PropertyGrid lookGrid = Grid();
    readonly PropertyGrid animGrid = Grid();
    readonly Label lookInfo = new() { Dock = DockStyle.Top, Height = 92, Padding = new Padding(2, 4, 2, 4), ForeColor = Dark.Dim };
    readonly FlowLayoutPanel lookButtons = new() { Dock = DockStyle.Top, Height = 44, WrapContents = false };
    readonly ListBox animList = new() { Dock = DockStyle.Top, Height = 150, IntegralHeight = false };
    readonly FlowLayoutPanel animButtons = new() { Dock = DockStyle.Top, Height = 44, WrapContents = false };
    readonly Label animInfo = new() { Dock = DockStyle.Top, Height = 58, Padding = new Padding(2, 4, 2, 4), ForeColor = Dark.Dim };
    Button? bProps, bLook, bAnims, bWhole;
    FrameOptions? lookFrame;

    /// Puts the three panes and their buttons into the Design page's right side.
    void BuildPanes(Control host)
    {
        Button PB(string text, Action a) { var b = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(6, 0, 6, 0), Margin = new Padding(0, 0, 6, 0) }; b.Click += (_, _) => a(); paneButtons.Controls.Add(b); return b; }
        bProps = PB("Properties", () => ShowPane(Pane.Props));
        bLook = PB("Look", () => ShowPane(Pane.Look));
        bAnims = PB("Animations", () => ShowPane(Pane.Anims));
        bWhole = PB("Whole widget", () => { selPart = null; ShowSelection(); });

        propsPane.Controls.Add(designGrid);

        Button LB(string text, Action a) { var b = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(6, 0, 6, 0), Margin = new Padding(0, 4, 6, 4) }; b.Click += (_, _) => a(); lookButtons.Controls.Add(b); return b; }
        LB("Give this part its own copy", OwnFrame);
        LB("Frames page", () => { if (lookFrame != null) ShowPage("Frames"); });
        lookPane.Controls.Add(lookGrid);
        lookPane.Controls.Add(lookButtons);
        lookPane.Controls.Add(lookInfo);
        lookGrid.PropertyValueChanged += (_, _) => { Changed(ref dirtyFrames); SaveNow(); };

        Button AB(string text, Action a) { var b = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(6, 0, 6, 0), Margin = new Padding(0, 4, 6, 4) }; b.Click += (_, _) => a(); animButtons.Controls.Add(b); return b; }
        Button? add = null;
        add = AB("Add ▾", () => DropDown(add!, TweenOptions.Presets.Select(p => (p.name, (Action)(() => AddTween(p.make()))))
            .Append(("Empty tween", (Action)(() => AddTween(new TweenOptions()))))));
        AB("Remove", RemoveTween);
        AB("▶ Play", () => { SaveNow(); preview?.Replay(); });
        animPane.Controls.Add(animGrid);
        animPane.Controls.Add(animButtons);
        animPane.Controls.Add(animList);
        animPane.Controls.Add(animInfo);
        animList.SelectedIndexChanged += (_, _) => animGrid.SelectedObject = animList.SelectedItem;
        animGrid.PropertyValueChanged += (_, _) => { var t = animList.SelectedItem; RefreshTweens(t as TweenOptions); Changed(ref dirtyAnims); SaveNow(); };

        // one box holds the three panes (only one shows), so docking leaves room for the buttons above
        var box = new Panel { Dock = DockStyle.Fill };
        box.Controls.Add(propsPane);
        box.Controls.Add(lookPane);
        box.Controls.Add(animPane);
        host.Controls.Add(box);
        host.Controls.Add(paneButtons);
    }

    WidgetOptions? SelWidget => sel == Sel.Widget && selIndex >= 0 && selIndex < widgets.Widgets.Count ? widgets.Widgets[selIndex] : null;

    void ShowPane(Pane p)
    {
        pane = SelWidget == null ? Pane.Props : p;
        propsPane.Visible = pane == Pane.Props; lookPane.Visible = pane == Pane.Look; animPane.Visible = pane == Pane.Anims;
        foreach (var (b, on) in new[] { (bProps, pane == Pane.Props), (bLook, pane == Pane.Look), (bAnims, pane == Pane.Anims) })
            if (b != null) { b.BackColor = on ? Dark.Accent : Dark.Raised; b.ForeColor = on ? Color.White : Dark.Text; b.FlatAppearance.BorderColor = on ? Dark.Accent : Dark.Line; }
        if (pane == Pane.Look) RefreshLook();
        if (pane == Pane.Anims) RefreshTweens();
    }

    /// Fills the right side for the current selection (a widget and maybe one of its parts).
    void ShowSelection()
    {
        var w = SelWidget;
        paneButtons.Visible = w != null;
        if (w == null) { ShowPane(Pane.Props); return; }
        if (selPart is Part part && part.Categories != null)
        {
            designGrid.SelectedObject = new PartView(w, PartView.Expand(w, part.Categories).Distinct().ToList());
            selLabel.Text = $"{w}  ›  {part.Name}";
        }
        else
        {
            designGrid.SelectedObject = w;
            selLabel.Text = "Widget: " + w + (selPart is Part p2 ? "  ›  " + p2.Name : "");
        }
        designGrid.ExpandAllGridItems();
        Friendly.Tidy(designGrid);
        if (bWhole != null) bWhole.Visible = selPart?.Categories != null;
        if (bLook != null) bLook.Enabled = true;
        ShowPane(pane);
    }

    // ───────── Look: the frame a part is drawn with ─────────

    /// The option that names this part's frame, and its value ("" when the part has none).
    (Action<string>? set, string value) FrameOf(WidgetOptions w, Part? part)
    {
        var prop = part?.FrameProp ?? "Frame";
        if (prop.Length == 0) return (null, "");
        if (prop.StartsWith("theme:"))
        {
            var ui = theme.Options.Ui;
            var tp = ui.GetType().GetProperty(prop[6..]);
            if (tp == null) return (null, "");
            return (v => { tp.SetValue(ui, v); MarkTheme(); }, (string)(tp.GetValue(ui) ?? ""));
        }
        var p = w.GetType().GetProperty(prop);
        if (p == null || p.PropertyType != typeof(string)) return (null, "");
        var value = (string)(p.GetValue(w) ?? "");
        if (value == "auto" && prop == "Frame") value = theme.Options.Ui.PanelFrame;
        return (v => { p.SetValue(w, v); MarkWidgets(); }, value);
    }

    void RefreshLook()
    {
        var w = SelWidget;
        lookFrame = null;
        if (w == null) return;
        var (_, name) = FrameOf(w, selPart);
        lookFrame = theme.Frames.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var what = selPart?.Name ?? "This widget";
        if (lookFrame == null)
        {
            lookInfo.Text = name.Length == 0 || name == "none"
                ? $"{what} isn't drawn with a frame. \"Give this part its own copy\" makes one for it (then style it here)."
                : $"{what} uses the frame \"{name}\", which isn't in this theme's frames yet.";
            lookGrid.SelectedObject = null;
            return;
        }
        lookInfo.Text = $"{what} is drawn with the frame \"{lookFrame.Name}\".  {FrameUses(lookFrame.Name)}  Changing it changes all of those.";
        lookGrid.SelectedObject = lookFrame;
        lookGrid.ExpandAllGridItems();
    }

    /// Copies the part's frame (or a plain one) under a new name and points the part at it.
    void OwnFrame()
    {
        var w = SelWidget;
        if (w == null) return;
        var (set, name) = FrameOf(w, selPart);
        if (set == null) { status.Text = "This part can't have a frame of its own."; return; }
        var basis = theme.Frames.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var copy = basis != null ? Binder.Clone(basis) : new FrameOptions();
        var stem = ((w.Name.Length > 0 ? w.Name : w.Type) + "-" + (selPart?.Name ?? "box")).ToLowerInvariant().Replace(' ', '-');
        var fresh = stem; int n = 2;
        while (theme.Frames.Any(f => f.Name.Equals(fresh, StringComparison.OrdinalIgnoreCase))) fresh = stem + "-" + n++;
        copy.Name = fresh;
        theme.Frames.Add(copy);
        set(fresh);
        RefreshFrames();
        Changed(ref dirtyFrames); SaveNow();
        RefreshLook();
        status.Text = $"Made the frame \"{fresh}\" for this part.";
    }

    // ───────── Animations ─────────

    bool Targets(TweenOptions t, WidgetOptions w)
    {
        var target = t.Target.Trim();
        return target == "*" || (target.StartsWith("type:", StringComparison.OrdinalIgnoreCase) && target[5..].Trim().Equals(w.Type, StringComparison.OrdinalIgnoreCase))
               || (w.Name.Length > 0 && target.Equals(w.Name, StringComparison.OrdinalIgnoreCase));
    }

    void RefreshTweens(TweenOptions? select = null)
    {
        var w = SelWidget;
        animList.BeginUpdate(); animList.Items.Clear();
        if (w != null) foreach (var t in anims.Tweens.Where(t => Targets(t, w))) animList.Items.Add(t);
        animList.EndUpdate();
        if (select != null && animList.Items.Contains(select)) animList.SelectedItem = select;
        else if (animList.Items.Count > 0) animList.SelectedIndex = 0;
        animGrid.SelectedObject = animList.SelectedItem;
        animGrid.ExpandAllGridItems();
        animInfo.Text = animList.Items.Count == 0
            ? "No animations on this widget yet. Add ▾ has ready-made ones (fade in, rise in, lift on hover, bob…)."
            : "▶ Play replays appear and loop tweens. For hover tweens, tick Animate and move the mouse over the widget.";
    }

    void AddTween(TweenOptions t)
    {
        var w = SelWidget;
        if (w == null) return;
        if (w.Name.Trim().Length == 0)
        {
            // tweens find their widget by name: give it one
            var stem = w.Type; int n = 1;
            while (widgets.Widgets.Any(x => x.Name.Equals(stem + n, StringComparison.OrdinalIgnoreCase))) n++;
            w.Name = stem + n;
            MarkWidgets();
            designGrid.Refresh();
        }
        t.Target = w.Name;
        anims.Tweens.Add(t);
        Changed(ref dirtyAnims); SaveNow();
        RefreshTweens(t);
        preview?.Replay();
    }

    void RemoveTween()
    {
        if (animList.SelectedItem is not TweenOptions t) return;
        anims.Tweens.Remove(t);
        Changed(ref dirtyAnims); SaveNow();
        RefreshTweens();
    }

    /// Where a frame is used (widgets and bar modules: any frame option, not just Frame).
    IEnumerable<string> FrameUsers(string name)
    {
        bool Is(object? v) => v is string s && s.Equals(name, StringComparison.OrdinalIgnoreCase);
        foreach (var w in widgets.Widgets)
            foreach (var p in w.GetType().GetProperties().Where(p => p.PropertyType == typeof(string) && p.Name != "Frame" && p.GetCustomAttribute<TypeConverterAttribute>()?.ConverterTypeName.Contains(nameof(FrameNameConverter)) == true))
                if (Is(p.GetValue(w))) yield return $"widget \"{w}\" ({Friendly.Spaced(p.Name).ToLowerInvariant()})";
        foreach (var m in bar.Left.Concat(bar.Center).Concat(bar.Right))
            foreach (var p in m.GetType().GetProperties().Where(p => p.PropertyType == typeof(string) && p.Name != "Frame" && p.GetCustomAttribute<TypeConverterAttribute>()?.ConverterTypeName.Contains(nameof(FrameNameConverter)) == true))
                if (Is(p.GetValue(m))) yield return $"module {m}";
    }
}
