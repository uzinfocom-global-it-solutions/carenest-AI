using Backend.Application.Auth.Commands.Register;
using Backend.Application.Chats.Commands.CreateChat;
using Backend.Application.Chats.Commands.SendMessage;
using Backend.Application.Chats.Queries.GetChatHistory;
using Backend.Application.Common.Exceptions;
using Backend.Application.Families.Commands.CreateFamily;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.FunctionalTests.Chats;

public class ChatFlowTests : TestBase
{
    [Test]
    public async Task CreateChat_StoresWithFamilyAndType()
    {
        var (userId, familyId) = await SetupFamilyAsync("chat-create@local");

        var chat = await TestApp.SendAsync(new CreateChatCommand(familyId, userId, "Family", "Daily plan"));

        chat.FamilyId.ShouldBe(familyId);
        chat.ChatType.ShouldBe(ChatTypeEnum.Family);
        chat.Title.ShouldBe("Daily plan");

        var stored = await TestApp.FindAsync<Chat>(chat.Id);
        stored.ShouldNotBeNull();
    }

    [Test]
    public async Task CreateChat_RejectsInvalidType()
    {
        var (userId, familyId) = await SetupFamilyAsync("chat-bad-type@local");

        await Should.ThrowAsync<ValidationException>(
            () => TestApp.SendAsync(new CreateChatCommand(familyId, userId, "Garbage", null)));
    }

    [Test]
    public async Task SendMessage_StoresUserAndAiMessagesWithParsedIntent()
    {
        var (userId, familyId) = await SetupFamilyAsync("chat-intent@local");
        var chat = await TestApp.SendAsync(new CreateChatCommand(familyId, userId, "Family", null));

        var aiMessage = await TestApp.SendAsync(
            new SendMessageCommand(chat.Id, userId, "Will it rain today?", "Text"));

        aiMessage.SenderType.ShouldBe(SenderTypeEnum.Ai);
        aiMessage.ParsedIntent.ShouldNotBeNullOrEmpty();
        aiMessage.ParsedIntent!.ShouldContain("intent");

        var rows = await TestApp.CountWhereAsync<ChatMessage>(m => m.ChatId == chat.Id);
        rows.ShouldBe(2); // user message + AI response
    }

    [Test]
    public async Task GetHistory_ReturnsRecentMessages()
    {
        var (userId, familyId) = await SetupFamilyAsync("chat-history@local");
        var chat = await TestApp.SendAsync(new CreateChatCommand(familyId, userId, "Family", null));

        await TestApp.SendAsync(new SendMessageCommand(chat.Id, userId, "Hello", "Text"));
        await TestApp.SendAsync(new SendMessageCommand(chat.Id, userId, "Question?", "Text"));

        var history = await TestApp.SendAsync(new GetChatHistoryQuery(chat.Id, userId, Limit: 10));

        history.Count.ShouldBeGreaterThanOrEqualTo(4); // 2 sends × (user + ai)
    }

    [Test]
    public async Task SendMessage_RejectsNonMember()
    {
        var (ownerId, familyId) = await SetupFamilyAsync("chat-rbac-a@local");
        var outsider = await TestApp.SendAsync(
            new RegisterCommand("chat-rbac-b@local", "Password123!", null, null));

        var chat = await TestApp.SendAsync(new CreateChatCommand(familyId, ownerId, "Family", null));

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => TestApp.SendAsync(new SendMessageCommand(chat.Id, outsider.UserId, "Hi", "Text")));
    }

    private static async Task<(string UserId, int FamilyId)> SetupFamilyAsync(string email)
    {
        var auth = await TestApp.SendAsync(new RegisterCommand(email, "Password123!", null, null));
        var family = await TestApp.SendAsync(
            new CreateFamilyCommand("Family", auth.UserId, null, null, null));
        return (auth.UserId, family.Id);
    }
}
