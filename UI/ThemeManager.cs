using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AutoHDR.UI;

public static class ThemeManager
{
    private static readonly Color DarkBackground = Color.FromArgb(15, 18, 22);
    private static readonly Color DarkHeader = Color.FromArgb(18, 22, 27);
    private static readonly Color DarkSurface = Color.FromArgb(24, 29, 35);
    private static readonly Color DarkCard = Color.FromArgb(27, 33, 40);
    private static readonly Color DarkCardHover = Color.FromArgb(34, 41, 49);
    private static readonly Color DarkInput = Color.FromArgb(31, 37, 44);
    private static readonly Color DarkText = Color.FromArgb(240, 242, 245);
    private static readonly Color DarkMuted = Color.FromArgb(173, 181, 190);
    private static readonly Color DarkGrid = Color.FromArgb(55, 63, 72);

    private static readonly Color LightBackground = Color.FromArgb(246, 247, 249);
    private static readonly Color LightHeader = Color.White;
    private static readonly Color LightSurface = Color.White;
    private static readonly Color LightCard = Color.FromArgb(250, 251, 252);
    private static readonly Color LightCardHover = Color.FromArgb(242, 245, 248);
    private static readonly Color LightMuted = Color.FromArgb(91, 101, 112);
    private static readonly Color LightGrid = Color.FromArgb(215, 220, 226);

    public static bool IsDark(AppTheme theme) => theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => IsWindowsDarkMode()
    };

    public static void Apply(Form form, AppTheme theme)
    {
        var dark = IsDark(theme);
        ApplyControl(form, dark);
        ApplyTitleBar(form, dark);
        form.Invalidate(true);
    }

    public static void ApplyWindowChrome(Form form, AppTheme theme)
    {
        if (!form.IsHandleCreated) return;
        ApplyTitleBar(form, IsDark(theme));
    }

    public static void Apply(ContextMenuStrip menu, AppTheme theme)
    {
        var dark = IsDark(theme);
        menu.Renderer = dark ? new ToolStripProfessionalRenderer(new DarkColorTable()) : new ToolStripSystemRenderer();
        menu.BackColor = dark ? DarkSurface : SystemColors.Control;
        menu.ForeColor = dark ? DarkText : SystemColors.ControlText;
        ApplyToolStripItems(menu.Items, dark);
    }

    public static void SetCardHover(Panel card, bool hover)
    {
        var dark = card.FindForm() is Form form && GetBrightness(form.BackColor) < 128;
        var surface = dark
            ? (hover ? DarkCardHover : DarkCard)
            : (hover ? LightCardHover : LightCard);

        card.BackColor = surface;
        foreach (Control child in card.Controls)
        {
            if ((child.Tag as string) == "card-layout")
                child.BackColor = surface;
        }
        card.Invalidate(true);
    }

    public static Color Accent(string name, bool dark)
    {
        return name.ToLowerInvariant() switch
        {
            "purple" => dark ? Color.FromArgb(175, 113, 255) : Color.FromArgb(117, 67, 196),
            "blue" => dark ? Color.FromArgb(99, 179, 255) : Color.FromArgb(41, 119, 201),
            "green" => dark ? Color.FromArgb(102, 202, 126) : Color.FromArgb(45, 145, 72),
            "amber" => dark ? Color.FromArgb(255, 183, 64) : Color.FromArgb(190, 119, 0),
            _ => dark ? Color.FromArgb(160, 170, 181) : Color.FromArgb(92, 103, 114)
        };
    }

    public static bool ControlIsDark(Control control)
        => control.FindForm() is Form form && GetBrightness(form.BackColor) < 128;

    private static int GetBrightness(Color color) => (color.R * 299 + color.G * 587 + color.B * 114) / 1000;

    private static void ApplyToolStripItems(ToolStripItemCollection items, bool dark)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = dark ? DarkSurface : SystemColors.Control;
            item.ForeColor = dark ? DarkText : SystemColors.ControlText;
            if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
            {
                menuItem.DropDown.BackColor = dark ? DarkSurface : SystemColors.Control;
                menuItem.DropDown.ForeColor = dark ? DarkText : SystemColors.ControlText;
                ApplyToolStripItems(menuItem.DropDownItems, dark);
            }
        }
    }

    private static void ApplyControl(Control control, bool dark)
    {
        var tag = control.Tag as string ?? string.Empty;

        if (tag is "header" or "header-layout")
        {
            control.BackColor = dark ? DarkHeader : LightHeader;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else if (tag == "section-layout")
        {
            // Avoid Transparent TableLayoutPanels: WinForms can leave stale paint
            // artifacts during resize/DPI relayout. Match the section surface.
            control.BackColor = dark ? DarkSurface : LightSurface;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else if (tag == "card-layout")
        {
            // Same reason as section-layout: use an opaque card surface so old
            // custom-painted borders are erased before the next frame.
            control.BackColor = dark ? DarkCard : LightCard;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else if (tag is "content" or "footer")
        {
            control.BackColor = dark ? DarkBackground : LightBackground;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else if (tag == "section")
        {
            control.BackColor = dark ? DarkSurface : LightSurface;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else if (tag.StartsWith("action-card:", StringComparison.Ordinal))
        {
            control.BackColor = dark ? DarkCard : LightCard;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else if (tag.StartsWith("card-icon:", StringComparison.Ordinal))
        {
            var accentName = tag["card-icon:".Length..];
            control.BackColor = Color.Transparent;
            control.ForeColor = Accent(accentName, dark);
        }
        else if (tag == "section-icon")
        {
            control.BackColor = Color.Transparent;
            control.ForeColor = Accent("purple", dark);
        }
        else if (tag == "muted")
        {
            control.BackColor = Color.Transparent;
            control.ForeColor = dark ? DarkMuted : LightMuted;
        }
        else
        {
            switch (control)
            {
                case Form:
                    control.BackColor = dark ? DarkBackground : LightBackground;
                    control.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;
                case GroupBox:
                case TableLayoutPanel:
                case FlowLayoutPanel:
                case Panel:
                    control.BackColor = dark ? DarkBackground : LightBackground;
                    control.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;
                case Button button:
                    button.UseVisualStyleBackColor = false;
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = dark ? DarkSurface : LightSurface;
                    button.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    button.FlatAppearance.BorderColor = dark ? DarkGrid : LightGrid;
                    button.FlatAppearance.MouseOverBackColor = dark ? DarkCardHover : LightCardHover;
                    break;
                case TextBox textBox:
                    textBox.BackColor = dark ? DarkInput : SystemColors.Window;
                    textBox.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = dark ? DarkInput : SystemColors.Window;
                    comboBox.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    break;
                case Label label:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;
                case DataGridView grid:
                    ApplyGrid(grid, dark);
                    break;
                default:
                    control.BackColor = dark ? DarkBackground : LightBackground;
                    control.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;
            }
        }

        foreach (Control child in control.Controls)
            ApplyControl(child, dark);
    }

    private static void ApplyGrid(DataGridView grid, bool dark)
    {
        grid.BackgroundColor = dark ? DarkBackground : LightBackground;
        grid.GridColor = dark ? DarkGrid : LightGrid;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? DarkSurface : Color.FromArgb(235, 238, 242);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = dark ? DarkText : SystemColors.ControlText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        grid.DefaultCellStyle.BackColor = dark ? DarkCard : Color.White;
        grid.DefaultCellStyle.ForeColor = dark ? DarkText : SystemColors.ControlText;
        grid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(48, 59, 71) : Color.FromArgb(220, 233, 248);
        grid.DefaultCellStyle.SelectionForeColor = dark ? Color.White : SystemColors.ControlText;
        grid.AlternatingRowsDefaultCellStyle.BackColor = dark ? Color.FromArgb(24, 30, 36) : Color.FromArgb(249, 250, 251);
    }

    private static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyTitleBar(Form form, bool dark)
    {
        if (!OperatingSystem.IsWindows() || !form.IsHandleCreated) return;
        try
        {
            // Important: do not read form.Handle until WinForms has created it
            // naturally. Accessing Handle from a constructor forces HWND creation
            // and can let Windows paint a partially-built/default-white form.
            var value = dark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, 20, ref value, sizeof(int));
        }
        catch { }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => DarkSurface;
        public override Color MenuBorder => DarkGrid;
        public override Color MenuItemBorder => DarkGrid;
        public override Color MenuItemSelected => DarkCardHover;
        public override Color MenuItemSelectedGradientBegin => DarkCardHover;
        public override Color MenuItemSelectedGradientEnd => DarkCardHover;
        public override Color MenuItemPressedGradientBegin => DarkCardHover;
        public override Color MenuItemPressedGradientMiddle => DarkCardHover;
        public override Color MenuItemPressedGradientEnd => DarkCardHover;
        public override Color ImageMarginGradientBegin => DarkSurface;
        public override Color ImageMarginGradientMiddle => DarkSurface;
        public override Color ImageMarginGradientEnd => DarkSurface;
        public override Color SeparatorDark => DarkGrid;
        public override Color SeparatorLight => DarkGrid;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
