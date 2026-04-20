using MauiMessenger.Api.Services;
using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MauiMessenger.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/agents")]
public sealed class AgentsController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly AgentMessengerOptions _agentMessengerOptions;

    public AgentsController(
        IUserService userService,
        IOptions<AgentMessengerOptions> agentMessengerOptions)
    {
        _userService = userService;
        _agentMessengerOptions = agentMessengerOptions.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> ListAsync(CancellationToken cancellationToken)
    {
        if (!HasValidAgentMessengerKey())
        {
            return Unauthorized("Missing or invalid AgentMessenger API key.");
        }

        return Ok(await _userService.ListAgentsAsync(cancellationToken));
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> RegisterAsync(
        [FromBody] RegisterAgentRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasValidAgentMessengerKey())
        {
            return Unauthorized("Missing or invalid AgentMessenger API key.");
        }

        try
        {
            var agent = await _userService.RegisterAgentAsync(request, cancellationToken);
            return Ok(agent);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private bool HasValidAgentMessengerKey()
    {
        if (!Request.Headers.TryGetValue("X-AgentMessenger-Key", out var key))
        {
            return false;
        }

        return string.Equals(key.ToString(), _agentMessengerOptions.ApiKey, StringComparison.Ordinal);
    }
}
