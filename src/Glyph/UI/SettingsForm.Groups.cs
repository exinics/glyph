using Glyph.Widgets;

namespace Glyph.UI;

/// Shortcuts page > "Groups (menu tabs)…": the [[group]] tables of shortcuts.toml (an icon and text for each group,
/// which menu widgets show as tabs). Edits a copy; OK puts it back and marks the shortcuts as changed.
internal sealed partial class SettingsForm
{
    List<ShortcutGroup> groups = new();

    void EditGroups()
    {
        // every group a shortcut names gets an entry, so there is something to fill in
        var work = groups.Select(g => new ShortcutGroup { Name = g.Name, Icon = g.Icon, Subtitle = g.Subtitle, Description = g.Description, Label = g.Label }).ToList();
        foreach (var n in shortcuts.Select(s => s.Group.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            if (!work.Any(g => g.Name.Trim().Equals(n, StringComparison.OrdinalIgnoreCase))) work.Add(new ShortcutGroup { Name = n });

        using var f = new Form { Text = "Groups (menu tabs)", Width = 860, Height = 560, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, Font = Dark.UiFont };
        var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        var grid = Grid();
        var left = new Panel { Dock = DockStyle.Left, Width = 268, Padding = new Padding(10) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 84, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        var hint = new Label
        {
            Dock = DockStyle.Top, Height = 64, Padding = new Padding(10, 8, 10, 0),
            Text = "A menu widget shows each group as a tab, in this order. Put shortcuts in a group with their Group (Shortcuts page). Icon: {icon:name} from the Icons page.",
        };
        void Refill(ShortcutGroup? sel = null)
        {
            list.BeginUpdate(); list.Items.Clear();
            foreach (var g in work) list.Items.Add(g);
            list.EndUpdate();
            if (sel != null) list.SelectedItem = sel; else if (list.Items.Count > 0 && list.SelectedIndex < 0) list.SelectedIndex = 0;
            grid.SelectedObject = list.SelectedItem;
        }
        Button B(string text, Action a) { var b = new Button { Text = text, Width = 112, Height = 32, Margin = new Padding(0, 0, 4, 6) }; b.Click += (_, _) => a(); buttons.Controls.Add(b); return b; }
        B("Add", () =>
        {
            var name = Ask("New group", "Name (the shortcuts' Group):", "", null, out _);
            if (string.IsNullOrWhiteSpace(name)) return;
            var g = new ShortcutGroup { Name = name.Trim() };
            work.Add(g); Refill(g);
        });
        B("Remove", () => { if (list.SelectedItem is ShortcutGroup g) { int i = work.IndexOf(g); work.Remove(g); Refill(work.Count > 0 ? work[Math.Min(i, work.Count - 1)] : null); } });
        B("Move up", () => Move(-1));
        B("Move down", () => Move(1));
        void Move(int d)
        {
            if (list.SelectedItem is not ShortcutGroup g) return;
            int i = work.IndexOf(g), j = i + d;
            if (j < 0 || j >= work.Count) return;
            (work[i], work[j]) = (work[j], work[i]);
            Refill(g);
        }
        list.SelectedIndexChanged += (_, _) => grid.SelectedObject = list.SelectedItem;
        grid.PropertyValueChanged += (_, _) => { var g = list.SelectedItem; Refill(g as ShortcutGroup); };
        left.Controls.Add(list);
        left.Controls.Add(buttons);
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 96, Height = 34 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 96, Height = 34 };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        bottom.Controls.Add(cancel); bottom.Controls.Add(ok);
        f.Controls.Add(grid); f.Controls.Add(left); f.Controls.Add(hint); f.Controls.Add(bottom);
        f.AcceptButton = ok; f.CancelButton = cancel;
        Dark.Apply(f); Dark.Primary(ok);
        f.HandleCreated += (_, _) => Dark.TitleBar(f);
        Refill();
        if (f.ShowDialog(this) != DialogResult.OK) return;
        groups = work.Where(g => g.Name.Trim().Length > 0).ToList();
        MarkShortcuts();
        status.Text = $"{groups.Count} groups. Save to use them.";
    }
}
