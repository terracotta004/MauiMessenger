using System.Text.Json;
using MauiMessenger.Core.DTOs;
using MauiMessenger.Core.Entities;
using MauiMessenger.Core.Interfaces;
using MauiMessenger.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MauiMessenger.Api.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly UserManager<User> _userManager;
    private readonly AgentMessengerOptions _agentMessengerOptions;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MessagesController(
        IMessageService messageService,
        UserManager<User> userManager,
        IOptions<AgentMessengerOptions> agentMessengerOptions)
    {
        _messageService = messageService;
        _userManager = userManager;
        _agentMessengerOptions = agentMessengerOptions.Value;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> ListByConversationAsync([FromQuery] Guid? conversationId, CancellationToken cancellationToken)
    {
        if (conversationId is null || conversationId == Guid.Empty)
        {
            return BadRequest("conversationId query parameter is required.");
        }

        var messages = await _messageService.ListByConversationIdAsync(conversationId.Value, cancellationToken);
        return Ok(messages);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult> CreateAsync([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        if (IsAgentMessage(payload))
        {
            if (!HasValidAgentMessengerKey())
            {
                return Unauthorized("Missing or invalid AgentMessenger API key.");
            }

            var agentRequest = payload.Deserialize<AgentMessageRequest>(JsonOptions);
            if (agentRequest is null)
            {
                return BadRequest("Invalid AgentMessenger message payload.");
            }

            try
            {
                var created = await _messageService.SendAgentMessageAsync(agentRequest, cancellationToken);
                return Ok(created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        var request = payload.Deserialize<CreateMessageRequest>(JsonOptions);
        if (request is null)
        {
            return BadRequest("Invalid message payload.");
        }

        try
        {
            var created = await _messageService.CreateAsync(request, cancellationToken);
            return Ok(created);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound("Conversation not found.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<MessageDto>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var deleted = await _messageService.DeleteAsync(id, user.Id, cancellationToken);
            return Ok(deleted);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound("Message not found.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpGet("inbox/{identity}")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<AgentMessageDto>>> AgentInboxAsync(
        string identity,
        CancellationToken cancellationToken)
    {
        if (!HasValidAgentMessengerKey())
        {
            return Unauthorized("Missing or invalid AgentMessenger API key.");
        }

        var messages = await _messageService.ListAgentInboxAsync(identity, cancellationToken);
        return Ok(messages);
    }

    private bool HasValidAgentMessengerKey()
    {
        if (!Request.Headers.TryGetValue("X-AgentMessenger-Key", out var key))
        {
            return false;
        }

        return string.Equals(key.ToString(), _agentMessengerOptions.ApiKey, StringComparison.Ordinal);
    }

    private static bool IsAgentMessage(JsonElement payload)
        => payload.ValueKind == JsonValueKind.Object
            && HasProperty(payload, "from")
            && HasProperty(payload, "to")
            && HasProperty(payload, "body");

    private static bool HasProperty(JsonElement payload, string propertyName)
        => payload.EnumerateObject().Any(property =>
            string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
}
