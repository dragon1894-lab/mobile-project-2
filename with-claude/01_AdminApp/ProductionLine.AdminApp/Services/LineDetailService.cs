using ProductionLine.Shared;
namespace _01_AdminApp.Services;
public interface ILineDetailService { Task<List<InspectionDto>> GetInspectionsAsync(int lineId); }
public sealed class ApiLineDetailService : ILineDetailService
{
    private readonly ProductionLineApiClient _api;
    public ApiLineDetailService(ProductionLineApiClient api) => _api = api;
    public Task<List<InspectionDto>> GetInspectionsAsync(int lineId) =>
        ServerCall.Run(() => _api.GetInspectionsAsync(lineId));
}
public sealed class MockLineDetailService : ILineDetailService
{
    public Task<List<InspectionDto>> GetInspectionsAsync(int lineId) => Task.FromResult(new List<InspectionDto>());
}
