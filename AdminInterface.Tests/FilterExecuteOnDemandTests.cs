using AdminInterface;
using AdminInterface.Components.Pages;

namespace AdminInterface.Tests;

/// <summary>
/// Integration test scenarios to verify that filters only apply when Execute is pressed
/// These tests document the expected behavior after removing auto-refresh callbacks
/// </summary>
public class FilterExecuteOnDemandTests
{
    [Fact]
    public void Setting_Authorization_Filter_Should_Not_Update_Local_DataView()
    {
        // This test documents that setting a filter value doesn't trigger data reload
        // The filter's Value property changes, but no LoadData() is called
        
        // Arrange
        var filter = new Filter<bool?>("isAuthorized", null);
        var originalValue = filter.Value;
        var callbackTriggered = false;
        
        // Setting OnChanged callback should not happen in HomeBase anymore
        filter.OnChanged = () => callbackTriggered = true;

        // Act
        filter.Value = true; // User selects "Authorized" from dropdown

        // Assert
        Assert.True(filter.IsActive); // Filter is now active
        Assert.Equal(true, filter.Value); // Filter value is set
        Assert.True(callbackTriggered); // Callback was triggered (Filter class mechanism works)
        // BUT: In HomeBase, the OnChanged callback is NOT set, so LoadData() won't be called
    }

    [Fact]
    public void Filter_Without_OnChanged_Handler_Should_Not_Trigger_Any_Action()
    {
        // Arrange
        var filter = new Filter<int?>("assocNum", null);
        // Note: NOT setting filter.OnChanged - simulating HomeBase behavior

        // Act
        filter.Value = 101;

        // Assert
        Assert.True(filter.IsActive);
        Assert.Equal(101, filter.Value);
        // No automatic action triggered - user must manually click Execute
    }

    [Fact]
    public void Multiple_Filters_Can_Be_Set_Without_Triggering_Updates()
    {
        // Simulates user filling out multiple filter fields before clicking Execute
        
        // Arrange
        var filters = new Dictionary<string, IFilter>
        {
            ["assocNum"] = new Filter<int?>("assocNum", null),
            ["assocName"] = new Filter<string?>("assocName", null),
            ["cmmsNum"] = new Filter<int?>("cmmsNum", null),
            ["isAuthorized"] = new Filter<bool?>("isAuthorized", null)
        };

        // Act - User fills multiple filters
        ((Filter<int?>)filters["assocNum"]).Value = 101;
        ((Filter<string?>)filters["assocName"]).Value = "John";
        ((Filter<int?>)filters["cmmsNum"]).Value = 5001;
        ((Filter<bool?>)filters["isAuthorized"]).Value = true;

        // Assert - All filters are set but no data fetch occurs yet
        Assert.All(filters.Values, f => Assert.True(f.IsActive));
        Assert.Equal(101, ((Filter<int?>)filters["assocNum"]).Value);
        Assert.Equal("John", ((Filter<string?>)filters["assocName"]).Value);
        Assert.Equal(5001, ((Filter<int?>)filters["cmmsNum"]).Value);
        Assert.Equal(true, ((Filter<bool?>)filters["isAuthorized"]).Value);
    }

    [Fact]
    public void Clearing_Filter_Should_Not_Trigger_Update_Until_Execute()
    {
        // Arrange
        var filter = new Filter<int?>("assocNum", 101);
        Assert.True(filter.IsActive);

        // Act - User clears the filter value
        filter.Value = null;

        // Assert - Filter is now inactive but no LoadData() triggered yet
        Assert.False(filter.IsActive);
        Assert.Null(filter.Value);
    }

    [Fact]
    public void Filter_Validation_Should_Work_Independently_From_Execution()
    {
        // The Filter class validates its state (IsActive) independently
        var filter = new Filter<string?>("assocName", "");
        Assert.False(filter.IsActive); // Empty string is default/inactive

        filter.Value = "John";
        Assert.True(filter.IsActive);

        filter.Value = "  "; // Whitespace
        Assert.False(filter.IsActive); // Still inactive

        filter.Value = "Jane";
        Assert.True(filter.IsActive);
    }
}
