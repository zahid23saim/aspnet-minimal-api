using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TaskApi.Tests;

// Spins the whole API up in memory with WebApplicationFactory and exercises the
// real HTTP endpoints -- no mocks, no running server needed.
//
// A fresh factory is created per test (constructor + Dispose) so each test gets
// its own in-memory store. That keeps the tests isolated -- one test's writes
// can't leak into another's assertions.
public class TaskApiTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private record TaskDto(int Id, string Title, bool Done);

    [Fact]
    public async Task Empty_store_returns_empty_list()
    {
        var client = _factory.CreateClient();
        var tasks = await client.GetFromJsonAsync<List<TaskDto>>("/tasks");
        Assert.NotNull(tasks);
        Assert.Empty(tasks!);
    }

    [Fact]
    public async Task Create_then_fetch_roundtrips()
    {
        var client = _factory.CreateClient();

        var created = await client.PostAsJsonAsync("/tasks", new { title = "write tests" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var task = await created.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(task);
        Assert.Equal("write tests", task!.Title);
        Assert.False(task.Done);

        var fetched = await client.GetFromJsonAsync<TaskDto>($"/tasks/{task.Id}");
        Assert.Equal(task.Id, fetched!.Id);
    }

    [Fact]
    public async Task Create_trims_title()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/tasks", new { title = "   spaced   " });
        var task = await res.Content.ReadFromJsonAsync<TaskDto>();
        Assert.Equal("spaced", task!.Title);
    }

    [Fact]
    public async Task Blank_title_is_rejected_with_400()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/tasks", new { title = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Missing_task_returns_404()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/tasks/99999");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Complete_marks_task_done()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/tasks", new { title = "finish" });
        var task = await created.Content.ReadFromJsonAsync<TaskDto>();

        var completed = await client.PutAsync($"/tasks/{task!.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var done = await completed.Content.ReadFromJsonAsync<TaskDto>();
        Assert.True(done!.Done);
    }

    [Fact]
    public async Task Delete_removes_task_then_404_on_second_delete()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/tasks", new { title = "temp" });
        var task = await created.Content.ReadFromJsonAsync<TaskDto>();

        var first = await client.DeleteAsync($"/tasks/{task!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.DeleteAsync($"/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }
}
