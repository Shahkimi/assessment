using TaskManager.Api.Contracts.Tasks;
using TaskManager.Api.Domain;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Tests;

public class TaskRequestValidatorTests
{
    private readonly CreateTaskRequestValidator _create = new();
    private readonly UpdateTaskRequestValidator _update = new();
    private readonly UpdateTaskStatusRequestValidator _status = new();

    [Fact]
    public void Create_valid_request_passes()
    {
        var result = _create.Validate(new CreateTaskRequest("Write docs", null, TaskPriority.High, null));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_title(string? title)
    {
        var result = _create.Validate(new CreateTaskRequest(title, null, TaskPriority.Low, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Title");
    }

    [Fact]
    public void Create_rejects_title_over_200_chars()
    {
        var result = _create.Validate(new CreateTaskRequest(new string('a', 201), null, TaskPriority.Low, null));
        Assert.Contains(result.Errors, e => e.PropertyName == "Title");
    }

    [Fact]
    public void Create_accepts_title_of_exactly_200_chars()
    {
        var result = _create.Validate(new CreateTaskRequest(new string('a', 200), null, TaskPriority.Low, null));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Create_rejects_description_over_2000_chars()
    {
        var result = _create.Validate(new CreateTaskRequest("t", new string('d', 2001), TaskPriority.Low, null));
        Assert.Contains(result.Errors, e => e.PropertyName == "Description");
    }

    [Fact]
    public void Create_rejects_missing_priority()
    {
        var result = _create.Validate(new CreateTaskRequest("t", null, null, null));
        Assert.Contains(result.Errors, e => e.PropertyName == "Priority");
    }

    [Fact]
    public void Create_rejects_out_of_range_priority()
    {
        var result = _create.Validate(new CreateTaskRequest("t", null, (TaskPriority)42, null));
        Assert.Contains(result.Errors, e => e.PropertyName == "Priority");
    }

    [Fact]
    public void Create_allows_past_due_date()
    {
        // Old tasks must stay editable, so a past due date is not an error.
        var result = _create.Validate(new CreateTaskRequest("t", null, TaskPriority.Low, new DateOnly(2000, 1, 1)));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Update_applies_same_rules_as_create()
    {
        var result = _update.Validate(new UpdateTaskRequest(" ", null, null, null));

        Assert.Contains(result.Errors, e => e.PropertyName == "Title");
        Assert.Contains(result.Errors, e => e.PropertyName == "Priority");
    }

    [Fact]
    public void Status_rejects_missing_value()
    {
        Assert.False(_status.Validate(new UpdateTaskStatusRequest(null)).IsValid);
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.Done)]
    public void Status_accepts_known_values(TaskItemStatus status)
    {
        Assert.True(_status.Validate(new UpdateTaskStatusRequest(status)).IsValid);
    }

    [Fact]
    public void Status_rejects_unknown_value()
    {
        Assert.False(_status.Validate(new UpdateTaskStatusRequest((TaskItemStatus)9)).IsValid);
    }
}
