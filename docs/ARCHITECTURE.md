# MauiMessenger Architecture & Data Model

## Goals
- Provide a maintainable MAUI app structure for chat features.
- Keep the UI responsive while messaging data is processed or synced.
- Enable gradual expansion (authentication, attachments, multi-device sync) without large refactors.

## System Architecture
### Layered Overview
- **Presentation (Views)**: `MainPage`, `ChatPage`, and navigation via `AppShell` render UI controls and bind to view models.
- **Presentation Logic (ViewModels)**: `ChatViewModel` and future view models expose observable state, commands for user actions, and validation/errors. They do not handle I/O directly.
- **Domain Services**: `IChatService` coordinates message creation, fetching conversations, and applies domain rules (e.g., trimming empty messages, limiting length, deduplicating). `IUserService` resolves the current user/session. These services are testable and platform-agnostic.
- **Data Access**: Repositories abstract persistence and remote calls. Examples: `IMessageRepository` (local DB or in-memory), `IConversationRepository`, and `IRemoteChatClient` (HTTP/SignalR). Services depend on interfaces so implementations can be swapped (e.g., offline-first vs. cloud-backed).
- **Platform/Infrastructure**: MAUI dependency injection in `MauiProgram` registers services and repositories, configures `HttpClient`, and provides platform-specific implementations (e.g., local storage paths, notifications).

### Component Responsibilities
- **App Initialization**: `MauiProgram` builds DI container, registers view models/services, configures routing, and creates the root `App`/`AppShell`.
- **Navigation Flow**: `MainPage` offers entry points to chats; `ChatPage` binds to `ChatViewModel` for a selected conversation. Navigation passes the conversation identifier as a parameter.
- **Message Lifecycle**:
  1. User enters text; `ChatViewModel.SendMessageCommand` validates and calls `IChatService.SendAsync`.
  2. `IChatService` enriches the message (author, timestamps, client-generated id) and persists via `IMessageRepository`.
  3. If a remote backend exists, `IChatService` forwards the message to `IRemoteChatClient` and updates message status (e.g., Pending → Sent → Delivered/Failed) based on callbacks or polling results.
  4. View models observe repository streams/collections to update the UI reactively.
- **Error & Offline Handling**: Services surface status codes and human-readable errors. Pending messages can be retried by `IChatService` when connectivity returns.

### Deployment/Runtime View
- **Client-only (current)**: Uses in-memory or local SQLite repositories; no remote client. Suitable for demos and offline usage.
- **Client + Backend (future)**: Adds `IRemoteChatClient` (HTTP/SignalR) hosted separately. Authentication and push notifications can be layered without breaking the UI or domain layers.

## Data Model
| Entity | Key Fields | Notes |
| --- | --- | --- |
| `User` | `Id` (GUID/string), `DisplayName`, `AvatarUrl`, `Status` | Represents the local or remote participant; stored locally for quick lookup. |
| `Conversation` | `Id`, `Title`, `LastMessagePreview`, `LastUpdatedUtc`, `ParticipantIds` | Group or direct chat; used for navigation lists and ordering. |
| `Message` | `Id` (GUID), `ConversationId`, `SenderId`, `Text`, `CreatedUtc`, `Status` (`Pending`, `Sent`, `Delivered`, `Failed`), `ReplyToMessageId?`, `Attachments` | Core chat payload; supports threading/replies and status updates. |
| `Attachment` | `Id`, `MessageId`, `Type` (image/file/audio), `Uri`, `ThumbnailUri?`, `SizeBytes` | Stored separately so messages can have zero or more attachments. |
| `TypingIndicator` | `ConversationId`, `UserId`, `IsTyping`, `ExpiresUtc` | Optional real-time UX element; can be transient (memory-only). |

### Repository Interfaces
- `IMessageRepository`
  - `Task<IEnumerable<Message>> GetByConversationAsync(string conversationId, int limit, int offset)`
  - `IAsyncEnumerable<Message> StreamByConversationAsync(string conversationId)` for live updates
  - `Task SaveAsync(Message message)` and `Task UpdateStatusAsync(string messageId, MessageStatus status)`
- `IConversationRepository`
  - `Task<IEnumerable<Conversation>> GetRecentAsync(int limit, int offset)`
  - `Task SaveAsync(Conversation conversation)` and `Task UpdatePreviewAsync(string conversationId, string text, DateTime updatedUtc)`
- `IUserRepository`
  - `Task<User?> GetCurrentAsync()` and `Task SaveAsync(User user)`

### Data Flow (Send Message)
1. View binds to `ChatViewModel.SendMessageCommand`.
2. ViewModel validates text and calls `IChatService.SendAsync(conversationId, text)`.
3. `IChatService` builds a `Message` with `Status = Pending`, saves it, and raises an observable update.
4. Service attempts remote send via `IRemoteChatClient`; on success, status moves to `Sent`/`Delivered`; on failure, status becomes `Failed` with error information for retry.

### Additional Considerations
- **Validation**: Services enforce max lengths, prevent empty messages, and normalize whitespace before persistence.
- **Sync Strategy**: Use client-generated IDs and timestamps to merge remote and local histories; conflicts resolved by server timestamp precedence.
- **Telemetry & Logging**: Wrap platform logging to capture message failures, retries, and navigation events for diagnostics.
