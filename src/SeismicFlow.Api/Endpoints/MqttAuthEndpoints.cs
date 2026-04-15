using Microsoft.AspNetCore.Authorization;
using SeismicFlow.Application.Common.Interfaces;

namespace SeismicFlow.Api.Endpoints;

public static class MqttAuthEndpoints
{
    public static void MapMqttAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/mqtt/auth")
            .WithTags("MQTT Auth")
            .AllowAnonymous();

        group.MapPost("/user", AuthenticateUser).WithName("MqttAuthUser");
        group.MapPost("/acl", CheckAcl).WithName("MqttAuthAcl");
    }

    private static async Task<IResult> AuthenticateUser(
        HttpRequest request,
        IMqttCredentialService mqttCredentialService,
        CancellationToken ct)
    {
        string? username = null;
        string? password = null;

        var contentType = request.ContentType ?? "";

        if (contentType.Contains("application/json"))
        {
            var body = await request.ReadFromJsonAsync<MqttAuthRequest>(ct);
            username = body?.Username;
            password = body?.Password;
        }
        else
        {
            var form = await request.ReadFormAsync(ct);
            username = form["username"].ToString();
            password = form["password"].ToString();
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return Results.Unauthorized();

        // Internal system consumer — bypass device credential check
        var sysUser = Environment.GetEnvironmentVariable("MQTT_CONSUMER_USERNAME") ?? "sf-consumer";
        var sysPass = Environment.GetEnvironmentVariable("MQTT_CONSUMER_PASSWORD") ?? "sf-consumer-secret";
        if (username == sysUser && password == sysPass)
            return Results.Ok();

        var isValid = await mqttCredentialService.ValidateCredentialsAsync(username, password, ct);
        return isValid ? Results.Ok() : Results.Unauthorized();
    }

    private static async Task<IResult> CheckAcl(
        HttpRequest request,
        IMqttCredentialService mqttCredentialService,
        CancellationToken ct)
    {
        string? username = null;
        string? topic = null;
        string? acc = null;

        var contentType = request.ContentType ?? "";

        if (contentType.Contains("application/json"))
        {
            var body = await request.ReadFromJsonAsync<MqttAclRequest>(ct);
            username = body?.Username;
            topic = body?.Topic;
            acc = body?.Acc.ToString();
        }
        else
        {
            var form = await request.ReadFormAsync(ct);
            username = form["username"].ToString();
            topic = form["topic"].ToString();
            acc = form["acc"].ToString();
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(topic))
            return Results.Unauthorized();

        // System consumer can read everything
        var sysUser = Environment.GetEnvironmentVariable("MQTT_CONSUMER_USERNAME") ?? "sf-consumer";
        if (username == sysUser)
            return Results.Ok();

        var isAllowed = await mqttCredentialService.CheckAclAsync(username, topic, acc ?? "2", ct);
        return isAllowed ? Results.Ok() : Results.Unauthorized();
    }
}

public sealed record MqttAuthRequest(string Username, string Password);
public sealed record MqttAclRequest(string Username, string Topic, int Acc);