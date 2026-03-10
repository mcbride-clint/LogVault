namespace LogVault.Client.Services;

public class AuthStateService
{
    private UserInfo? _user;
    public UserInfo? CurrentUser => _user;
    public bool IsAuthenticated => _user != null;
    public bool IsAdmin => _user?.Roles.Contains("Admin") == true;

    public event Action? OnChange;

    public async Task InitializeAsync(ILogVaultApiClient apiClient)
    {
        _user = await apiClient.GetMeAsync();
        NotifyStateChanged();
    }

    public void SetUser(UserInfo? user)
    {
        _user = user;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
