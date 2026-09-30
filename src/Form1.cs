using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;

namespace NovaBrowser;

public partial class Form1 : Form
{
    private TableLayoutPanel toolbar;
    private TabControl tabs;

    private ChromeButton backButton;
    private ChromeButton forwardButton;
    private ChromeButton refreshButton;
    private ChromeButton newTabButton;
    private ChromeButton goButton;
    private ChromeButton menuButton;
    private ChromeButton bookmarkButton;
    private ContextMenuStrip browserMenu;

    private TextBox addressBar;
    private FlowLayoutPanel bookmarksBar;
    private Label bookmarksTitle;
    private StatusStrip statusBar;
    private ToolStripStatusLabel pageStatusLabel;
    private ToolStripStatusLabel zoomLabel;
    private ToolStripButton zoomOutButton;
    private ToolStripButton zoomInButton;
    private readonly List<BrowserBookmark> bookmarks = new();
    private Dictionary<WebView2, string> displayedUrls = new();
    private const string NewTabUrl = "nova://newtab";
    private const string UserAgentBrand = "NovaBrowser/1.0";
    private Task<CoreWebView2Environment>? browserEnvironmentTask;
    private readonly HashSet<string> enabledFlags = new(StringComparer.Ordinal);

    private static readonly BrowserFlagDefinition[] BrowserFlagCatalog =
    {
        new("experimental-web-platform", "Experimental Web Platform features", "Enable experimental web APIs that are still under development.", "--enable-experimental-web-platform-features"),
        new("gpu-rasterization", "GPU rasterization", "Use the GPU for page rasterization when supported by the system.", "--enable-gpu-rasterization"),
        new("smooth-scrolling", "Smooth scrolling", "Enable animated scrolling behavior in web content.", "--enable-smooth-scrolling"),
        new("quic", "QUIC protocol", "Enable the QUIC transport protocol for compatible connections.", "--enable-quic"),
        new("zero-copy", "Zero-copy rasterizer", "Allow compatible graphics surfaces to be shared without an extra copy.", "--enable-zero-copy"),
        new("force-dark", "Auto dark mode for web content", "Apply Chromium's experimental dark theme to web pages.", "WebContentsForceDark"),
        new("parallel-downloading", "Parallel downloading", "Download supported files in multiple parallel parts.", "ParallelDownloading"),
        new("back-forward-cache", "Back-forward cache", "Keep eligible pages alive for faster back and forward navigation.", "BackForwardCache")
    };

    private sealed record BrowserBookmark(string Title, string Url);
    private sealed record BrowserFlagDefinition(string Id, string Name, string Description, string Argument);

    public Form1()
    {
        InitializeComponent();

        Icon = Icon.ExtractAssociatedIcon(
    Application.ExecutablePath);

        Text = "Nova Browser";

        WindowState = FormWindowState.Maximized;
        BackColor = Color.FromArgb(17, 22, 24);
        ForeColor = Color.FromArgb(231, 238, 236);
        Font = new Font("Segoe UI", 9.5f);

        toolbar = new TableLayoutPanel();
        toolbar.Dock = DockStyle.Top;
        toolbar.Height = 46;
        toolbar.Padding = new Padding(9, 5, 9, 5);
        toolbar.BackColor = Color.FromArgb(25, 32, 34);
        toolbar.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;

        toolbar.RowCount = 1;
        toolbar.ColumnCount = 8;

        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));

        backButton = CreateButton("←");
        forwardButton = CreateButton("→");
        refreshButton = CreateButton("↻");
        newTabButton = CreateButton("+");
        goButton = CreateButton("Go", true);
        bookmarkButton = CreateButton("☆");
        menuButton = CreateButton("⋮");

        addressBar = new TextBox();
        addressBar.Dock = DockStyle.Fill;
        addressBar.Margin = Padding.Empty;
        addressBar.Text = "https://www.bing.com";
        addressBar.BackColor = Color.FromArgb(34, 43, 45);
        addressBar.ForeColor = Color.FromArgb(231, 238, 236);
        addressBar.BorderStyle = BorderStyle.None;
        addressBar.Font = new Font("Segoe UI", 10f);

        Panel addressContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = addressBar.BackColor,
            Margin = new Padding(6, 2, 6, 2),
            Padding = new Padding(9, 6, 8, 5)
        };
        Label addressIcon = new Label
        {
            Text = "WEB",
            Dock = DockStyle.Left,
            Width = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(137, 207, 177),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold)
        };
        addressContainer.Controls.Add(addressBar);
        addressContainer.Controls.Add(addressIcon);

        toolbar.Controls.Add(backButton, 0, 0);
        toolbar.Controls.Add(forwardButton, 1, 0);
        toolbar.Controls.Add(refreshButton, 2, 0);
        toolbar.Controls.Add(addressContainer, 3, 0);
        toolbar.Controls.Add(goButton, 4, 0);
        toolbar.Controls.Add(bookmarkButton, 5, 0);
        toolbar.Controls.Add(newTabButton, 6, 0);
        toolbar.Controls.Add(menuButton, 7, 0);

        tabs = new TabControl();
        tabs.Dock = DockStyle.Fill;
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.Padding = new Point(18, 7);
        tabs.ItemSize = new Size(190, 36);
        tabs.BackColor = BackColor;
        tabs.ForeColor = ForeColor;

        tabs.DrawItem += Tabs_DrawItem;
        tabs.MouseDown += Tabs_MouseDown;
        tabs.SelectedIndexChanged += Tabs_SelectedIndexChanged;

        bookmarksBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10, 4, 10, 2),
            BackColor = Color.FromArgb(21, 27, 29)
        };
        bookmarksTitle = new Label
        {
            Text = "BOOKMARKS",
            Width = 82,
            Height = 25,
            Margin = new Padding(0, 0, 8, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(137, 207, 177),
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        bookmarksBar.Controls.Add(bookmarksTitle);
        LoadBookmarks();
        RenderBookmarks();
        LoadFlags();

        statusBar = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = true,
            BackColor = Color.FromArgb(25, 32, 34),
            ForeColor = Color.FromArgb(184, 198, 193),
            Padding = new Padding(8, 2, 8, 2),
            Font = new Font("Segoe UI", 8.5f)
        };
        pageStatusLabel = new ToolStripStatusLabel("Ready")
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        zoomOutButton = CreateStatusButton("−", "Zoom out");
        zoomLabel = new ToolStripStatusLabel("100%")
        {
            AutoSize = false,
            Width = 42,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(224, 233, 230)
        };
        zoomInButton = CreateStatusButton("+", "Zoom in");
        statusBar.Items.Add(pageStatusLabel);
        statusBar.Items.Add(new ToolStripSeparator());
        statusBar.Items.Add(new ToolStripStatusLabel("ZOOM")
        {
            ForeColor = Color.FromArgb(137, 207, 177),
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        });
        statusBar.Items.Add(zoomOutButton);
        statusBar.Items.Add(zoomLabel);
        statusBar.Items.Add(zoomInButton);

        browserMenu = new ContextMenuStrip();
        browserMenu.BackColor = Color.FromArgb(30, 38, 40);
        browserMenu.ForeColor = ForeColor;
        browserMenu.Font = Font;

browserMenu.Items.Add("New Tab", null,
    async (s,e) =>
{
    await CreateNewTab(NewTabUrl);
});

browserMenu.Items.Add("Nova Flags", null,
    (s,e) =>
{
    addressBar.Text = "nova://flags";
    NavigateCurrentTab();
});

browserMenu.Items.Add("Nova GPU", null,
    (s,e) =>
{
    addressBar.Text = "nova://gpu";
    NavigateCurrentTab();
});

browserMenu.Items.Add("Nova Settings", null,
    (s,e) =>
{
    addressBar.Text = "nova://settings";
    NavigateCurrentTab();
});

browserMenu.Items.Add("Nova Version", null,
    (s,e) =>
{
    addressBar.Text = "nova://version";
    NavigateCurrentTab();
});

browserMenu.Items.Add("-");

browserMenu.Items.Add("Copilot", null,
    (s,e) =>
{
    addressBar.Text = "https://copilot.microsoft.com";
    NavigateCurrentTab();
});

browserMenu.Items.Add("Exit", null,
    (s,e) =>
{
    Close();
});

        ToolTip toolbarToolTips = new ToolTip(components!);
        toolbarToolTips.SetToolTip(backButton, "Back");
        toolbarToolTips.SetToolTip(forwardButton, "Forward");
        toolbarToolTips.SetToolTip(refreshButton, "Reload");
        toolbarToolTips.SetToolTip(bookmarkButton, "Bookmark this page");
        toolbarToolTips.SetToolTip(newTabButton, "New tab");
        toolbarToolTips.SetToolTip(menuButton, "Browser menu");

        Controls.Add(tabs);
        Controls.Add(statusBar);
        Controls.Add(bookmarksBar);
        Controls.Add(toolbar);

        Load += Form1_Load;

        newTabButton.Click += async (s, e) =>
        {
            await CreateNewTab(NewTabUrl);
        };
        bookmarkButton.Click += (s, e) => SaveCurrentBookmark();
        zoomOutButton.Click += (s, e) => ChangeZoom(-0.1);
        zoomInButton.Click += (s, e) => ChangeZoom(0.1);

        goButton.Click += (s, e) =>
        {
            NavigateCurrentTab();
        };
        menuButton.Click += (s,e) =>
{
    browserMenu.Show(
        menuButton,
        0,
        menuButton.Height);
};

        addressBar.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                NavigateCurrentTab();
                e.SuppressKeyPress = true;
            }
        };

        backButton.Click += (s, e) =>
        {
            var browser = GetCurrentBrowser();

            if (browser?.CanGoBack == true)
                browser.GoBack();
        };

        forwardButton.Click += (s, e) =>
        {
            var browser = GetCurrentBrowser();

            if (browser?.CanGoForward == true)
                browser.GoForward();
        };

        refreshButton.Click += (s, e) =>
        {
            GetCurrentBrowser()?.Reload();
        };
    }

    private async void Form1_Load(object? sender, EventArgs e)
    {
        await CreateNewTab(NewTabUrl);
    }

    private ChromeButton CreateButton(string text, bool emphasized = false)
    {
        Color buttonColor = emphasized
            ? Color.FromArgb(137, 207, 177)
            : Color.FromArgb(38, 48, 50);

        return new ChromeButton(text)
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(2),
            BackColor = buttonColor,
            ForeColor = emphasized
                ? Color.FromArgb(20, 35, 30)
                : Color.FromArgb(137, 207, 177),
            Font = new Font(
                emphasized ? "Segoe UI" : "Segoe UI Symbol",
                emphasized ? 8.5f : 18f,
                emphasized ? FontStyle.Bold : FontStyle.Regular),
            AccessibleName = text,
            AccessibleRole = AccessibleRole.PushButton,
            Cursor = Cursors.Hand,
            TabStop = true,
            HoverBackColor = emphasized
                ? Color.FromArgb(158, 222, 193)
                : Color.FromArgb(55, 68, 70),
            PressedBackColor = emphasized
                ? Color.FromArgb(113, 187, 154)
                : Color.FromArgb(68, 82, 84)
        };
    }

    private sealed class ChromeButton : Control
    {
        private readonly Label caption;
        private bool isHovered;
        private bool isPressed;
        private bool isKeyboardPressed;

        public ChromeButton(string captionText)
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            caption = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                AutoSize = false,
                Margin = Padding.Empty,
                BackColor = Color.FromArgb(38, 48, 50),
                ForeColor = ForeColor,
                Font = new Font("Segoe UI Symbol", 15f),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false,
                TabStop = false,
                Text = captionText
            };
            caption.MouseEnter += (s, e) => SetHovered(true);
            caption.MouseLeave += (s, e) => SetHovered(false);
            caption.Click += (s, e) => OnClick(EventArgs.Empty);
            Controls.Add(caption);
            caption.BringToFront();
            UpdateAppearance();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using SolidBrush brush = new SolidBrush(CurrentBackground);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Focused)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
        }

        protected override void OnTextChanged(EventArgs e)
        {
            if (caption != null)
                caption.Text = Text;
            base.OnTextChanged(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            if (caption != null)
                caption.Font = Font;
            base.OnFontChanged(e);
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            if (caption != null)
                caption.ForeColor = ForeColor;
            base.OnForeColorChanged(e);
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            UpdateAppearance();
            base.OnBackColorChanged(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            UpdateAppearance();
            base.OnEnabledChanged(e);
        }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color HoverBackColor { get; set; }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color PressedBackColor { get; set; }

        protected override void OnMouseEnter(EventArgs e)
        {
            SetHovered(true);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            SetHovered(false);
            base.OnMouseLeave(e);
        }

        private void SetHovered(bool hovered)
        {
            isHovered = hovered;
            if (!hovered && !isKeyboardPressed)
                isPressed = false;
            UpdateAppearance();
        }

        private Color CurrentBackground => !Enabled
            ? Color.FromArgb(31, 38, 39)
            : isPressed
                ? PressedBackColor
                : isHovered
                    ? HoverBackColor
                    : BackColor;

        private void UpdateAppearance()
        {
            if (caption != null)
                caption.BackColor = CurrentBackground;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Enabled && e.Button == MouseButtons.Left)
            {
                Focus();
                isPressed = true;
                Capture = true;
            }
            UpdateAppearance();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool shouldClick = isPressed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location);
            isPressed = false;
            Capture = false;
            UpdateAppearance();
            base.OnMouseUp(e);
            if (shouldClick)
                OnClick(EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && e.KeyCode is Keys.Space or Keys.Enter)
            {
                isKeyboardPressed = true;
                isPressed = true;
                UpdateAppearance();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            bool shouldClick = isKeyboardPressed && e.KeyCode is Keys.Space or Keys.Enter;
            isKeyboardPressed = false;
            isPressed = false;
            UpdateAppearance();
            base.OnKeyUp(e);
            if (shouldClick)
                OnClick(EventArgs.Empty);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            isKeyboardPressed = false;
            isPressed = false;
            UpdateAppearance();
            base.OnLostFocus(e);
        }
    }

    private ToolStripButton CreateStatusButton(string text, string toolTip)
    {
        return new ToolStripButton(text)
        {
            AutoSize = false,
            Size = new Size(22, 20),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ForeColor = Color.FromArgb(137, 207, 177),
            ToolTipText = toolTip,
            Font = new Font("Segoe UI", 9f)
        };
    }

    private string BookmarksFilePath =>
        Path.Combine(Application.LocalUserAppDataPath, "bookmarks.json");

    private string FlagsFilePath =>
        Path.Combine(Application.LocalUserAppDataPath, "experimental-flags.json");

    private void LoadFlags()
    {
        try
        {
            if (!File.Exists(FlagsFilePath))
                return;

            List<string>? savedFlags =
                JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FlagsFilePath));
            if (savedFlags == null)
                return;

            foreach (string flagId in savedFlags)
            {
                if (BrowserFlagCatalog.Any(flag => flag.Id == flagId))
                    enabledFlags.Add(flagId);
            }
        }
        catch
        {
            enabledFlags.Clear();
        }
    }

    private void SaveFlags()
    {
        Directory.CreateDirectory(Application.LocalUserAppDataPath);
        File.WriteAllText(
            FlagsFilePath,
            JsonSerializer.Serialize(enabledFlags.OrderBy(flagId => flagId)));
    }

    private string BuildAdditionalBrowserArguments()
    {
        List<string> arguments = new();
        List<string> enabledFeatures = new();

        foreach (BrowserFlagDefinition flag in BrowserFlagCatalog)
        {
            if (!enabledFlags.Contains(flag.Id))
                continue;

            if (flag.Argument.StartsWith("--", StringComparison.Ordinal))
                arguments.Add(flag.Argument);
            else
                enabledFeatures.Add(flag.Argument);
        }

        if (enabledFeatures.Count > 0)
            arguments.Add($"--enable-features={string.Join(',', enabledFeatures)}");

        return string.Join(' ', arguments);
    }

    private Task<CoreWebView2Environment> GetBrowserEnvironmentAsync()
    {
        browserEnvironmentTask ??= CoreWebView2Environment.CreateAsync(
            null,
            null,
            new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = BuildAdditionalBrowserArguments()
            });

        return browserEnvironmentTask;
    }

    private void HandleFlagMessage(WebView2 browser, string messageJson)
    {
        if (!displayedUrls.TryGetValue(browser, out string? pageUrl) ||
            !string.Equals(pageUrl, "nova://flags", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            using JsonDocument message = JsonDocument.Parse(messageJson);
            JsonElement root = message.RootElement;
            if (!root.TryGetProperty("type", out JsonElement type))
            {
                return;
            }

            string? messageType = type.GetString();
            if (messageType == "restart")
            {
                SaveFlags();
                Application.Restart();
                return;
            }

            if (messageType != "toggle" ||
                !root.TryGetProperty("id", out JsonElement idElement) ||
                !root.TryGetProperty("enabled", out JsonElement enabledElement))
                return;

            string? flagId = idElement.GetString();
            if (flagId == null || !BrowserFlagCatalog.Any(flag => flag.Id == flagId))
                return;

            if (enabledElement.GetBoolean())
                enabledFlags.Add(flagId);
            else
                enabledFlags.Remove(flagId);

            SaveFlags();
            if (tabs.SelectedTab?.Controls.Contains(browser) == true)
                pageStatusLabel.Text = "Flag saved. Restart Nova Browser to apply.";
        }
        catch (Exception)
        {
            if (tabs.SelectedTab?.Controls.Contains(browser) == true)
                pageStatusLabel.Text = "Could not save browser flags.";
        }
    }

    private void LoadBookmarks()
    {
        try
        {
            if (!File.Exists(BookmarksFilePath))
            {
                bookmarks.Add(new BrowserBookmark("Bing", "https://www.bing.com"));
                bookmarks.Add(new BrowserBookmark("Copilot", "https://copilot.microsoft.com"));
                return;
            }

            List<BrowserBookmark>? savedBookmarks =
                JsonSerializer.Deserialize<List<BrowserBookmark>>(
                    File.ReadAllText(BookmarksFilePath));

            if (savedBookmarks != null)
            {
                bookmarks.AddRange(savedBookmarks.Where(bookmark =>
                    !string.IsNullOrWhiteSpace(bookmark.Title) &&
                    !string.IsNullOrWhiteSpace(bookmark.Url)));
            }
        }
        catch
        {
            bookmarks.Clear();
        }
    }

    private void SaveBookmarks()
    {
        Directory.CreateDirectory(Application.LocalUserAppDataPath);
        File.WriteAllText(
            BookmarksFilePath,
            JsonSerializer.Serialize(bookmarks));
    }

    private void RenderBookmarks()
    {
        while (bookmarksBar.Controls.Count > 1)
        {
            Control oldButton = bookmarksBar.Controls[1];
            bookmarksBar.Controls.RemoveAt(1);
            oldButton.Dispose();
        }

        foreach (BrowserBookmark bookmark in bookmarks)
        {
            int buttonWidth = Math.Clamp(
                TextRenderer.MeasureText(bookmark.Title, Font).Width + 22,
                76,
                180);
            Button bookmarkLink = new Button
            {
                Text = bookmark.Title,
                Width = buttonWidth,
                Height = 25,
                Margin = new Padding(2, 0, 4, 0),
                Padding = new Padding(7, 0, 5, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 43, 45),
                ForeColor = Color.FromArgb(213, 225, 220),
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            bookmarkLink.FlatAppearance.BorderSize = 0;
            bookmarkLink.FlatAppearance.MouseOverBackColor = Color.FromArgb(49, 63, 64);
            bookmarkLink.Click += (s, e) => NavigateToBookmark(bookmark.Url);
            bookmarksBar.Controls.Add(bookmarkLink);
        }
    }

    private void SaveCurrentBookmark()
    {
        WebView2? browser = GetCurrentBrowser();
        if (browser?.Source == null)
            return;

        string url = displayedUrls.TryGetValue(browser, out string? displayedUrl)
            ? displayedUrl
            : browser.Source.ToString();

        int existingIndex = bookmarks.FindIndex(bookmark =>
            string.Equals(bookmark.Url, url, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            bookmarks.RemoveAt(existingIndex);
            SaveBookmarks();
            RenderBookmarks();
            UpdateBookmarkButtonState();
            pageStatusLabel.Text = "Bookmark removed";
            return;
        }

        string title = tabs.SelectedTab?.Text ?? "New Tab";
        if (string.IsNullOrWhiteSpace(title) || title == "New Tab")
            title = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Host : url;

        bookmarks.Add(new BrowserBookmark(title, url));
        SaveBookmarks();
        RenderBookmarks();
        UpdateBookmarkButtonState();
        pageStatusLabel.Text = "Bookmark added";
    }

    private void NavigateToBookmark(string url)
    {
        addressBar.Text = url;
        NavigateCurrentTab();
    }

    private void ChangeZoom(double amount)
    {
        WebView2? browser = GetCurrentBrowser();
        if (browser?.CoreWebView2 == null)
            return;

        browser.ZoomFactor = Math.Clamp(browser.ZoomFactor + amount, 0.5, 3.0);
        UpdateStatusForCurrentTab();
    }

    private void UpdateStatusForCurrentTab()
    {
        WebView2? browser = GetCurrentBrowser();
        if (browser?.CoreWebView2 == null)
        {
            pageStatusLabel.Text = "Ready";
            zoomLabel.Text = "100%";
            return;
        }

        pageStatusLabel.Text = string.IsNullOrWhiteSpace(browser.CoreWebView2.StatusBarText)
            ? "Ready"
            : browser.CoreWebView2.StatusBarText;
        zoomLabel.Text = $"{browser.ZoomFactor:P0}";
    }

    private void UpdateBookmarkButtonState()
    {
        WebView2? browser = GetCurrentBrowser();
        string? url = browser == null
            ? null
            : displayedUrls.TryGetValue(browser, out string? displayedUrl)
                ? displayedUrl
                : browser.Source?.ToString();

        bool isBookmarked = bookmarks.Any(bookmark =>
            string.Equals(bookmark.Url, url, StringComparison.OrdinalIgnoreCase));
        bookmarkButton.Text = isBookmarked ? "★" : "☆";
        bookmarkButton.BackColor = Color.FromArgb(38, 48, 50);
        bookmarkButton.ForeColor = Color.FromArgb(137, 207, 177);
    }

    private async Task CreateNewTab(string url)
    {
        TabPage page = new TabPage("New Tab");
        page.BackColor = BackColor;

        WebView2 browser = new WebView2();
        browser.Dock = DockStyle.Fill;

        page.Controls.Add(browser);

        tabs.TabPages.Add(page);
        tabs.SelectedTab = page;

        CoreWebView2Environment environment = await GetBrowserEnvironmentAsync();
        await browser.EnsureCoreWebView2Async(environment);

        browser.CoreWebView2.Settings.UserAgent = CreateNovaUserAgent(
            browser.CoreWebView2.Settings.UserAgent);

        browser.CoreWebView2.WebMessageReceived += (s, e) =>
            HandleFlagMessage(browser, e.WebMessageAsJson);

        browser.CoreWebView2.StatusBarTextChanged += (s, e) =>
        {
            if (tabs.SelectedTab == page)
                BeginInvoke(UpdateStatusForCurrentTab);
        };
        browser.CoreWebView2.NavigationStarting += (s, e) =>
        {
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out Uri? requestedUri) &&
                requestedUri.Scheme.Equals("nova", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                string internalUrl = e.Uri;
                BeginInvoke(() =>
                {
                    if (tabs.SelectedTab == page)
                    {
                        addressBar.Text = internalUrl;
                        NavigateCurrentTab();
                    }
                });
                return;
            }

            if (displayedUrls.TryGetValue(browser, out string? displayedUrl) &&
                string.Equals(displayedUrl, NewTabUrl, StringComparison.OrdinalIgnoreCase) &&
                !e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
            {
                displayedUrls.Remove(browser);
            }

            if (tabs.SelectedTab == page)
                pageStatusLabel.Text = "Loading...";
        };
        browser.CoreWebView2.NavigationCompleted += (s, e) =>
        {
            if (tabs.SelectedTab == page)
            {
                if (browser.Source?.ToString().Equals("about:blank", StringComparison.OrdinalIgnoreCase) == true &&
                    string.Equals(browser.CoreWebView2.DocumentTitle, "New Tab", StringComparison.OrdinalIgnoreCase))
                {
                    displayedUrls[browser] = NewTabUrl;
                }

                if (displayedUrls.TryGetValue(browser, out string? shownUrl))
                    addressBar.Text = shownUrl;

                UpdateStatusForCurrentTab();
            }
        };

        browser.CoreWebView2.DocumentTitleChanged += (s, e) =>
        {
            string title = browser.CoreWebView2.DocumentTitle;
            if (displayedUrls.TryGetValue(browser, out string? displayedUrl) &&
                string.Equals(displayedUrl, "nova://flags", StringComparison.OrdinalIgnoreCase))
            {
                title = "Nova Flags";
            }

            if (string.IsNullOrWhiteSpace(title))
                title = "New Tab";

            if (title.Length > 28)
                title = title.Substring(0, 28);

            BeginInvoke(() =>
            {
                page.Text = title;
                tabs.Invalidate();

                if (tabs.SelectedTab == page)
                {
                    Text = $"{title} - Nova Browser";
                }
            });
        };

        browser.SourceChanged += (s, e) =>
        {
            if (tabs.SelectedTab == page)
            {
                BeginInvoke(() =>
                {
                    if (displayedUrls.TryGetValue(
        browser,
        out string? shown))
{
    addressBar.Text = shown;
}
else
{
    addressBar.Text =
        NormalizeUserUrl(
            browser.Source.ToString());
}
                    UpdateStatusForCurrentTab();
                    UpdateBookmarkButtonState();
                });
            }
        };

        if (string.Equals(url, NewTabUrl, StringComparison.OrdinalIgnoreCase))
            NavigateToNewTab(browser);
        else
            browser.Source = new Uri(url);
    }

    private void NavigateToNewTab(WebView2 browser)
    {
        displayedUrls[browser] = NewTabUrl;
        addressBar.Text = NewTabUrl;
        browser.CoreWebView2.NavigateToString(BuildNewTabHtml());
    }

    private static string CreateNovaUserAgent(string defaultUserAgent)
    {
        int edgeProductIndex = defaultUserAgent.LastIndexOf(
            " Edg/",
            StringComparison.OrdinalIgnoreCase);
        string compatibleUserAgent = edgeProductIndex >= 0
            ? defaultUserAgent[..edgeProductIndex]
            : defaultUserAgent;

        if (compatibleUserAgent.Contains(UserAgentBrand, StringComparison.OrdinalIgnoreCase))
            return compatibleUserAgent;

        return $"{compatibleUserAgent} {UserAgentBrand}";
    }

    private string BuildNewTabHtml()
    {
        string shortcutMarkup = string.Join(
            Environment.NewLine,
            bookmarks
                .Where(bookmark => Uri.TryCreate(bookmark.Url, UriKind.Absolute, out Uri? uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .Take(8)
                .Select(bookmark =>
                {
                    string safeTitle = System.Net.WebUtility.HtmlEncode(bookmark.Title);
                    string safeUrl = System.Net.WebUtility.HtmlEncode(bookmark.Url);
                    string initial = System.Net.WebUtility.HtmlEncode(
                        bookmark.Title[..1].ToUpperInvariant());

                    return $"<a class=\"shortcut\" href=\"{safeUrl}\"><span class=\"shortcut-icon\">{initial}</span><span>{safeTitle}</span></a>";
                }));

        if (string.IsNullOrWhiteSpace(shortcutMarkup))
        {
            shortcutMarkup = "<a class=\"shortcut\" href=\"https://www.bing.com\"><span class=\"shortcut-icon\">B</span><span>Bing</span></a>" +
                "<a class=\"shortcut\" href=\"https://copilot.microsoft.com\"><span class=\"shortcut-icon\">C</span><span>Copilot</span></a>";
        }

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>New Tab</title>
                <style>
                    :root { color-scheme: dark; font-family: "Segoe UI", sans-serif; background: #111618; color: #e7eeec; }
                    * { box-sizing: border-box; }
                    body { margin: 0; min-height: 100vh; background: radial-gradient(ellipse at 50% 38%, #243436 0, #171f21 38%, #111618 78%); }
                    main { min-height: 100vh; display: flex; flex-direction: column; padding: 34px 5vw 22px; }
                    header, footer { display: flex; align-items: center; justify-content: space-between; }
                    .brand { display: flex; align-items: center; gap: 12px; color: #89cfb1; font-size: 14px; font-weight: 700; }
                    .brand-mark { width: 34px; height: 34px; display: grid; place-items: center; border: 1px solid #89cfb1; border-radius: 10px; font-size: 17px; }
                    .clock { color: #afbfba; font-size: 13px; }
                    section { width: min(680px, 100%); margin: auto; transform: translateY(-24px); }
                    h1 { margin: 0 0 24px; text-align: center; font-size: 34px; font-weight: 500; letter-spacing: 0; }
                    form { display: flex; height: 56px; padding: 5px; border: 1px solid #3a4a4b; border-radius: 9px; background: #20292b; box-shadow: 0 12px 40px #080c0d66; }
                    input { flex: 1; min-width: 0; border: 0; outline: 0; padding: 0 14px; background: transparent; color: #f3f7f5; font: inherit; font-size: 16px; }
                    input::placeholder { color: #9ba9a5; }
                    button { width: 46px; border: 0; border-radius: 6px; background: #89cfb1; color: #14231e; font-size: 19px; cursor: pointer; }
                    .shortcuts { display: flex; justify-content: center; flex-wrap: wrap; gap: 12px; margin-top: 24px; }
                    .shortcut { display: flex; min-width: 128px; align-items: center; gap: 10px; padding: 10px 12px; border: 1px solid #303e40; border-radius: 8px; color: #dce6e2; text-decoration: none; background: #1a2224aa; font-size: 13px; }
                    .shortcut:hover { border-color: #89cfb1; background: #253234; }
                    .shortcut-icon { display: grid; width: 28px; height: 28px; place-items: center; border-radius: 7px; background: #89cfb1; color: #14231e; font-weight: 700; }
                    footer { color: #72827e; font-size: 11px; }
                    @media (max-width: 560px) { main { padding: 22px 18px; } h1 { font-size: 27px; } section { transform: none; } .shortcuts { justify-content: stretch; } .shortcut { flex: 1 1 42%; } }
                </style>
            </head>
            <body>
                <main>
                    <header><div class="brand"><span class="brand-mark">N</span><span>NOVA</span></div><div class="clock" id="clock"></div></header>
                    <section>
                        <h1>Where to next?</h1>
                        <form action="https://www.bing.com/search" method="get">
                            <input type="search" name="q" placeholder="Search the web" aria-label="Search the web" autofocus>
                            <button type="submit" aria-label="Search">&#8594;</button>
                        </form>
                        <div class="shortcuts">{{shortcutMarkup}}</div>
                    </section>
                    <footer><span>Nova Browser</span><span>nova://newtab</span></footer>
                </main>
                <script>
                    document.getElementById('clock').textContent = new Intl.DateTimeFormat(undefined, { weekday: 'long', month: 'long', day: 'numeric' }).format(new Date());
                </script>
            </body>
            </html>
            """;
    }

    private string BuildNovaFlagsPageHtml()
    {
        string flagCards = string.Join(
            Environment.NewLine,
            BrowserFlagCatalog.Select(flag =>
            {
                string safeId = System.Net.WebUtility.HtmlEncode(flag.Id);
                string safeName = System.Net.WebUtility.HtmlEncode(flag.Name);
                string safeDescription = System.Net.WebUtility.HtmlEncode(flag.Description);
                string checkedAttribute = enabledFlags.Contains(flag.Id) ? "checked" : string.Empty;

                return $"<label class=\"flag-card\" data-search=\"{safeName.ToLowerInvariant()} {safeDescription.ToLowerInvariant()}\"><span class=\"flag-copy\"><strong>{safeName}</strong><small>{safeDescription}</small></span><span class=\"flag-control\"><input type=\"checkbox\" data-flag=\"{safeId}\" {checkedAttribute}><span class=\"switch\"></span></span></label>";
            }));

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Nova Flags</title>
                <style>
                    :root { color-scheme: dark; font-family: "Segoe UI", sans-serif; background: #111618; color: #e7eeec; }
                    * { box-sizing: border-box; }
                    body { margin: 0; min-height: 100vh; background: linear-gradient(135deg, #111618, #182223 58%, #14201e); }
                    main { width: min(900px, 100%); margin: 0 auto; padding: 34px 28px 26px; }
                    header { display: flex; align-items: center; justify-content: space-between; padding-bottom: 18px; border-bottom: 1px solid #344243; }
                    .brand { color: #89cfb1; font-size: 13px; font-weight: 700; letter-spacing: 1px; }
                    .page-url { color: #8d9e98; font-size: 12px; }
                    .intro { padding: 35px 0 23px; }
                    .eyebrow { color: #89cfb1; font-size: 11px; font-weight: 700; text-transform: uppercase; }
                    h1 { margin: 8px 0; font-size: 30px; font-weight: 500; }
                    .intro p { margin: 0; max-width: 680px; color: #a9b8b3; font-size: 14px; line-height: 1.55; }
                    .notice { margin: 18px 0; padding: 12px 14px; border-left: 3px solid #89cfb1; background: #1b2828; color: #b7c5c0; font-size: 12px; line-height: 1.5; }
                    .tools { display: flex; gap: 10px; margin: 20px 0 12px; }
                    .search { flex: 1; min-width: 0; height: 40px; padding: 0 12px; border: 1px solid #344243; border-radius: 6px; background: #1b2426; color: #e7eeec; font: inherit; }
                    button { border: 1px solid #435452; border-radius: 6px; padding: 0 14px; background: #273334; color: #dce7e2; font: inherit; cursor: pointer; }
                    button:hover { border-color: #89cfb1; }
                    .restart { border-color: #89cfb1; background: #89cfb1; color: #14231e; font-weight: 600; }
                    .restart:hover { background: #9cddbf; }
                    .flag-list { display: grid; gap: 8px; }
                    .flag-card { display: flex; align-items: center; justify-content: space-between; gap: 20px; min-height: 76px; padding: 13px 15px; border: 1px solid #303e40; border-radius: 7px; background: #192224; cursor: pointer; }
                    .flag-card:hover { border-color: #536764; }
                    .flag-copy { display: grid; gap: 5px; }
                    .flag-copy strong { color: #e5eeea; font-size: 14px; font-weight: 600; }
                    .flag-copy small { color: #98a9a3; font-size: 12px; line-height: 1.45; }
                    .flag-control { position: relative; flex: 0 0 42px; height: 24px; }
                    .flag-control input { position: absolute; width: 1px; height: 1px; opacity: 0; }
                    .switch { position: absolute; inset: 0; border: 1px solid #586864; border-radius: 14px; background: #293435; transition: background .15s ease; }
                    .switch::after { content: ""; position: absolute; top: 3px; left: 3px; width: 16px; height: 16px; border-radius: 50%; background: #a2b0ab; transition: transform .15s ease, background .15s ease; }
                    .flag-control input:checked + .switch { border-color: #89cfb1; background: #416453; }
                    .flag-control input:checked + .switch::after { transform: translateX(18px); background: #b8f0d3; }
                    .flag-control input:focus-visible + .switch { outline: 2px solid #b8f0d3; outline-offset: 2px; }
                    .footer { display: flex; justify-content: space-between; gap: 12px; margin-top: 17px; color: #81918c; font-size: 11px; }
                    #save-status { color: #89cfb1; }
                    @media (max-width: 560px) { main { padding: 22px 16px; } h1 { font-size: 25px; } .tools { flex-wrap: wrap; } .search { flex-basis: 100%; } button { min-height: 38px; } .flag-card { gap: 12px; } }
                </style>
            </head>
            <body>
                <main>
                    <header><span class="brand">NOVA BROWSER</span><span class="page-url">nova://flags</span></header>
                    <section class="intro">
                        <div class="eyebrow">Experimental</div>
                        <h1>Nova Flags</h1>
                        <p>Try experimental browser features managed by Nova. Changes are saved for your next browser launch.</p>
                        <div class="notice">Experimental options can affect stability, performance, or compatibility. This list contains the switches Nova supports; it is not the complete Edge flags catalog. Restart Nova to apply changes.</div>
                    </section>
                    <div class="tools">
                        <input class="search" id="search" type="search" placeholder="Search Nova flags" aria-label="Search Nova flags">
                        <button id="reset" type="button">Reset</button>
                        <button class="restart" id="restart" type="button">Restart Nova</button>
                    </div>
                    <section class="flag-list" aria-label="Available experimental flags">{{flagCards}}</section>
                    <footer class="footer"><span>WebView2 runtime: {{System.Net.WebUtility.HtmlEncode(GetRuntimeVersion())}}</span><span id="save-status" aria-live="polite">Ready</span></footer>
                </main>
                <script>
                    const status = document.getElementById('save-status');
                    document.querySelectorAll('[data-flag]').forEach(toggle => {
                        toggle.addEventListener('change', () => {
                            window.chrome.webview.postMessage({ type: 'toggle', id: toggle.dataset.flag, enabled: toggle.checked });
                            status.textContent = 'Saved. Restart Nova to apply.';
                        });
                    });
                    document.getElementById('search').addEventListener('input', event => {
                        const query = event.target.value.trim().toLowerCase();
                        document.querySelectorAll('.flag-card').forEach(card => {
                            card.hidden = !card.dataset.search.includes(query);
                        });
                    });
                    document.getElementById('reset').addEventListener('click', () => {
                        document.querySelectorAll('[data-flag]:checked').forEach(toggle => {
                            toggle.checked = false;
                            toggle.dispatchEvent(new Event('change'));
                        });
                    });
                    document.getElementById('restart').addEventListener('click', () => {
                        window.chrome.webview.postMessage({ type: 'restart' });
                    });
                </script>
            </body>
            </html>
            """;
    }

    private string GetRuntimeVersion()
    {
        WebView2? browser = GetCurrentBrowser();
        return browser?.CoreWebView2?.Environment.BrowserVersionString ?? "Loading";
    }

    private string BuildNovaInternalPageHtml(WebView2 browser, string route)
    {
        string runtimeVersion = System.Net.WebUtility.HtmlEncode(
            browser.CoreWebView2.Environment.BrowserVersionString);
        string productVersion = System.Net.WebUtility.HtmlEncode(Application.ProductVersion);
        string pageTitle;
        string pageHeading;
        string pageDescription;
        string detailsMarkup;

        switch (route)
        {
            case "settings":
                pageTitle = "Nova Settings";
                pageHeading = "Browser settings";
                pageDescription = "Your current Nova Browser setup and available controls.";
                detailsMarkup =
                    $"<article><span>Startup</span><strong>Nova New Tab</strong><small>New windows open nova://newtab.</small></article>" +
                    $"<article><span>Search</span><strong>Bing</strong><small>Address-bar searches use Bing.</small></article>" +
                    $"<article><span>Bookmarks</span><strong>{bookmarks.Count} saved</strong><small>Bookmarks are stored in this Windows profile.</small></article>" +
                    $"<article><span>Experiments</span><strong><a class=\"page-link\" href=\"nova://flags\">Manage Nova Flags</a></strong><small>{enabledFlags.Count} switches selected; restart required to apply.</small></article>" +
                    "<article><span>About</span><strong><a class=\"page-link\" href=\"nova://version\">Nova Browser version</a></strong><small>View application and runtime details.</small></article>";
                break;
            case "version":
                pageTitle = "Nova Version";
                pageHeading = "About Nova Browser";
                pageDescription = "Application and embedded browser runtime information.";
                detailsMarkup =
                    $"<article><span>Application</span><strong>Nova Browser</strong><small>Version {productVersion}</small></article>" +
                    $"<article><span>Browser runtime</span><strong>WebView2</strong><small>Version {runtimeVersion}</small></article>" +
                    $"<article><span>Browser identity</span><strong>{UserAgentBrand}</strong><small>Appended to the default user-agent.</small></article>" +
                    "<article><span>Settings</span><strong><a class=\"page-link\" href=\"nova://settings\">Nova Settings</a></strong><small>View the current browser configuration.</small></article>";
                break;
            default:
                pageTitle = "Nova GPU";
                pageHeading = "Graphics diagnostics";
                pageDescription = "Graphics are managed by the embedded WebView2 runtime and your system drivers.";
                detailsMarkup =
                    $"<article><span>Graphics provider</span><strong>System managed</strong><small>Hardware acceleration follows Windows and runtime settings.</small></article>" +
                    $"<article><span>WebView2 runtime</span><strong>{runtimeVersion}</strong><small>Engine diagnostics are provided by the installed runtime.</small></article>";
                break;
        }

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{pageTitle}}</title>
                <style>
                    :root { color-scheme: dark; font-family: "Segoe UI", sans-serif; background: #111618; color: #e7eeec; }
                    * { box-sizing: border-box; }
                    body { margin: 0; min-height: 100vh; background: linear-gradient(135deg, #111618, #1c2829 60%, #14201e); }
                    main { width: min(920px, 100%); margin: 0 auto; padding: 38px 28px; }
                    header { display: flex; align-items: center; justify-content: space-between; padding-bottom: 20px; border-bottom: 1px solid #344243; }
                    .brand { color: #89cfb1; font-size: 13px; font-weight: 700; text-decoration: none; letter-spacing: 1px; }
                    .location { color: #899994; font-size: 12px; }
                    section { padding: 48px 0 30px; }
                    .eyebrow { color: #89cfb1; font-size: 11px; font-weight: 700; letter-spacing: 1px; text-transform: uppercase; }
                    h1 { margin: 10px 0; font-size: 32px; font-weight: 500; }
                    .description { max-width: 620px; color: #aab9b4; font-size: 15px; line-height: 1.6; }
                    .details { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 14px; margin-top: 25px; }
                    article { min-height: 132px; padding: 20px; border: 1px solid #354344; border-radius: 8px; background: #192224; }
                    article span, article small { display: block; color: #9baaa5; font-size: 12px; }
                    article strong { display: block; margin: 12px 0 6px; color: #e7eeec; font-size: 18px; font-weight: 500; }
                    .page-link { color: #89cfb1; font-size: 15px; text-decoration: none; }
                    .page-link:hover { text-decoration: underline; }
                    footer { margin-top: 46px; color: #758580; font-size: 11px; }
                    @media (max-width: 560px) { main { padding: 24px 18px; } section { padding-top: 34px; } h1 { font-size: 27px; } }
                </style>
            </head>
            <body>
                <main>
                    <header><a class="brand" href="nova://newtab">NOVA BROWSER</a><span class="location">nova://{{route}}</span></header>
                    <section>
                        <div class="eyebrow">Nova Browser</div>
                        <h1>{{pageHeading}}</h1>
                        <p class="description">{{pageDescription}}</p>
                        <div class="details">{{detailsMarkup}}</div>
                    </section>
                    <footer>Powered by WebView2</footer>
                </main>
            </body>
            </html>
            """;
    }

    private void Tabs_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var browser = GetCurrentBrowser();

        if (browser != null)
        {
            if (displayedUrls.TryGetValue(
        browser,
        out string? shown))
{
    addressBar.Text = shown;
}
else
{
    addressBar.Text =
        NormalizeUserUrl(
            browser.Source?.ToString() ?? "");
}
        }

    UpdateStatusForCurrentTab();
    UpdateBookmarkButtonState();
    }

    private WebView2? GetCurrentBrowser()
    {
        if (tabs.SelectedTab == null)
            return null;

        foreach (Control control in tabs.SelectedTab.Controls)
        {
            if (control is WebView2 browser)
                return browser;
        }

        return null;
    }
    private string NormalizeUserUrl(string url)
{
    if (url.StartsWith("edge://"))
        return "nova://" + url.Substring(7);

    if (url.StartsWith("chrome://"))
        return "nova://" + url.Substring(9);

    if (url.StartsWith("brave://"))
        return "nova://" + url.Substring(8);

    if (url.StartsWith("opera://"))
        return "nova://" + url.Substring(8);

    if (url.StartsWith("vivaldi://"))
        return "nova://" + url.Substring(10);

    if (url.StartsWith("about:")
        && !url.Equals("about:blank",
        StringComparison.OrdinalIgnoreCase))
    {
        return "nova://" + url.Substring(6);
    }

    return url;
}

private string ResolveNovaUrl(string url)
{
    if (!url.StartsWith("nova://"))
        return url;

    return "edge://" + url.Substring(7);
}
    private void NavigateCurrentTab()
    {
        var browser = GetCurrentBrowser();

        if (browser == null)
            return;

        string url = addressBar.Text.Trim();
        url = NormalizeUserUrl(url);

        if (string.Equals(url, NewTabUrl, StringComparison.OrdinalIgnoreCase))
        {
            NavigateToNewTab(browser);
            return;
        }

        if (url.StartsWith("nova://"))
{
    string novaPage = url.Substring("nova://".Length).Trim('/');
    if (novaPage.Equals("flags", StringComparison.OrdinalIgnoreCase) ||
        novaPage.Equals("gpu", StringComparison.OrdinalIgnoreCase) ||
        novaPage.Equals("settings", StringComparison.OrdinalIgnoreCase) ||
        novaPage.Equals("version", StringComparison.OrdinalIgnoreCase))
    {
        string canonicalUrl = $"nova://{novaPage.ToLowerInvariant()}";
        displayedUrls[browser] = canonicalUrl;
        addressBar.Text = canonicalUrl;

        if (novaPage.Equals("flags", StringComparison.OrdinalIgnoreCase))
        {
            browser.CoreWebView2.NavigateToString(BuildNovaFlagsPageHtml());
            return;
        }

        browser.CoreWebView2.NavigateToString(
            BuildNovaInternalPageHtml(browser, novaPage.ToLowerInvariant()));
        return;
    }

    string realUrl =
        ResolveNovaUrl(url);

    displayedUrls[browser] = url;

    browser.CoreWebView2.Navigate(realUrl);

    return;
}

        displayedUrls.Remove(browser);

        try
        {

            if (url.Contains("://"))
            {
                browser.Source = new Uri(url);
            }
            else
            {
                browser.Source =
                    new Uri("https://" + url);
            }
        }
        catch
        {
            browser.Source = new Uri(
                $"https://www.bing.com/search?q={Uri.EscapeDataString(url)}"
            );
        }
    }

    private void Tabs_DrawItem(
        object? sender,
        DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;

        TabPage page = tabs.TabPages[e.Index];

        Rectangle tabRect =
            tabs.GetTabRect(e.Index);

        Color background =
            e.Index == tabs.SelectedIndex
                ? Color.FromArgb(38, 49, 51)
                : Color.FromArgb(25, 32, 34);

        using SolidBrush brush =
            new SolidBrush(background);

        e.Graphics.FillRectangle(
            brush,
            tabRect);

        if (e.Index == tabs.SelectedIndex)
        {
            using SolidBrush accentBrush =
                new SolidBrush(Color.FromArgb(137, 207, 177));
            e.Graphics.FillRectangle(
                accentBrush,
                new Rectangle(tabRect.X + 10, tabRect.Bottom - 2, tabRect.Width - 20, 2));
        }

        TextRenderer.DrawText(
            e.Graphics,
            page.Text,
            Font,
            new Rectangle(
                tabRect.X + 14,
                tabRect.Y + 2,
                tabRect.Width - 50,
                tabRect.Height
            ),
            e.Index == tabs.SelectedIndex
                ? Color.FromArgb(238, 245, 242)
                : Color.FromArgb(163, 177, 173),
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
        );

        TextRenderer.DrawText(
            e.Graphics,
            "×",
            Font,
            new Rectangle(
                tabRect.Right - 32,
                tabRect.Y + 3,
                24,
                tabRect.Height - 6
            ),
            Color.FromArgb(153, 171, 166),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
        );
    }

    private void Tabs_MouseDown(
        object? sender,
        MouseEventArgs e)
    {
        for (int i = 0; i < tabs.TabPages.Count; i++)
        {
            Rectangle tabRect =
                tabs.GetTabRect(i);

            Rectangle closeRect =
                new Rectangle(
                    tabRect.Right - 32,
                    tabRect.Top + 3,
                    24,
                    tabRect.Height - 6);

            if (!closeRect.Contains(e.Location))
                continue;

            if (tabs.TabPages.Count > 1)
            {
                TabPage page = tabs.TabPages[i];
                tabs.TabPages.Remove(page);
                page.Dispose();
            }

            break;
        }
    }
}