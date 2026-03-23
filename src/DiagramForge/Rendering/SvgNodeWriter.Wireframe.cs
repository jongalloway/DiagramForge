using System.Runtime.CompilerServices;
using System.Text;
using DiagramForge.Models;

namespace DiagramForge.Rendering;

internal static partial class SvgNodeWriter
{
    // ── Wireframe rendering ───────────────────────────────────────────────────

    private const double WfStroke = 1.2;
    private const double WfRadius = 4.0;
    private const double WfCheckmarkInsetRatio = 0.2;  // relative inset for tick path endpoints
    private const double WfDropdownChevronSize = 4.5;  // half-width of the dropdown chevron arrow

    /// <summary>
    /// Wireframe color palette derived from four semantic theme colors:
    /// <see cref="Theme.BackgroundColor"/>, <see cref="Theme.TextColor"/>,
    /// <see cref="Theme.AccentColor"/>, and <see cref="Theme.SubtleTextColor"/>.
    /// </summary>
    private sealed record WireframePalette
    {
        public required string CardFill { get; init; }
        public required string CardBorder { get; init; }
        public required string HeaderFill { get; init; }
        public required string ButtonFill { get; init; }
        public required string ButtonText { get; init; }
        public required string InputBorder { get; init; }
        public required string InputBg { get; init; }
        public required string InputPlaceholder { get; init; }
        public required string TextColor { get; init; }
        public required string SubtleText { get; init; }
        public required string DividerColor { get; init; }
        public required string BadgeFill { get; init; }
        public required string BadgeBorder { get; init; }
        public required string BadgeText { get; init; }
        public required string ImageBg { get; init; }
        public required string ImageBorder { get; init; }
        public required string ImageX { get; init; }
        public required string TabActiveFill { get; init; }
        public required string TabInactiveFill { get; init; }
        public required string TabBorder { get; init; }
        public required string TabActiveText { get; init; }
        public required string TabInactiveText { get; init; }
        public required string CheckboxBorder { get; init; }
        public required string CheckColor { get; init; }
        public required string ToggleOnFill { get; init; }
        public required string ToggleOffFill { get; init; }
        public required string KnobFill { get; init; }

        public static WireframePalette FromTheme(Theme theme)
        {
            string bg = theme.BackgroundColor;
            string fg = theme.TextColor;
            string muted = theme.SubtleTextColor;
            string accent = theme.AccentColor;
            bool isLight = ColorUtils.IsLight(bg);

            // Strong foreground (buttons, check marks)
            string strong = ColorUtils.Blend(fg, bg, 0.15);
            // Inverse text (button labels, toggle knob)
            string inverse = isLight
                ? ColorUtils.Blend(bg, "#FFFFFF", 0.05)
                : ColorUtils.Blend(bg, "#000000", 0.05);

            return new WireframePalette
            {
                CardFill = ColorUtils.Blend(bg, fg, isLight ? 0.02 : 0.06),
                CardBorder = ColorUtils.Blend(muted, bg, 0.40),
                HeaderFill = ColorUtils.Blend(bg, fg, isLight ? 0.08 : 0.12),
                ButtonFill = strong,
                ButtonText = inverse,
                InputBorder = muted,
                InputBg = isLight ? ColorUtils.Blend(bg, "#FFFFFF", 0.30) : ColorUtils.Blend(bg, fg, 0.06),
                InputPlaceholder = ColorUtils.Blend(muted, bg, 0.20),
                TextColor = fg,
                SubtleText = muted,
                DividerColor = ColorUtils.Blend(muted, bg, 0.55),
                BadgeFill = ColorUtils.Blend(bg, accent, isLight ? 0.12 : 0.18),
                BadgeBorder = ColorUtils.Blend(accent, bg, 0.40),
                BadgeText = ColorUtils.Blend(accent, fg, 0.30),
                ImageBg = ColorUtils.Blend(bg, muted, 0.15),
                ImageBorder = ColorUtils.Blend(muted, bg, 0.20),
                ImageX = ColorUtils.Blend(muted, bg, 0.30),
                TabActiveFill = bg,
                TabInactiveFill = ColorUtils.Blend(bg, fg, isLight ? 0.08 : 0.12),
                TabBorder = ColorUtils.Blend(muted, bg, 0.35),
                TabActiveText = fg,
                TabInactiveText = muted,
                CheckboxBorder = ColorUtils.Blend(fg, bg, 0.50),
                CheckColor = ColorUtils.Blend(fg, bg, 0.10),
                ToggleOnFill = ColorUtils.Blend(fg, bg, 0.35),
                ToggleOffFill = ColorUtils.Blend(muted, bg, 0.40),
                KnobFill = inverse,
            };
        }
    }

    /// <summary>
    /// Cache keyed by <see cref="Theme"/> identity so that <see cref="WireframePalette.FromTheme"/>
    /// is computed at most once per distinct theme per process lifetime.
    /// <see cref="ConditionalWeakTable{TKey,TValue}"/> is used so cached entries are automatically
    /// discarded when the theme object is GC-collected.
    /// </summary>
    private static readonly ConditionalWeakTable<Theme, WireframePalette> s_paletteCache = new();

    private static void AppendWireframeNode(StringBuilder sb, Node node, string kind, Theme theme)
    {
        var p = s_paletteCache.GetValue(theme, static t => WireframePalette.FromTheme(t));

        switch (kind)
        {
            case "column":
            case "row":
                // Layout-only containers — invisible, no SVG output.
                if (node.Metadata.ContainsKey("wireframe:isRoot"))
                    return;
                // Non-root column/row: also invisible (pure layout helpers).
                return;

            case "card":
                AppendWfCard(sb, node, theme, p.CardFill, p.CardBorder, p, isHeader: false);
                break;

            case "header":
                AppendWfCard(sb, node, theme, p.HeaderFill, p.CardBorder, p, isHeader: true);
                break;

            case "footer":
                AppendWfCard(sb, node, theme, p.HeaderFill, p.CardBorder, p, isHeader: false);
                break;

            case "button":
                AppendWfButton(sb, node, theme, p);
                break;

            case "textinput":
                AppendWfTextInput(sb, node, theme, p);
                break;

            case "checkbox":
                AppendWfCheckbox(sb, node, theme, p);
                break;

            case "radio":
                AppendWfRadio(sb, node, theme, p);
                break;

            case "toggle":
                AppendWfToggle(sb, node, theme, p);
                break;

            case "dropdown":
                AppendWfDropdown(sb, node, theme, p);
                break;

            case "tabs":
                AppendWfTabs(sb, node, theme, p);
                break;

            case "badge":
                AppendWfBadge(sb, node, theme, p);
                break;

            case "image":
                AppendWfImage(sb, node, theme, p);
                break;

            case "divider":
                AppendWfDivider(sb, node, theme, p);
                break;

            case "heading":
                AppendWfHeading(sb, node, theme, p);
                break;

            case "text":
                AppendWfText(sb, node, theme, p);
                break;
        }
    }

    // Card / Header / Footer surface

    private static void AppendWfCard(StringBuilder sb, Node node, Theme theme, string fill, string border, WireframePalette p, bool isHeader)
    {
        string rx = SvgRenderSupport.F(isHeader ? 0 : WfRadius);
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{rx}" ry="{rx}" fill="{SvgRenderSupport.Escape(fill)}" stroke="{SvgRenderSupport.Escape(border)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double labelY = fontSize + 8;
            double labelX = 12;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(labelX)}" y="{SvgRenderSupport.F(labelY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" font-weight="bold" fill="{SvgRenderSupport.Escape(p.SubtleText)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Button

    private static void AppendWfButton(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(WfRadius)}" ry="{SvgRenderSupport.F(WfRadius)}" fill="{SvgRenderSupport.Escape(p.ButtonFill)}" stroke="none"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize;
            double textX = node.Width / 2;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(textX)}" y="{SvgRenderSupport.F(textY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.ButtonText)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Text input

    private static void AppendWfTextInput(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(WfRadius)}" ry="{SvgRenderSupport.F(WfRadius)}" fill="{SvgRenderSupport.Escape(p.InputBg)}" stroke="{SvgRenderSupport.Escape(p.InputBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="8" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.InputPlaceholder)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Checkbox

    private static void AppendWfCheckbox(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        const double boxSize = 14;
        bool isChecked = node.Metadata.TryGetValue("wireframe:checked", out var cv) && cv is true;

        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");

        double topY = (node.Height - boxSize) / 2;
        sb.AppendLine($"""    <rect x="0" y="{SvgRenderSupport.F(topY)}" width="{SvgRenderSupport.F(boxSize)}" height="{SvgRenderSupport.F(boxSize)}" rx="2" ry="2" fill="{SvgRenderSupport.Escape(p.InputBg)}" stroke="{SvgRenderSupport.Escape(p.CheckboxBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        if (isChecked)
        {
            double cx = boxSize / 2;
            double cy = topY + boxSize / 2;
            // Checkmark tick: two-segment polyline (left-bottom valley → right-top peak)
            double inset = boxSize * WfCheckmarkInsetRatio;
            sb.AppendLine($"""    <polyline points="{SvgRenderSupport.F(inset)},{SvgRenderSupport.F(cy)} {SvgRenderSupport.F(cx * 0.75)},{SvgRenderSupport.F(topY + boxSize - inset)} {SvgRenderSupport.F(boxSize - inset)},{SvgRenderSupport.F(topY + inset)}" fill="none" stroke="{SvgRenderSupport.Escape(p.CheckColor)}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>""");
        }

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(boxSize + 6)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Radio button

    private static void AppendWfRadio(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        const double circleR = 7;
        const double dotR = 3.5;
        bool isChecked = node.Metadata.TryGetValue("wireframe:checked", out var cv) && cv is true;

        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");

        double cy = node.Height / 2;
        sb.AppendLine($"""    <circle cx="{SvgRenderSupport.F(circleR)}" cy="{SvgRenderSupport.F(cy)}" r="{SvgRenderSupport.F(circleR)}" fill="{SvgRenderSupport.Escape(p.InputBg)}" stroke="{SvgRenderSupport.Escape(p.CheckboxBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        if (isChecked)
            sb.AppendLine($"""    <circle cx="{SvgRenderSupport.F(circleR)}" cy="{SvgRenderSupport.F(cy)}" r="{SvgRenderSupport.F(dotR)}" fill="{SvgRenderSupport.Escape(p.CheckColor)}"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = cy + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(circleR * 2 + 6)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Toggle

    private static void AppendWfToggle(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        bool isOn = node.Metadata.TryGetValue("wireframe:on", out var ov) && ov is true;

        const double pillW = 44;
        const double pillH = 22;
        const double knobR = 9;
        string pillFill = isOn ? p.ToggleOnFill : p.ToggleOffFill;
        double knobX = isOn ? (pillW - knobR - 3) : (knobR + 3);
        double knobY = pillH / 2;

        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        double pillTop = (node.Height - pillH) / 2;
        sb.AppendLine($"""    <rect x="0" y="{SvgRenderSupport.F(pillTop)}" width="{SvgRenderSupport.F(pillW)}" height="{SvgRenderSupport.F(pillH)}" rx="{SvgRenderSupport.F(pillH / 2)}" ry="{SvgRenderSupport.F(pillH / 2)}" fill="{SvgRenderSupport.Escape(pillFill)}" stroke="none"/>""");
        sb.AppendLine($"""    <circle cx="{SvgRenderSupport.F(knobX)}" cy="{SvgRenderSupport.F(pillTop + knobY)}" r="{SvgRenderSupport.F(knobR)}" fill="{SvgRenderSupport.Escape(p.KnobFill)}" stroke="{SvgRenderSupport.Escape(p.CardBorder)}" stroke-width="0.8"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(pillW + 8)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Dropdown

    private static void AppendWfDropdown(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(WfRadius)}" ry="{SvgRenderSupport.F(WfRadius)}" fill="{SvgRenderSupport.Escape(p.InputBg)}" stroke="{SvgRenderSupport.Escape(p.InputBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        // Chevron icon on the right
        double chevX = node.Width - 18;
        double chevY = node.Height / 2;
        sb.AppendLine($"""    <polyline points="{SvgRenderSupport.F(chevX - WfDropdownChevronSize)},{SvgRenderSupport.F(chevY - WfDropdownChevronSize * 0.6)} {SvgRenderSupport.F(chevX)},{SvgRenderSupport.F(chevY + WfDropdownChevronSize * 0.6)} {SvgRenderSupport.F(chevX + WfDropdownChevronSize)},{SvgRenderSupport.F(chevY - WfDropdownChevronSize * 0.6)}" fill="none" stroke="{SvgRenderSupport.Escape(p.SubtleText)}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>""");

        // Vertical separator line before chevron
        sb.AppendLine($"""    <line x1="{SvgRenderSupport.F(node.Width - 28)}" y1="5" x2="{SvgRenderSupport.F(node.Width - 28)}" y2="{SvgRenderSupport.F(node.Height - 5)}" stroke="{SvgRenderSupport.Escape(p.InputBorder)}" stroke-width="0.8"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="8" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Tab bar

    private static void AppendWfTabs(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        string[]? tabs = node.Metadata.TryGetValue("wireframe:tabs", out var tabsObj) ? tabsObj as string[] : null;
        int activeTab = node.Metadata.TryGetValue("wireframe:activeTab", out var atObj) && atObj is int at ? at : 0;

        if (tabs is null || tabs.Length == 0)
            return;

        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");

        double tabFontSize = theme.FontSize * 0.9;
        double tabW = node.Width / tabs.Length;

        // Bottom border line for the whole bar
        sb.AppendLine($"""    <line x1="0" y1="{SvgRenderSupport.F(node.Height)}" x2="{SvgRenderSupport.F(node.Width)}" y2="{SvgRenderSupport.F(node.Height)}" stroke="{SvgRenderSupport.Escape(p.TabBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        for (int i = 0; i < tabs.Length; i++)
        {
            bool isActive = i == activeTab;
            double tx = i * tabW;
            string tabFill = isActive ? p.TabActiveFill : p.TabInactiveFill;
            string tabText = isActive ? p.TabActiveText : p.TabInactiveText;
            string tabBotStroke = isActive ? p.TabActiveFill : p.TabBorder;

            sb.AppendLine($"""    <rect x="{SvgRenderSupport.F(tx)}" y="0" width="{SvgRenderSupport.F(tabW)}" height="{SvgRenderSupport.F(node.Height)}" fill="{SvgRenderSupport.Escape(tabFill)}" stroke="{SvgRenderSupport.Escape(p.TabBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

            // Cover the bottom border for the active tab
            if (isActive)
                sb.AppendLine($"""    <line x1="{SvgRenderSupport.F(tx + 1)}" y1="{SvgRenderSupport.F(node.Height)}" x2="{SvgRenderSupport.F(tx + tabW - 1)}" y2="{SvgRenderSupport.F(node.Height)}" stroke="{SvgRenderSupport.Escape(tabBotStroke)}" stroke-width="{SvgRenderSupport.F(WfStroke + 0.5)}"/>""");

            double textX = tx + tabW / 2;
            double textY = node.Height / 2 + tabFontSize * 0.35;
            string fontWeight = isActive ? " font-weight=\"bold\"" : string.Empty;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(textX)}" y="{SvgRenderSupport.F(textY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(tabFontSize)}"{fontWeight} fill="{SvgRenderSupport.Escape(tabText)}">{SvgRenderSupport.Escape(tabs[i])}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Badge

    private static void AppendWfBadge(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        double rx = node.Height / 2;
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(rx)}" ry="{SvgRenderSupport.F(rx)}" fill="{SvgRenderSupport.Escape(p.BadgeFill)}" stroke="{SvgRenderSupport.Escape(p.BadgeBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke * 0.8)}"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.8;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(node.Width / 2)}" y="{SvgRenderSupport.F(textY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.BadgeText)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Image placeholder

    private static void AppendWfImage(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(WfRadius)}" ry="{SvgRenderSupport.F(WfRadius)}" fill="{SvgRenderSupport.Escape(p.ImageBg)}" stroke="{SvgRenderSupport.Escape(p.ImageBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        // Diagonal cross lines
        sb.AppendLine($"""    <line x1="0" y1="0" x2="{SvgRenderSupport.F(node.Width)}" y2="{SvgRenderSupport.F(node.Height)}" stroke="{SvgRenderSupport.Escape(p.ImageX)}" stroke-width="1"/>""");
        sb.AppendLine($"""    <line x1="{SvgRenderSupport.F(node.Width)}" y1="0" x2="0" y2="{SvgRenderSupport.F(node.Height)}" stroke="{SvgRenderSupport.Escape(p.ImageX)}" stroke-width="1"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.85;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(node.Width / 2)}" y="{SvgRenderSupport.F(textY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.SubtleText)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Divider

    private static void AppendWfDivider(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        double midY = node.Y + node.Height / 2;
        sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(node.X)}" y1="{SvgRenderSupport.F(midY)}" x2="{SvgRenderSupport.F(node.X + node.Width)}" y2="{SvgRenderSupport.F(midY)}" stroke="{SvgRenderSupport.Escape(p.DividerColor)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");
    }

    // Heading

    private static void AppendWfHeading(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        if (string.IsNullOrWhiteSpace(node.Label.Text))
            return;

        int level = node.Metadata.TryGetValue("wireframe:headingLevel", out var lvObj) && lvObj is int lv ? lv : 1;
        double fontSize = node.Label.FontSize ?? (level == 1 ? 20.0 : (level == 2 ? 16.0 : 14.0));
        string fontWeight = level <= 2 ? " font-weight=\"bold\"" : string.Empty;
        double textY = node.Y + node.Height / 2 + fontSize * 0.35;
        sb.AppendLine($"""  <text x="{SvgRenderSupport.F(node.X)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}"{fontWeight} fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
    }

    // Text (body / bold)

    private static void AppendWfText(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        if (string.IsNullOrWhiteSpace(node.Label.Text))
            return;

        bool bold = node.Metadata.TryGetValue("wireframe:bold", out var bv) && bv is true;
        double fontSize = theme.FontSize;
        string fontWeightAttr = bold ? " font-weight=\"bold\"" : string.Empty;
        double textY = node.Y + node.Height / 2 + fontSize * 0.35;
        string color = bold ? p.TextColor : p.SubtleText;
        sb.AppendLine($"""  <text x="{SvgRenderSupport.F(node.X)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}"{fontWeightAttr} fill="{SvgRenderSupport.Escape(color)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
    }
}
