# Security / Release Gate

Only source is committed. No credentials, updates, telemetry, notifications, background startup, PowerShell or arbitrary command execution in the application source.

TLS certificate validation is left enabled for IMAP/SMTP. Incoming HTML is displayed as plain text, not executed. Account JSON is protected with Windows DPAPI CurrentUser. Third-party packages require NuGet restore and audit.

The published Windows artifact is unsigned and *UNVERIFIED*. No malware-free guarantee. Previous flagged Rust/Tauri executables are not part of this repository. Never deploy this MVP to company mailboxes without independent security review and live tests.
