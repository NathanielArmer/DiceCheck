using DiceCheck.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;

namespace DiceCheck.Web;

public partial class Program 
{ 
    public static WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllersWithViews();

        // Add CORS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Configure JSON options
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });

        // In development, the React dev server will be used
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.WithOrigins("http://localhost:5173") // Vite dev server default port
                           .AllowAnyHeader()
                           .AllowAnyMethod();
                });
            });
        }

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }
        else
        {
            app.UseDeveloperExceptionPage();
            app.UseCors();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        // API Endpoints
        app.MapPost("/api/roll", (RollRequest request) =>
        {
            try
            {
                if (request.Sides <= 0)
                    return Results.BadRequest("Number of sides must be positive");
                if (request.NumberOfDice <= 0)
                    return Results.BadRequest("Number of dice must be positive");

                // Validate conditions
                if (request.Conditions?.Any() == true)
                {
                    foreach (var condition in request.Conditions)
                    {
                        // Parse and validate condition type
                        if (!Enum.TryParse<ConditionType>(condition.Type, true, out var conditionType))
                            return Results.BadRequest($"Invalid condition type: {condition.Type}");

                        // Validate count for CountMatching condition
                        if (conditionType == ConditionType.CountMatching && !condition.Count.HasValue)
                            return Results.BadRequest("Count is required for CountMatching condition");

                        // Store the parsed condition type
                        condition.ParsedType = conditionType;
                    }
                }

                var roll = new DiceRoll(request.Sides, request.NumberOfDice);
                var result = roll.Roll();
                
                if (request.Conditions?.Any() == true)
                {
                    try
                    {
                        var conditions = request.Conditions.Select(c => 
                        {
                            if (c.ParsedType == ConditionType.CountMatching && c.Count.HasValue)
                            {
                                return RollCondition.CreateCountMatching(c.Count.Value, c.Value);
                            }
                            return RollCondition.Create(c.ParsedType, c.Value);
                        }).ToList();
                            
                        return Results.Ok(new
                        {
                            Values = result.Values,
                            Sum = result.Sum,
                            Conditions = conditions.Select(c => new
                            {
                                Condition = c.ToString(),
                                Satisfied = c.Evaluate(result)
                            })
                        });
                    }
                    catch (ArgumentException ex)
                    {
                        return Results.BadRequest(ex.Message);
                    }
                }
                
                return Results.Ok(new
                {
                    Values = result.Values,
                    Sum = result.Sum
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in /api/roll: {ex}");
                return Results.Problem(ex.Message);
            }
        });

        // Serve index.html for all non-API routes to support client-side routing
        app.MapFallbackToFile("index.html");

        return app;
    }

    public static void Main(string[] args)
    {
        CreateApp(args).Run();
    }
}

public class RollRequest
{
    public int Sides { get; set; }
    public int NumberOfDice { get; set; }
    public List<ConditionRequest>? Conditions { get; set; }
}

public class ConditionRequest
{
    public string? Type { get; set; }
    public int Value { get; set; }
    public int? Count { get; set; }
    internal ConditionType ParsedType { get; set; }
}
