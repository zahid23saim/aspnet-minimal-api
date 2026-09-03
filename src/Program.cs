using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// A tiny in-memory task store. Real apps would swap this for EF Core + a
// database; the endpoint shape stays the same.
var store = new ConcurrentDictionary<int, TodoTask>();
var nextId = 0;

// GET /tasks  -> list everything
app.MapGet("/tasks", () => store.Values.OrderBy(t => t.Id));

// GET /tasks/{id}  -> one task, or 404
app.MapGet("/tasks/{id:int}", (int id) =>
    store.TryGetValue(id, out var task)
        ? Results.Ok(task)
        : Results.NotFound());

// POST /tasks  -> create; validates the payload and returns 201 + Location
app.MapPost("/tasks", (CreateTask input) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = new[] { "Title is required." }
        });

    var id = Interlocked.Increment(ref nextId);
    var task = new TodoTask(id, input.Title.Trim(), Done: false);
    store[id] = task;
    return Results.Created($"/tasks/{id}", task);
});

// PUT /tasks/{id}/complete  -> mark done, or 404
app.MapPut("/tasks/{id:int}/complete", (int id) =>
{
    if (!store.TryGetValue(id, out var task))
        return Results.NotFound();
    store[id] = task with { Done = true };
    return Results.Ok(store[id]);
});

// DELETE /tasks/{id}  -> 204 if removed, 404 if it was never there
app.MapDelete("/tasks/{id:int}", (int id) =>
    store.TryRemove(id, out _) ? Results.NoContent() : Results.NotFound());

app.Run();

// Records used as the API's data model.
public record TodoTask(int Id, string Title, bool Done);
public record CreateTask(string? Title);

// Make the implicit Program class visible to WebApplicationFactory in tests.
public partial class Program { }
