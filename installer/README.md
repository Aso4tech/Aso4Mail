# Aso4Mail native Windows installer (unverified)
Build this **only after the C# .NET build succeeds** and a separate security review confirms the EXE is appropriate to test.

1. Publish the Windows application using the GitHub Actions artifact.
2. Put the unzipped published files inside a folder named `publish` next to `Aso4Mail-10.1.iss`.
3. Open the ISS file with Inno Setup 6 and compile.
4. Install on a **Windows test virtual machine**, not a customer workstation, then verify the program is listed as Aso4Mail in Windows notification settings.
5. Run Windows Defender scans on both Aso4Mail.exe and the installer. Never disable antivirus to install.

The installer uses `C:\Program Files\Aso4Tech\Aso4Mail`, adds Start/desktop shortcuts with AppUserModelID, does not set auto-start and does not run PowerShell.

A successful compile or unsigned artifact is *not a guarantee of safety*. Previous Rust/Tauri builds have been quarantined and are not reused in this source branch.
