using System.Diagnostics;
using AutoHDR.Database;
using AutoHDR.Models;
using AutoHDR.Mods;

namespace AutoHDR.UI;

public sealed class HdrOptionsForm : Form
{
    public HdrOptionsForm(
        InstalledGame game,
        GameIdentityMatch? nativeMatch,
        HdrGame? hdr10Metadata,
        IReadOnlyList<HdrModMatch>? mods,
        AppTheme theme,
        FileLogger logger)
    {
        Text = $"HDR Options — {game.Name}";
        Icon = AppIcon.Create();
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(650, mods is { Count: > 0 } ? 500 : 350);
        MinimumSize = new Size(600, 330);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20),
            Tag = "content"
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var title = new Label
        {
            Text = game.Name,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(title, 0, 0);

        var capability = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12, 8, 12, 8),
            Tag = "section"
        };
        capability.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        capability.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        capability.Controls.Add(MakeCapability("NATIVE HDR", nativeMatch is null ? "Unknown" : nativeMatch.Entry.NativeHdr ? "Supported" : "SDR / Disabled", nativeMatch?.Entry.Source ?? "No verified database identity"), 0, 0);
        capability.SetRowSpan(capability.GetControlFromPosition(0, 0)!, 2);
        capability.Controls.Add(MakeCapability("HDR10+ GAMING", hdr10Metadata?.Hdr10PlusGaming == true ? "Supported" : "Not listed", hdr10Metadata?.Hdr10PlusSource ?? "No HDR10+ capability record"), 1, 0);
        capability.SetRowSpan(capability.GetControlFromPosition(1, 0)!, 2);
        root.Controls.Add(capability, 0, 1);

        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 12, 0, 0),
            Tag = "content"
        };

        if (nativeMatch is not null)
        {
            body.Controls.Add(new Label
            {
                Text = $"Identity: {nativeMatch.MatchType} · {nativeMatch.ConfidenceLabel} · {nativeMatch.Score}%",
                AutoSize = true,
                Tag = "muted",
                Margin = new Padding(0, 0, 0, 10)
            });
        }

        if (mods is { Count: > 0 })
        {
            body.Controls.Add(new Label
            {
                Text = "HDR MODS — CANARY",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Margin = new Padding(0, 4, 0, 8),
                Tag = "muted"
            });
            foreach (var mod in mods)
            {
                var panel = new TableLayoutPanel
                {
                    Width = 575,
                    Height = 58,
                    ColumnCount = 3,
                    RowCount = 1,
                    Margin = new Padding(0, 0, 0, 8),
                    Padding = new Padding(10, 8, 10, 8),
                    Tag = "section"
                };
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
                panel.Controls.Add(new Label { Text = mod.Provider, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                panel.Controls.Add(new Label { Text = $"{mod.ReadinessLabel}\n{mod.UpstreamStatus}", Dock = DockStyle.Fill, AutoEllipsis = true, Tag = "muted", TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
                var link = new Button { Text = "View Project ↗", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Tag = "toolbar-button", Cursor = Cursors.Hand };
                link.Click += (_, _) => Open(mod.Url, logger);
                panel.Controls.Add(link, 2, 0);
                body.Controls.Add(panel);
            }
        }
        else
        {
            body.Controls.Add(new Label
            {
                Text = "Capability details are sourced from the local TrueAuto HDR database and verified identity metadata.",
                AutoSize = true,
                MaximumSize = new Size(570, 0),
                Tag = "muted"
            });
        }
        root.Controls.Add(body, 0, 2);

        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK, Padding = new Padding(12, 0, 12, 0), Tag = "toolbar-button" };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Tag = "content" };
        footer.Controls.Add(close);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);
        AcceptButton = close;
        CancelButton = close;
        ThemeManager.Apply(this, theme);
    }

    private static Control MakeCapability(string label, string value, string source)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4),
            Tag = "section-layout"
        };
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold), Tag = "muted" });
        panel.Controls.Add(new Label { Text = value, AutoSize = true, Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold) });
        panel.Controls.Add(new Label { Text = source, AutoSize = true, MaximumSize = new Size(270, 0), Tag = "muted" });
        return panel;
    }

    private static void Open(string url, FileLogger logger)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) { logger.Log($"Could not open HDR mod source: {ex.Message}"); }
    }
}
