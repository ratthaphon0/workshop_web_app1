using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using TodoApi.Dtos;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// Services
// ===============================

builder.Services.AddOpenApi();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new Exception("JWT Key not found");

var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(key),

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ===============================
// Development
// ===============================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ใช้ HTTP ก่อนสำหรับ workshop
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();


// ===============================
// Fake Database
// ===============================

var todos = new List<TodoGetDto>
{
    new(1, "Learn C#", true),
    new(2, "Learn ASP.NET Core", false),
    new(3, "Build a web API", false)
};


// ===============================
// LOGIN
// ===============================

var authGroup =
    app.MapGroup("/api/auth")
        .WithTags("Authentication");

authGroup.MapPost("/login", (LoginDto login) =>
{
    const string username = "admin";
    const string password = "1234";

    if (
        login.Username != username ||
        login.Password != password
    )
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(
            ClaimTypes.Name,
            login.Username
        ),

        new Claim(
            ClaimTypes.Role,
            "Admin"
        )
    };

    var credentials =
        new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256
        );

    var jwtToken =
        new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

    var token =
        new JwtSecurityTokenHandler()
            .WriteToken(jwtToken);

    return Results.Ok(new
    {
        token,
        expiresIn = 3600
    });
});


// ===============================
// GET ALL
// ===============================

var todoGroup =
    app.MapGroup("/api/todos")
        .WithTags("Todos")
        .RequireAuthorization();

todoGroup.MapGet("", () =>
{
    return Results.Ok(todos);
});


// ===============================
// GET BY ID
// ===============================

todoGroup.MapGet("/{id:int}", (int id) =>
{
    var todo =
        todos.FirstOrDefault(t => t.Id == id);

    if (todo is null)
    {
        return Results.NotFound(new
        {
            message = "Todo not found"
        });
    }

    return Results.Ok(todo);
});


// ===============================
// CREATE
// ===============================

todoGroup.MapPost("", (TodoPostDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Title))
    {
        return Results.BadRequest(new
        {
            message = "Title is required"
        });
    }

    var nextId =
        todos.Count == 0
            ? 1
            : todos.Max(t => t.Id) + 1;

    var todo =
        new TodoGetDto(
            nextId,
            dto.Title,
            false
        );

    todos.Add(todo);

    return Results.Created(
        $"/api/todos/{todo.Id}",
        todo
    );
});


// ===============================
// UPDATE
// ===============================

todoGroup.MapPut(
    "/{id:int}",
    (int id, TodoPutDto dto) =>
    {
        try
        {
            var index =
                todos.FindIndex(t => t.Id == id);

            if (index == -1)
            {
                return Results.NotFound(new
                {
                    message = "Todo not found"
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return Results.BadRequest(new
                {
                    message = "Title is required"
                });
            }

            var updatedTodo =
                new TodoGetDto(
                    id,
                    dto.Title,
                    dto.IsCompleted
                );

            todos[index] = updatedTodo;

            return Results.Ok(updatedTodo);
        }
        catch (Exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unable to update todo"
            );
        }
    }
);


// ===============================
// DELETE
// ===============================

todoGroup.MapDelete("/{id:int}", (int id) =>
{
    try
    {
        var todo =
            todos.FirstOrDefault(t => t.Id == id);

        if (todo is null)
        {
            return Results.NotFound(new
            {
                message = "Todo not found"
            });
        }

        todos.Remove(todo);

        return Results.NoContent();
    }
    catch (Exception)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Unable to delete todo"
        );
    }
});


app.Run();