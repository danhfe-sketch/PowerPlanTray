# ⚡ Power Plan Tray (Native/No-IDE)

An ultra-lightweight, unobtrusive, and universal system tray power plan manager for Windows.

The main highlight of this project is its "hardcore/native" development approach: **it requires no heavy IDEs like Visual Studio**. The entire codebase can be edited in Notepad++ and natively compiled via PowerShell using the C# compiler already built into Windows.

## ✨ Features

* **Ultra-Lightweight:** The final executable is just a few kilobytes and consumes near-zero RAM.
* **Universal (Language-Proof):** Uses GUID parsing to identify power plans, ensuring it works flawlessly on any Windows localization (English, Portuguese, Japanese, etc.).
* **Full Management:** Quickly switch between plans, create new ones (cloning the active one), and delete inactive plans directly from the tray menu.
* **Auto-Startup:** Silently registers itself in the Windows Registry on its first run to start with the system automatically.

## 🚀 How to Compile (Via PowerShell)

You don't need to download Visual Studio. Windows already ships with the C# compiler (`csc.exe`) via the .NET Framework.

1. Download the `PowerPlanTray.cs` file and a custom icon (`icon.ico`) to the same folder.
2. Open **PowerShell**, navigate to that folder, and run the following commands:

```powershell
# Set the path to the native Windows C# compiler
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

# Compile the file and inject the icon
& $csc /target:winexe /win32icon:icon.ico /out:PowerPlanTray.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll PowerPlanTray.cs
