# Victoria email (IMAP/SMTP)

Victoria has **email_*** tools so she can check, sort, file, mark, delete, and reply. She manages three named mailboxes:

| Id | Role | Whose |
| --- | --- | --- |
| `victoria` | victoria | **Hers** — provision a mailbox for her (new Gmail or your domain) |
| `personal` | personal | Kurt personal |
| `business` | business | Kurt business |

Host never creates those mailboxes. You create them, turn on IMAP, and put **app passwords** in `SoulCore/.env`. Never commit addresses or passwords.

## Tools

| Tool | What | Gate |
| --- | --- | --- |
| `email_accounts` | List configured mailboxes (no secrets) | none |
| `email_inbox` | Recent messages (`unread_only` optional) | AllowEmailRead |
| `email_read` | One message body by uid | AllowEmailRead |
| `email_search` | From / subject / body search | AllowEmailRead |
| `email_file` | Move to folder or Gmail label (sort) | AllowEmailRead |
| `email_mark` | Read/unread + flag/star | AllowEmailRead |
| `email_delete` | Delete (Trash when available) | AllowEmailDelete + `confirmed=true` |
| `email_send` | New mail or reply (`reply_to_uid`) | AllowEmailSend + `confirmed=true` |

Send and delete are two-phase: first call returns a confirm prompt. She tells you; you say yes; she recalls with `confirmed=true`.

## Gmail setup (each account)

1. Google Account → Security → 2-Step Verification **on**.
2. App passwords → Mail → generate a 16-character password.
3. Enable IMAP in Gmail Settings → Forwarding and POP/IMAP.

Other hosts: set `ImapHost` / `SmtpHost` / ports. Port 993 = IMAP SSL; 587 = SMTP STARTTLS; 465 = SMTP implicit TLS (`SmtpUseSsl=true`).

## Host env (never commit)

Copy from `SoulCore/.env.example`. Example:

```text
SOULCORE_Email__Accounts__0__Id=victoria
SOULCORE_Email__Accounts__0__Role=victoria
SOULCORE_Email__Accounts__0__DisplayName=Victoria
SOULCORE_Email__Accounts__0__Address=victoria@example.com
SOULCORE_Email__Accounts__0__Username=victoria@example.com
SOULCORE_Email__Accounts__0__Password=

SOULCORE_Email__Accounts__1__Id=personal
SOULCORE_Email__Accounts__1__Role=personal
SOULCORE_Email__Accounts__1__Address=
SOULCORE_Email__Accounts__1__Username=
SOULCORE_Email__Accounts__1__Password=

SOULCORE_Email__Accounts__2__Id=business
SOULCORE_Email__Accounts__2__Role=business
SOULCORE_Email__Accounts__2__Address=
SOULCORE_Email__Accounts__2__Username=
SOULCORE_Email__Accounts__2__Password=
```

Restart Host after editing `.env`.

## Session gates

Default **off**. After Host is up:

- ChatDesktop → Settings → **Tools & Access** → Email checkboxes, or
- `POST /settings/tools` with `allowEmailRead` / `allowEmailSend` / `allowEmailDelete`, or
- `SOULCORE_Tools__AllowEmailRead=true` (and send/delete as needed) then restart.

## Chat

Ask her to “check my email”, “check your inbox”, “search personal email for …”, “file that to Archive”. She should call `email_*`, not open Gmail in the browser.

## Safety

- Passwords only in `.env` / user-secrets.
- No logging of password values.
- Send/delete never auto-fire from a single model call.
- SoulLoop still does not drive email acts.
