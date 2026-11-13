# Aso4Mail Pro — current feature status (development only)

Black/gold Outlook-inspired WPF desktop shell. Features wired to real IMAP: account connection, folder discovery, listing and opening messages, search in loaded folder, unread filter, reply/reply-all/forward composition, archive/trash via MOVE, read/unread and flags, EML export, attachment saving, HTML-safe plain text view, per-account signatures with image references, SSL/TLS, periodic refresh while open, sending via SMTP, copying mail to Sent and saving new drafts on IMAP.

**Known limitations:** HTML is deliberately not rendered; body is plain text for safety pending security review. Draft editing and deleting an existing draft are not yet implemented. Attachment forwarding requires manually reattaching files. Folder rules, contacts and calendar are not implemented. This is not identical to Outlook or ready for customer production. No notifications or autostart. Requires independent security review and test against cPanel plus Microsoft Defender scan before use. Exact UI and feature parity cannot be guaranteed.

**Mail server limits:** 50 MiB attachments may exceed a 60 MB server-side message limit after MIME/base64 expansion; large sends can be rejected.
