# Aso4Mail — Black Edition UI + Windows Notifications (development branch)

This branch restores the refined black-and-gold UI principles from the old Rust edition in the C# WPF version. Email stays on cPanel/WHM IMAP/SMTP servers. Notifications use the Windows toast API; there is no PowerShell invocation or remote push server.

## Notification behavior
- Inbox checks occur every 30 seconds while Aso4Mail is running, including when the window is hidden to system tray.
- Each connected account is monitored. Messages already present at first check establish a baseline and do **not** generate bulk notifications.
- Clicking a notification requests the corresponding account's Inbox message. If it was moved/deleted, Aso4Mail shows the Inbox instead.
- Exit from the tray menu stops the process. No automatic login startup is installed.
- Notification and hide-to-tray preferences are user-controlled.
- Windows notification permissions and installed desktop-app identity affect toast delivery. A GitHub-built unsigned EXE is not proof of reliable notification identity.

## Security gate
This is an unverified development build. Build success is **not** a malware safety certification.
After GitHub Actions passes, test on a fresh Windows VM and scan all output with Windows Defender; seek independent review before use with customer accounts. Do not restore or execute old quarantined Rust releases.
