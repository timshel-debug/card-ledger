namespace CardService.Api.Tests;

public abstract class IntegrationTestBase : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        
        // Clear database and FX provider before each test
        Factory.ClearDatabase();
        Factory.FakeTreasuryProvider.Clear();
        Factory.FakeTreasuryProvider.ShouldThrow = false;
    }

    public void Dispose()
    {
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
