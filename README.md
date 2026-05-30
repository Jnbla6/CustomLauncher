<div align="center">

# 🚀 Custom Launcher

<img width="600" height="400" alt="image" src="https://github.com/user-attachments/assets/3874997f-4ae0-49d3-9698-72029b7e1af6" />


  <br><br>
  <strong>A lightweight, portable, and fully responsive Windows desktop workspace built with C# and WPF.</strong>
  <br><br>

  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 8">
  <img src="https://img.shields.io/badge/Platform-Windows%20(WPF)-0078D6?style=flat-square&logo=windows" alt="Platform: Windows">
  <a href="https://github.com/Jnbla6/CustomLauncher/releases/latest">
    <img src="https://img.shields.io/badge/Download-Latest_Release-2EA043?style=flat-square&logo=github" alt="Download Latest Release">
  </a>
</div>

---

## 🎯 What is Custom Launcher?
When working with multiple isolated programs, your desktop or start menu can quickly become cluttered. Custom Launcher solves that by acting as a central, customizable hub. It allows anyone to gather standalone applications, portable tools, or legacy `.exe` files into one unified, elegant workspace. It is highly effective for:

* **Creating a Custom Suite:** Grouping standalone software (like design, video, or audio editing tools) into a single, cohesive interface.
* **Developer & SysAdmin Toolboxes:** Organizing daily scripts, portable debugging tools, and server management utilities in one clean dashboard.
* **Enterprise & Teams:** Distributing a unified "company portal" to employees with one-click access to internal tools.
* **Personal Game Hubs:** Aggregating portable games or emulators into a custom library.

## ✨ Key Features
* **Dynamic App Management:** Add new programs directly from the UI using the built-in "+" interface. Browse for your `.exe` and add it on the fly.
* **Modern Bento Grid UI:** A sleek, minimalist Dark Theme interface featuring rounded corners, smooth hover effects, and a fluid layout that scales perfectly.
* **100% Clean & Portable:** The `.exe` sits alone wherever you place it. Application names and paths are saved silently in your Windows `%AppData%` folder, ensuring no extra files clutter your workspace.
* **Isolated Execution:** Programs are launched as independent background processes. Closing the launcher won't close your active tools, and a crashing app won't freeze the launcher.
* **Single-File Deployment:** Compiled into one standalone, self-contained `.exe` file that runs on Windows without needing external installers or .NET SDKs.

---

## 📥 Download & Usage

Get the latest standalone executable and start organizing your workspace immediately. No installation required!

1. Go to the **[Releases Page](https://github.com/Jnbla6/CustomLauncher/releases/latest)**.
2. Download `CustomLauncher.exe`.
3. Place it anywhere on your PC (e.g., your Desktop).
4. Run it and start dragging your apps!

> **⚠️ Note on Windows SmartScreen:** Because this is a newly built, open-source desktop application, Windows Defender might display a security warning ("Windows protected your PC") on the first launch. Simply click **More info** ➔ **Run anyway**.

---

### Running Locally
Clone this repository, open your terminal, and navigate to the project directory:
```bash
git clone [https://github.com/Jnbla6/CustomLauncher.git](https://github.com/Jnbla6/CustomLauncher.git)
cd CustomLauncher
dotnet run
```

@Badr
