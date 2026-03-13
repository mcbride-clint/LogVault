using Microsoft.JSInterop;
using MudBlazor;

namespace LogVault.Client.Services;

public class ThemeService
{
    public bool IsDarkMode { get; private set; } = false;

    private IJSRuntime? _js;
    private bool _initialized;

    public async Task InitializeAsync(IJSRuntime js)
    {
        if (_initialized) return;
        _initialized = true;
        _js = js;
        IsDarkMode = await js.InvokeAsync<bool>("themeInterop.getPreferredTheme");
        await js.InvokeVoidAsync("themeInterop.watchSystemTheme", DotNetObjectReference.Create(this));
        OnChange?.Invoke();
    }

    [JSInvokable]
    public void OnSystemThemeChanged(bool isDark)
    {
        IsDarkMode = isDark;
        OnChange?.Invoke();
    }

    public MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#5C6BC0",
            PrimaryDarken = "#3949AB",
            PrimaryLighten = "#7986CB",
            Secondary = "#26A69A",
            SecondaryDarken = "#00897B",
            Tertiary = "#EF5350",
            AppbarBackground = "#1A237E",
            AppbarText = Colors.Shades.White,
            DrawerBackground = "#1A237E",
            DrawerText = "rgba(255,255,255,0.75)",
            DrawerIcon = "rgba(255,255,255,0.75)",
            Background = "#F4F5F7",
            Surface = Colors.Shades.White,
            TextPrimary = "#1A1A2E",
            TextSecondary = "rgba(0,0,0,0.54)",
            ActionDefault = "#74718E",
            ActionDisabled = "rgba(0,0,0,0.26)",
            ActionDisabledBackground = "rgba(0,0,0,0.12)",
            Divider = "rgba(0,0,0,0.12)",
            TableLines = "rgba(0,0,0,0.08)",
            HoverOpacity = 0.04,
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7986CB",
            PrimaryDarken = "#5C6BC0",
            PrimaryLighten = "#9FA8DA",
            Secondary = "#4DB6AC",
            SecondaryDarken = "#26A69A",
            Tertiary = "#EF9A9A",
            AppbarBackground = "#0D0D1A",
            AppbarText = Colors.Shades.White,
            DrawerBackground = "#0D0D1A",
            DrawerText = "rgba(255,255,255,0.70)",
            DrawerIcon = "rgba(255,255,255,0.70)",
            Background = "#121212",
            Surface = "#1E1E2E",
            TextPrimary = "rgba(255,255,255,0.87)",
            TextSecondary = "rgba(255,255,255,0.60)",
            ActionDefault = "rgba(255,255,255,0.54)",
            ActionDisabled = "rgba(255,255,255,0.26)",
            ActionDisabledBackground = "rgba(255,255,255,0.12)",
            Divider = "rgba(255,255,255,0.12)",
            TableLines = "rgba(255,255,255,0.07)",
            HoverOpacity = 0.08,
            OverlayDark = "rgba(33,33,33,0.75)",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Roboto", "Helvetica", "Arial", "sans-serif"],
                FontSize = "0.875rem",
                FontWeight = "400",
                LineHeight = "1.43",
                LetterSpacing = "0.01071em"
            },
            H5 = new H5Typography { FontSize = "1.25rem", FontWeight = "600" },
            H6 = new H6Typography { FontSize = "1rem", FontWeight = "600" },
            Subtitle1 = new Subtitle1Typography { FontSize = "0.9rem", FontWeight = "500" },
            Body2 = new Body2Typography { FontSize = "0.8125rem" },
            Button = new ButtonTypography { FontSize = "0.8125rem", FontWeight = "600", TextTransform = "none" },
            Caption = new CaptionTypography { FontSize = "0.75rem" }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "240px",
            AppbarHeight = "64px"
        }
    };

    public event Action? OnChange;

    public async Task ToggleDarkModeAsync()
    {
        IsDarkMode = !IsDarkMode;
        if (_js is not null)
            await _js.InvokeVoidAsync("themeInterop.saveTheme", IsDarkMode);
        OnChange?.Invoke();
    }
}
