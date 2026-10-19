using _01_AdminApp.Services;
using ProductionLine.Shared;
namespace _01_AdminApp;
internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var api = new ProductionLineApiClient();
        Application.Run(new AdminLoginForm(new ApiAdminAuthService(api),
            new ApiDashboardService(api), new ApiLineDetailService(api), new ApiUserService(api), api));
    }
}
