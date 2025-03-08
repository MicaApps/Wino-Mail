using Wino.Core.Domain.Interfaces;

namespace Wino.Calendar.Services;

public class CalendarAuthenticatorConfig : IAuthenticatorConfig
{
    public string OutlookAuthenticatorClientId => "d26be8d1-9975-4654-aa42-35b398759196";

    public string[] OutlookScope => new string[]
    {
        "Calendars.Read",
        "Calendars.Read.Shared",
        "offline_access",
        "Calendars.ReadBasic",
        "Calendars.ReadWrite",
        "Calendars.ReadWrite.Shared",
        "User.Read"
    };

    public string GmailAuthenticatorClientId => "973025879644-s7b4ur9p3rlgop6a22u7iuptdc0brnrn.apps.googleusercontent.com";

    public string[] GmailScope => new string[]
    {
        "https://www.googleapis.com/auth/calendar",
        "https://www.googleapis.com/auth/calendar.events",
        "https://www.googleapis.com/auth/calendar.settings.readonly",
        "https://www.googleapis.com/auth/userinfo.profile",
        "https://www.googleapis.com/auth/userinfo.email"
    };

    public string GmailTokenStoreIdentifier => "WinoCalendarGmailTokenStore";
}
