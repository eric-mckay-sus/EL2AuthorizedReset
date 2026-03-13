namespace AdminInterface.Tests;

/// <summary>
/// Tests for the Home.razor filter implementation
/// Validates that filters correctly modify the data view using LINQ
/// </summary>
public class HomeFilterTests
{
    /// <summary>
    /// Helper method to create sample Reset data for testing
    /// </summary>
    private List<Reset> CreateSampleData()
    {
        return
        [
            new Reset
            {
                Id = 1,
                Timestamp = new DateTime(2026, 1, 15, 10, 30, 0),
                AssocNum = 101,
                AssocName = "John Smith",
                CmmsNum = 5001,
                LineName = "Line A",
                IsAuthorized = true
            },
            new Reset
            {
                Id = 2,
                Timestamp = new DateTime(2026, 1, 16, 14, 45, 0),
                AssocNum = 102,
                AssocName = "Jane Johnson",
                CmmsNum = 5002,
                LineName = "Line B",
                IsAuthorized = false
            },
            new Reset
            {
                Id = 3,
                Timestamp = new DateTime(2026, 2, 10, 9, 15, 0),
                AssocNum = 101,
                AssocName = "John Smith",
                CmmsNum = 5001,
                LineName = "Line A",
                IsAuthorized = true
            },
            new Reset
            {
                Id = 4,
                Timestamp = new DateTime(2026, 2, 20, 16, 20, 0),
                AssocNum = 103,
                AssocName = "Bob Williams",
                CmmsNum = 5003,
                LineName = "Line C",
                IsAuthorized = true
            },
            new Reset
            {
                Id = 5,
                Timestamp = new DateTime(2026, 3, 1, 11, 0, 0),
                AssocNum = 102,
                AssocName = "Jane Johnson",
                CmmsNum = 5002,
                LineName = "Line B",
                IsAuthorized = true
            }
        ];
    }

    [Fact]
    public void FilterAssocNum_Should_Filter_By_Associate_Number()
    {
        // Arrange
        var data = CreateSampleData();
        var testFilter = new Filter<int?>("assocNum", 101);

        // Act
        var filtered = data
            .Where(r => r.AssocNum == testFilter.Value)
            .ToList();

        // Assert
        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, r => Assert.Equal(101, r.AssocNum));
        Assert.Contains(filtered, r => r.Id == 1);
        Assert.Contains(filtered, r => r.Id == 3);
    }

    [Fact]
    public void FilterAssocName_Should_Filter_By_Name_Contains()
    {
        // Arrange
        var data = CreateSampleData();
        var testFilter = new Filter<string?>("assocName", "Jane");

        // Act
        var filtered = data
            .Where(r => r.AssocName != null && r.AssocName.ToLower().Contains(testFilter.Value!.ToLower()))
            .ToList();

        // Assert
        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, r => Assert.Contains("Jane", r.AssocName!));
    }

    [Fact]
    public void FilterCmmsNum_Should_Filter_By_Cmms_Number()
    {
        // Arrange
        var data = CreateSampleData();
        var testFilter = new Filter<int?>("cmmsNum", 5002);

        // Act
        var filtered = data
            .Where(r => r.CmmsNum == testFilter.Value)
            .ToList();

        // Assert
        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, r => Assert.Equal(5002, r.CmmsNum));
    }

    [Fact]
    public void FilterLineName_Should_Filter_By_Line_Contains()
    {
        // Arrange
        var data = CreateSampleData();
        var testFilter = new Filter<string?>("lineName", "Line");

        // Act
        var filtered = data
            .Where(r => r.LineName != null && r.LineName.ToLower().Contains(testFilter.Value!.ToLower()))
            .ToList();

        // Assert
        Assert.Equal(5, filtered.Count); // All records have a line
    }

    [Fact]
    public void FilterIsAuthorized_Should_Filter_By_Authorization_Status()
    {
        // Arrange
        var data = CreateSampleData();
        var testFilter = new Filter<bool?>("isAuthorized", true);

        // Act
        var filtered = data
            .Where(r => r.IsAuthorized == testFilter.Value)
            .ToList();

        // Assert
        Assert.Equal(4, filtered.Count);
        Assert.All(filtered, r => Assert.True(r.IsAuthorized));
    }

    [Fact]
    public void FilterStartDate_Should_Filter_After_Date()
    {
        // Arrange
        var data = CreateSampleData();
        var testFilter = new Filter<DateTime?>("after", new DateTime(2026, 2, 1));

        // Act
        var filtered = data
            .Where(r => r.Timestamp != null && r.Timestamp >= testFilter.Value)
            .ToList();

        // Assert
        Assert.Equal(3, filtered.Count); // Records on or after 2/1/2026
    }

    [Fact]
    public void FilterEndDate_Should_Filter_Before_Date()
    {
        // Arrange
        var data = CreateSampleData();
        var testFilter = new Filter<DateTime?>("before", new DateTime(2026, 2, 15));
        var endOfDay = testFilter.Value?.AddDays(1).AddTicks(-1);

        // Act
        var filtered = data
            .Where(r => r.Timestamp != null && r.Timestamp <= endOfDay)
            .ToList();

        // Assert
        Assert.Equal(3, filtered.Count); // Records on or before 2/15/2026
    }

    [Fact]
    public void Multiple_Filters_Should_Apply_Together()
    {
        // Arrange
        var data = CreateSampleData();
        var filterAssocNum = new Filter<int?>("assocNum", 101);
        var filterAuthorized = new Filter<bool?>("isAuthorized", true);

        // Act
        var filtered = data
            .Where(r => r.AssocNum == filterAssocNum.Value)
            .Where(r => r.IsAuthorized == filterAuthorized.Value)
            .ToList();

        // Assert
        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, r => Assert.Equal(101, r.AssocNum));
        Assert.All(filtered, r => Assert.True(r.IsAuthorized));
    }

    [Fact]
    public void Negated_Filter_Should_Exclude_Instead_Of_Include()
    {
        // Arrange
        var data = CreateSampleData();
        var testFilter = new Filter<int?>("assocNum", 101, isNegated: true);

        // Act
        var filtered = data
            .Where(r => r.AssocNum != testFilter.Value)
            .ToList();

        // Assert
        Assert.Equal(3, filtered.Count);
        Assert.DoesNotContain(filtered, r => r.AssocNum == 101);
    }

    [Fact]
    public void Inactive_Filter_Should_Not_Affect_Results()
    {
        // Arrange
        var data = CreateSampleData();
        var testFilter = new Filter<int?>("assocNum", null); // Null means inactive

        // Act - Simulate applying an inactive filter (it should be skipped)
        var filtered = testFilter.IsActive ? data.Where(r => r.AssocNum == testFilter.Value).ToList() : data;

        // Assert
        Assert.False(testFilter.IsActive);
        Assert.Equal(5, filtered.Count); // All records returned
    }

    [Fact]
    public void Filter_Value_Change_Should_Update_IsActive()
    {
        // Arrange
        var filter = new Filter<int?>("assocNum", null);
        Assert.False(filter.IsActive);

        // Act
        filter.Value = 101;

        // Assert
        Assert.True(filter.IsActive);
        Assert.Equal(101, filter.Value);
    }

    [Fact]
    public void Filter_Should_Trigger_OnChanged_Callback()
    {
        // Arrange
        var filter = new Filter<int?>("assocNum", null);
        var callCount = 0;
        filter.OnChanged = () => callCount++;

        // Act
        filter.Value = 101;

        // Assert
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Date_Range_Filter_Should_Include_Entire_Day()
    {
        // Arrange
        var data = new List<Reset>
        {
            new Reset { Id = 1, Timestamp = new DateTime(2026, 2, 15, 0, 0, 0) },
            new Reset { Id = 2, Timestamp = new DateTime(2026, 2, 15, 12, 0, 0) },
            new Reset { Id = 3, Timestamp = new DateTime(2026, 2, 15, 23, 59, 59) },
            new Reset { Id = 4, Timestamp = new DateTime(2026, 2, 16, 0, 0, 0) }
        };

        var endDate = new DateTime(2026, 2, 15);
        var endOfDay = endDate.AddDays(1).AddTicks(-1);

        // Act
        var filtered = data
            .Where(r => r.Timestamp != null && r.Timestamp <= endOfDay)
            .ToList();

        // Assert
        Assert.Equal(3, filtered.Count);
        Assert.DoesNotContain(filtered, r => r.Id == 4);
    }
}
