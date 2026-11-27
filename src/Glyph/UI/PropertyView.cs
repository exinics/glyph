using System.ComponentModel;
using System.Drawing.Design;
using System.Reflection;
using System.Text;
using Glyph.Options;

namespace Glyph.UI;

/// Makes the settings window's property lists easier to read without touching the option classes:
/// categories in a sensible order (placement first, looks and links last), names spaced out ("OffsetX" -> "Offset X"),
/// On / Off instead of True / False, a swatch beside every colour, and nothing that can't be changed.
/// Settings window only (the files and the guide read the classes directly).
internal static class Friendly
{
    static bool registered;

    // earlier in the list = higher up; categories not named here go between the two lists
    static readonly string[] Head = { "Placement", "Module", "Menu", "Tabs", "Heading", "List", "Details", "Panel", "Text", "Clock" };
    static readonly string[] Tail = { "Box", "Colours", "Link", "Clicks" };

    /// The category with leading tabs so the property list sorts it into place (tabs don't show).
    public static string Ordered(string cat)
    {
        if (cat.Length > 0 && char.IsDigit(cat[0])) return cat;   // "1. Frame", "2. Shape": already in order
        int i = Array.IndexOf(Head, cat);
        if (i >= 0) return new string('\t', 40 - i) + cat;
        i = Array.IndexOf(Tail, cat);
        if (i >= 0) return new string('\t', Tail.Length - i) + cat;
        return new string('\t', 20) + cat;
    }

    public static string Plain(string cat) => cat.TrimStart('\t');

    public static string Spaced(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]))) sb.Append(' ');
            sb.Append(i > 0 && char.IsUpper(name[i]) && i + 1 < name.Length && char.IsLower(name[i + 1]) ? char.ToLowerInvariant(name[i]) : name[i]);
        }
        return sb.ToString();
    }

    /// Hooks the readable view onto every option class (once per settings window process).
    public static void Register()
    {
        if (registered) return;
        registered = true;
        foreach (var t in typeof(Friendly).Assembly.GetTypes())
        {
            if (!t.IsClass || t.Namespace == null || !t.Namespace.StartsWith("Glyph")) continue;
            if (t.IsSubclassOf(typeof(Control)) || t.IsSubclassOf(typeof(Component))) continue;
            bool options = t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => p.GetCustomAttribute<DescriptionAttribute>() != null || p.GetCustomAttribute<CategoryAttribute>() != null);
            if (!options) continue;
            TypeDescriptor.AddProvider(new Provider(TypeDescriptor.GetProvider(t)), t);
        }
    }

    /// Folds away the categories most people rarely need (Link, Clicks), after a list is filled.
    public static void Tidy(PropertyGrid g)
    {
        try
        {
            var item = g.SelectedGridItem;
            while (item?.Parent != null) item = item.Parent;
            if (item == null) return;
            foreach (GridItem c in item.GridItems)
                if (c.GridItemType == GridItemType.Category && Plain(c.Label ?? "") is "Link" or "Clicks") c.Expanded = false;
        }
        catch { }
    }

    /// Bigger text and a taller description box.
    public static void Style(PropertyGrid g)
    {
        g.Font = new Font("Segoe UI", 10.5f);
        g.HandleCreated += (_, _) =>
        {
            foreach (Control c in g.Controls)
                if (c.GetType().Name == "DocComment")
                {
                    c.Height = 104;
                    c.GetType().GetField("userSized", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(c, true);
                }
        };
    }

    sealed class Provider : TypeDescriptionProvider
    {
        public Provider(TypeDescriptionProvider parent) : base(parent) { }
        public override ICustomTypeDescriptor? GetTypeDescriptor(Type objectType, object? instance) => new Descriptor(base.GetTypeDescriptor(objectType, instance));
    }

    sealed class Descriptor : CustomTypeDescriptor
    {
        public Descriptor(ICustomTypeDescriptor? parent) : base(parent) { }
        public override PropertyDescriptorCollection GetProperties() => Wrap(base.GetProperties());
        public override PropertyDescriptorCollection GetProperties(Attribute[]? a) => Wrap(base.GetProperties(a));
        static PropertyDescriptorCollection Wrap(PropertyDescriptorCollection props) =>
            new(props.Cast<PropertyDescriptor>().Where(p => p.IsBrowsable && !p.IsReadOnly).Select(p => (PropertyDescriptor)new Prop(p)).ToArray(), true);
    }

    /// One property, shown in the readable way; everything else goes to the real one.
    sealed class Prop : PropertyDescriptor
    {
        readonly PropertyDescriptor inner;
        readonly bool colour;
        public Prop(PropertyDescriptor inner) : base(inner)
        {
            this.inner = inner;
            colour = inner.PropertyType == typeof(string) && inner.Attributes[typeof(ColorAttribute)] != null;
        }
        public override string Category => Ordered(inner.Category);
        public override string DisplayName => inner.DisplayName == inner.Name ? Spaced(inner.Name) : inner.DisplayName;
        public override Type ComponentType => inner.ComponentType;
        public override bool IsReadOnly => inner.IsReadOnly;
        public override Type PropertyType => inner.PropertyType;
        public override bool CanResetValue(object component) => inner.CanResetValue(component);
        public override object? GetValue(object? component) => inner.GetValue(component);
        public override void ResetValue(object component) => inner.ResetValue(component);
        public override void SetValue(object? component, object? value) => inner.SetValue(component, value);
        public override bool ShouldSerializeValue(object component) => inner.ShouldSerializeValue(component);
        public override TypeConverter Converter => inner.PropertyType == typeof(bool) ? OnOff.Instance : inner.Converter;
        public override object? GetEditor(Type editorBaseType) => colour && editorBaseType == typeof(UITypeEditor) ? Swatch.Instance : inner.GetEditor(editorBaseType);
    }
}

/// On / Off for true / false.
internal sealed class OnOff : BooleanConverter
{
    public static readonly OnOff Instance = new();
    public override object? ConvertTo(ITypeDescriptorContext? c, System.Globalization.CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is bool b ? (b ? "On" : "Off") : base.ConvertTo(c, culture, value, destinationType);
    public override object? ConvertFrom(ITypeDescriptorContext? c, System.Globalization.CultureInfo? culture, object value) =>
        value is string s ? s.Trim().ToLowerInvariant() is "on" or "true" or "yes" or "1" : base.ConvertFrom(c, culture, value);
}

/// Paints the colour a colour name stands for, beside the name (the drop-down of names still works).
internal sealed class Swatch : UITypeEditor
{
    public static readonly Swatch Instance = new();
    /// The colour for a palette name or #hex (set by the settings window from the theme being edited).
    public static Func<string, Color?>? Resolve;
    public override bool GetPaintValueSupported(ITypeDescriptorContext? context) => true;
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.None;
    public override void PaintValue(PaintValueEventArgs e)
    {
        var c = e.Value is string s ? Resolve?.Invoke(s.Trim()) : null;
        if (c is Color col) { using var b = new SolidBrush(col); e.Graphics.FillRectangle(b, e.Bounds); return; }
        using var p = new Pen(Dark.Dim);
        e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right - 1, e.Bounds.Top);   // none / auto: a slash
    }
}

/// Some of an object's options: only the categories that belong to one part of a widget (the Design page).
internal sealed class PartView : CustomTypeDescriptor
{
    readonly object target;
    readonly HashSet<string> cats;
    public PartView(object target, IEnumerable<string> categories) : base(TypeDescriptor.GetProvider(target).GetTypeDescriptor(target))
    {
        this.target = target;
        cats = new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase);
    }
    public override PropertyDescriptorCollection GetProperties() => GetProperties(null);
    public override PropertyDescriptorCollection GetProperties(Attribute[]? a) =>
        new(base.GetProperties(a).Cast<PropertyDescriptor>().Where(p => cats.Contains(Friendly.Plain(p.Category))).ToArray(), true);
    public override object? GetPropertyOwner(PropertyDescriptor? pd) => target;

    /// "*own" = the categories the object's own type adds (e.g. Clock for a clock widget).
    public static IEnumerable<string> Expand(object target, IEnumerable<string> categories)
    {
        foreach (var c in categories)
        {
            if (c != "*own") { yield return c; continue; }
            foreach (var p in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (p.GetCustomAttribute<CategoryAttribute>()?.Category is string cat) yield return cat;
        }
    }
}
