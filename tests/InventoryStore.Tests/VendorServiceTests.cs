using InventoryStore.Application.DTOs;
using InventoryStore.Application.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Tests.Fakes;

namespace InventoryStore.Tests;

// The service reads the wall clock directly, so these tests leave CreatedAt and UpdatedAt unasserted.
public class VendorServiceTests
{
    private readonly FakeVendorRepository _repository = new();
    private readonly VendorService _service;

    public VendorServiceTests()
    {
        _service = new VendorService(_repository);
    }

    private static CreateVendorDto NewVendor(string name, string? phone = null, string? email = null) =>
        new(name, phone, email, Address: null, Notes: null);

    [Fact]
    public async Task CreatingAVendor_TrimsEveryTextFieldItStores()
    {
        var created = await _service.CreateAsync(new CreateVendorDto(
            "  Acme Repair  ", " 555-0100 ", " service@acme.test ", " 1 Main St ", " Fast turnaround "));

        Assert.Equal("Acme Repair", created.Name);
        Assert.Equal("555-0100", created.Phone);
        Assert.Equal("service@acme.test", created.Email);
        Assert.Equal("1 Main St", created.Address);
        Assert.Equal("Fast turnaround", created.Notes);
    }

    [Fact]
    public async Task CreatingAVendorWithoutOptionalFields_LeavesThemEmpty()
    {
        var created = await _service.CreateAsync(NewVendor("Acme Repair"));

        Assert.Null(created.Phone);
        Assert.Null(created.Email);
    }

    [Fact]
    public async Task QuickCreate_MakesANewVendor_WhenNoneHasThatName()
    {
        var created = await _service.QuickCreateAsync("Acme Repair");

        Assert.Equal("Acme Repair", created.Name);
        Assert.Single(_repository.Vendors);
    }

    [Fact]
    public async Task QuickCreate_TrimsTheNameBeforeStoringIt()
    {
        var created = await _service.QuickCreateAsync("   Acme Repair   ");

        Assert.Equal("Acme Repair", created.Name);
    }

    [Theory]
    [InlineData("Acme")]
    [InlineData("acme")]
    [InlineData("ACME")]
    [InlineData("  Acme  ")]
    public async Task QuickCreate_ReusesTheExistingVendor_WhenTheNameDiffersOnlyByCaseOrSpacing(string typed)
    {
        await _repository.CreateAsync(new Vendor { Name = "Acme" });
        var createCallsBefore = _repository.CreateCallCount;

        var result = await _service.QuickCreateAsync(typed);

        Assert.Equal("Acme", result.Name);
        Assert.Equal(createCallsBefore, _repository.CreateCallCount);
        Assert.Single(_repository.Vendors);
    }

    [Fact]
    public async Task UpdatingAVendor_TrimsTheNewValues_AndClearsFieldsSetToNull()
    {
        var existing = await _repository.CreateAsync(new Vendor { Name = "Acme", Phone = "555-0100" });

        await _service.UpdateAsync(existing.Id, new UpdateVendorDto("  Acme Repair ", Phone: null, " a@acme.test ", null, null));

        var stored = Assert.Single(_repository.Vendors);
        Assert.Equal("Acme Repair", stored.Name);
        Assert.Null(stored.Phone);
        Assert.Equal("a@acme.test", stored.Email);
    }

    [Fact]
    public async Task UpdatingAVendorThatDoesNotExist_IsRefused()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateAsync(999, new UpdateVendorDto("Anyone", null, null, null, null)));
    }

    [Fact]
    public async Task SearchingVendors_ReturnsOnlyThoseMatchingTheQuery()
    {
        await _repository.CreateAsync(new Vendor { Name = "Acme Repair" });
        await _repository.CreateAsync(new Vendor { Name = "Zenith Calibration" });

        var found = await _service.SearchAsync("acme");

        Assert.Equal("Acme Repair", Assert.Single(found).Name);
    }
}
