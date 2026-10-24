namespace House.ChatDesktop.Services;

/// <summary>
/// Contacts Open chat. The foreground persona shows the drawer and does not activate.
/// A cancelled quarantine confirm does not switch and does not open the drawer.
/// </summary>
public readonly record struct ContactsOpenChat(bool ShowDrawer, bool Activate)
{
    public static ContactsOpenChat Resolve(bool alreadyForeground, bool quarantineConfirmed)
    {
        if (alreadyForeground)
            return new ContactsOpenChat(ShowDrawer: true, Activate: false);

        if (!quarantineConfirmed)
            return new ContactsOpenChat(ShowDrawer: false, Activate: false);

        return new ContactsOpenChat(ShowDrawer: true, Activate: true);
    }
}
