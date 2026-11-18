using System.Text.Json;
using System.Text.Json.Serialization;
using HarborOps.Api.Authentication;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Incidents;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HarborOps.Api.Incidents;

public static class IncidentEndpoints
{
    private const int MaximumBodyBytes = 8192;
    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    private static CurrentSession Session(HttpContext context) =>
        (CurrentSession)context.Items["IdentitySession"]!;

    private static IResult Failure(HttpContext context,int status,string code,string message) =>
        Results.Json(new { code, message, correlationId = context.Items["CorrelationId"] },
            statusCode: status);

    private static bool Id(string? value,out Guid result) =>
        Guid.TryParse(value,out result) && result != Guid.Empty;

    private static bool OperationId(HttpContext context,out Guid result)
    {
        result = default;
        var header = context.Request.Headers["X-HarborOps-Operation-Id"];
        return header.Count == 1 && Id(header.ToString(),out result);
    }

    private static async Task<T?> Body<T>(HttpContext context) where T : class
    {
        var type=context.Request.ContentType ?? "";
        if (!type.Equals("application/json",StringComparison.OrdinalIgnoreCase) &&
            !type.StartsWith("application/json;",StringComparison.OrdinalIgnoreCase))
            return null;
        if (context.Request.ContentLength is > MaximumBodyBytes or <= 0) return null;
        var data = new byte[MaximumBodyBytes+1];
        var read=0;
        try
        {
            while(read < data.Length)
            {
                var count=await context.Request.Body.ReadAsync(
                    data.AsMemory(read,data.Length-read),context.RequestAborted);
                if(count==0)break;
                read+=count;
            }
            if(read is 0 or > MaximumBodyBytes)return null;
            return JsonSerializer.Deserialize<T>(data.AsSpan(0,read),BodyOptions);
        }
        catch(JsonException) { return null; }
        catch(ArgumentException) { return null; }
    }

    private static async Task<bool> MutationAllowed(HttpContext ctx,IAntiforgery anti) =>
        AuthenticationSetup.IsAllowedTransport(ctx,
            ctx.RequestServices.GetRequiredService<IConfiguration>()) &&
        await AuthenticationSetup.ValidMutationAsync(ctx,anti);

    private static async Task<IResult> Guarded(HttpContext ctx,Func<Task<IResult>> work)
    {
        ctx.Response.Headers.CacheControl="no-store";
        try { return await work(); }
        catch (IncidentRequestException ex) {
            return Failure(ctx,ex.Status,ex.Code,ex.Status==404?"Resource not found":
                ex.Status==409?"The incident changed or the operation conflicts with its current state":
                "The requested incident action is not permitted");
        }
        catch (ArgumentException) { return Failure(ctx,400,"invalid_request","Invalid incident request"); }
        catch (DbUpdateException) { return Failure(ctx,503,"dependency_unavailable","Incident storage unavailable"); }
        catch (NpgsqlException) { return Failure(ctx,503,"dependency_unavailable","Incident storage unavailable"); }
        catch (TimeoutException) { return Failure(ctx,503,"dependency_unavailable","Incident storage unavailable"); }
    }

    public static void MapIncidentEndpoints(this WebApplication app)
    {
        // M18 is never activated in a retained/default deployment by migration alone.
        if (!app.Configuration.GetValue<bool>("M18:EnableIncidents")) return;
        var root=app.MapGroup("/api/v1/organizations/{organizationId}/incidents")
            .RequireAuthorization().RequireRateLimiting("m18-incidents");

        root.MapGet("",(string organizationId,HttpContext ctx,IncidentStore store)=>
            Guarded(ctx,async()=>
            {
                if(!Id(organizationId,out var org))return Failure(ctx,400,"invalid_request","Invalid incident request");
                var query=ctx.Request.Query;
                if(query.Keys.Any(k=>k!="limit") || query.Any(x=>x.Value.Count!=1))
                    return Failure(ctx,400,"invalid_request","Invalid incident filter");
                var limit=25;
                if(query.ContainsKey("limit")&&(!int.TryParse(query["limit"],out limit)||limit is <1 or >100))
                    return Failure(ctx,400,"invalid_request","Invalid incident limit");
                var list=await store.ListAsync(Session(ctx),org,limit,ctx.RequestAborted);
                return Results.Ok(new { incidents=list });
            }));

        root.MapGet("/{incidentId}",(string organizationId,string incidentId,
            HttpContext ctx,IncidentStore store)=>
            Guarded(ctx,async()=>
            {
                if(!Id(organizationId,out var org)||!Id(incidentId,out var id))
                    return Failure(ctx,400,"invalid_request","Invalid incident request");
                var item=await store.GetAsync(Session(ctx),org,id,true,ctx.RequestAborted);
                return Results.Ok(item);
            }));

        root.MapPost("",(string organizationId,HttpContext ctx,
            IAntiforgery anti,IncidentStore store)=>
            Guarded(ctx,async()=>
            {
                if(!await MutationAllowed(ctx,anti))
                    return Failure(ctx,403,"csrf_invalid","Request validation failed");
                if(!Id(organizationId,out var org)||!OperationId(ctx,out var operation))
                    return Failure(ctx,400,"invalid_request","Invalid incident request");
                var input=await Body<IncidentCreate>(ctx);
                if(input is null)return Failure(ctx,400,"invalid_request","Invalid incident request");
                var value=await store.CreateAsync(Session(ctx),org,input,operation,ctx.RequestAborted);
                return Results.Json(value.Incident,
                    statusCode:value.Replayed?200:201);
            }));

        root.MapPost("/{incidentId}/transitions",
            (string organizationId,string incidentId,HttpContext ctx,
                IAntiforgery anti,IncidentStore store)=>
            Guarded(ctx,async()=>
            {
                if(!await MutationAllowed(ctx,anti))
                    return Failure(ctx,403,"csrf_invalid","Request validation failed");
                if(!Id(organizationId,out var org)||!Id(incidentId,out var id)||
                    !OperationId(ctx,out var operation))
                    return Failure(ctx,400,"invalid_request","Invalid incident request");
                var input=await Body<IncidentTransition>(ctx);
                if(input is null)return Failure(ctx,400,"invalid_request","Invalid incident request");
                return Results.Ok((await store.TransitionAsync(Session(ctx),org,id,input,
                    operation,false,ctx.RequestAborted)).Incident);
            }));

        root.MapPost("/{incidentId}/reopen",
            (string organizationId,string incidentId,HttpContext ctx,
                IAntiforgery anti,IncidentStore store)=>
            Guarded(ctx,async()=>
            {
                if(!await MutationAllowed(ctx,anti))
                    return Failure(ctx,403,"csrf_invalid","Request validation failed");
                if(!Id(organizationId,out var org)||!Id(incidentId,out var id)||
                    !OperationId(ctx,out var operation))
                    return Failure(ctx,400,"invalid_request","Invalid incident request");
                var input=await Body<IncidentTransition>(ctx);
                if(input is null)return Failure(ctx,400,"invalid_request","Invalid incident request");
                return Results.Ok((await store.TransitionAsync(Session(ctx),org,id,input,
                    operation,true,ctx.RequestAborted)).Incident);
            }));

        root.MapPost("/{incidentId}/notes",
            (string organizationId,string incidentId,HttpContext ctx,
                IAntiforgery anti,IncidentStore store)=>
            Guarded(ctx,async()=>
            {
                if(!await MutationAllowed(ctx,anti))
                    return Failure(ctx,403,"csrf_invalid","Request validation failed");
                if(!Id(organizationId,out var org)||!Id(incidentId,out var id)||
                    !OperationId(ctx,out var operation))
                    return Failure(ctx,400,"invalid_request","Invalid incident request");
                var input=await Body<IncidentNoteInput>(ctx);
                if(input is null)return Failure(ctx,400,"invalid_request","Invalid incident request");
                return Results.Ok((await store.AddNoteAsync(Session(ctx),org,id,input,
                    operation,ctx.RequestAborted)).Incident);
            }));

        root.MapPatch("/{incidentId}/assignment",
            (string organizationId,string incidentId,HttpContext ctx,
                IAntiforgery anti,IncidentStore store)=>
            Guarded(ctx,async()=>
            {
                if(!await MutationAllowed(ctx,anti))
                    return Failure(ctx,403,"csrf_invalid","Request validation failed");
                if(!Id(organizationId,out var org)||!Id(incidentId,out var id)||
                    !OperationId(ctx,out var operation))
                    return Failure(ctx,400,"invalid_request","Invalid incident request");
                var input=await Body<IncidentAssignment>(ctx);
                if(input is null)return Failure(ctx,400,"invalid_request","Invalid incident request");
                return Results.Ok((await store.AssignAsync(Session(ctx),org,id,input,
                    operation,ctx.RequestAborted)).Incident);
            }));
    }
}
