using Backend.Application.Auth.Commands.Register;
using Backend.Application.Common.Exceptions;
using Backend.Application.Users.Commands.UpdateUserSettings;
using Backend.Application.Users.Models;
using Backend.Application.Users.Queries.GetUserSettings;
using Backend.Domain.Enums;

namespace Backend.Application.FunctionalTests.Users;

public class UserSettingsTests : TestBase
{
    [Test]
    public async Task Get_AfterRegister_ReturnsDefaults()
    {
        var auth = await TestApp.SendAsync(
            new RegisterCommand("settings1@local", "Password123!", null, null));

        var settings = await TestApp.SendAsync(new GetUserSettingsQuery(auth.UserId));

        settings.UserId.ShouldBe(auth.UserId);
        settings.PreferredInputMode.ShouldBe(InputModeEnum.Text);
        settings.PreferredOutputMode.ShouldBe(OutputModeEnum.Text);
        settings.VoiceEnabled.ShouldBeFalse();
        settings.NotificationLevel.ShouldBe(NotificationLevelEnum.Normal);
        settings.ProactiveMode.ShouldBe(ProactiveModeEnum.Balanced);
        settings.PreferredLanguage.ShouldBe("en");
        settings.Timezone.ShouldBe("UTC");
        settings.QuietHoursStart.ShouldBeNull();
        settings.QuietHoursEnd.ShouldBeNull();
    }

    [Test]
    public async Task Update_PartialChange_OnlyUpdatesProvidedFields()
    {
        var auth = await TestApp.SendAsync(
            new RegisterCommand("settings2@local", "Password123!", null, null));

        var dto = new UpdateUserSettingDto(
            PreferredInputMode: null,
            PreferredOutputMode: null,
            VoiceEnabled: true,
            NotificationLevel: "High",
            ProactiveMode: null,
            QuietHoursStart: new TimeOnly(22, 0),
            QuietHoursEnd: new TimeOnly(7, 0),
            PreferredLanguage: null,
            Timezone: "Europe/Tashkent");

        var updated = await TestApp.SendAsync(new UpdateUserSettingsCommand(auth.UserId, dto));

        updated.VoiceEnabled.ShouldBeTrue();
        updated.NotificationLevel.ShouldBe(NotificationLevelEnum.High);
        updated.QuietHoursStart.ShouldBe(new TimeOnly(22, 0));
        updated.QuietHoursEnd.ShouldBe(new TimeOnly(7, 0));
        updated.Timezone.ShouldBe("Europe/Tashkent");

        // Untouched fields keep defaults.
        updated.PreferredInputMode.ShouldBe(InputModeEnum.Text);
        updated.PreferredLanguage.ShouldBe("en");
        updated.ProactiveMode.ShouldBe(ProactiveModeEnum.Balanced);
    }

    [Test]
    public async Task Update_InvalidEnum_ThrowsValidation()
    {
        var auth = await TestApp.SendAsync(
            new RegisterCommand("settings3@local", "Password123!", null, null));

        var dto = new UpdateUserSettingDto(
            PreferredInputMode: "garbage", null, null, null, null, null, null, null, null);

        await Should.ThrowAsync<ValidationException>(
            () => TestApp.SendAsync(new UpdateUserSettingsCommand(auth.UserId, dto)));
    }

    [Test]
    public async Task Update_QuietHoursMustBePaired()
    {
        var auth = await TestApp.SendAsync(
            new RegisterCommand("settings4@local", "Password123!", null, null));

        var dto = new UpdateUserSettingDto(
            null, null, null, null, null,
            QuietHoursStart: new TimeOnly(22, 0),
            QuietHoursEnd: null,
            null, null);

        await Should.ThrowAsync<ValidationException>(
            () => TestApp.SendAsync(new UpdateUserSettingsCommand(auth.UserId, dto)));
    }
}
