using System.Collections.Generic;
using System.Linq;
using HermesDesktop.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Graphics;

namespace HermesDesktop;

public sealed partial class MainWindow : Window
{
    private static readonly IReadOnlyDictionary<string, System.Type> PageMap = new Dictionary<string, System.Type>
    {
        ["dashboard"] = typeof(DashboardPage),
        ["chat"] = typeof(ChatPage),
        ["agent"] = typeof(AgentPage),
        ["developer"] = typeof(DeveloperPage),
        ["skills"] = typeof(SkillsPage),
        ["memory"] = typeof(MemoryPage),
        ["settings"] = typeof(SettingsPage),
    };

    private static readonly ResourceLoader ResourceLoader = new();
    internal static MainWindow? ActiveShell { get; private set; }

    // Track presenter kind so AppWindow.Changed can detect the
    // overlapped→maximized transition that triggered the v2.4.0 stretch bug.
    private AppWindowPresenterKind _lastPresenterKind;
    private bool _isUpdatingNavigationSelection;

    public MainWindow()
    {
        InitializeComponent();
        ActiveShell = this;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        Title = ResourceLoader.GetString("WindowTitle");
        AppTitleBar.Title = Title;
        AppWindow.Resize(new SizeInt32(1480, 960));
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Hook AppWindow.Changed to fix the drag-maximize layout regression.
        // When the user drags the title bar to the top edge of the screen,
        // Windows raises a presenter change to OverlappedPresenter+Maximized,
        // but the root XAML tree retains its previous size for one frame and
        // renders stretched/clipped until something forces a measure pass.
        // Force an InvalidateMeasure on the root element when presenter
        // kind changes — cheap (single layout pass) and idempotent.
        _lastPresenterKind = AppWindow.Presenter?.Kind ?? AppWindowPresenterKind.Default;
        AppWindow.Changed += OnAppWindowChanged;

        ShellNavigation.SelectedItem = ChatNavItem;
        NavigateToTag("chat");
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        // Only react to presenter changes — Position/Size events fire constantly
        // during drag and would thrash the layout tree for no benefit.
        if (!args.DidPresenterChange) return;

        var newKind = sender.Presenter?.Kind ?? AppWindowPresenterKind.Default;
        if (newKind == _lastPresenterKind) return;
        _lastPresenterKind = newKind;

        // Walk to the root visual element. Window.Content is the XAML root we
        // own; UpdateLayout() forces an immediate measure+arrange pass so the
        // stretched-frame artifact never reaches the compositor.
        if (Content is FrameworkElement root)
        {
            root.InvalidateMeasure();
            root.InvalidateArrange();
            root.UpdateLayout();
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isUpdatingNavigationSelection)
            return;

        if (args.SelectedItemContainer?.Tag is string tag)
        {
            NavigateToTag(tag);
        }
    }

    internal void NavigateToAgentRuntime() => NavigateToTag("agent", AgentPage.RuntimeTabParameter);

    internal void NavigateToDeveloperNpcInspector() => NavigateToTag("developer");

    private void NavigateToTag(string tag, object? parameter = null)
    {
        if (!PageMap.TryGetValue(tag, out System.Type? pageType))
            return;

        var selectedItem = ShellNavigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, tag, System.StringComparison.OrdinalIgnoreCase));
        if (selectedItem is not null && !ReferenceEquals(ShellNavigation.SelectedItem, selectedItem))
        {
            _isUpdatingNavigationSelection = true;
            try
            {
                ShellNavigation.SelectedItem = selectedItem;
            }
            finally
            {
                _isUpdatingNavigationSelection = false;
            }
        }

        if (ContentFrame.CurrentSourcePageType != pageType || parameter is not null)
        {
            ContentFrame.Navigate(pageType, parameter);
        }
    }
}
