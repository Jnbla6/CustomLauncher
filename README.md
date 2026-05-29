# 🚀 White Label Launcher

A lightweight, dynamic, and fully responsive Windows desktop launcher built with C# and WPF. Designed as a flexible "white-label" solution, this project allows anyone to gather multiple standalone applications, portable tools, or legacy `.exe` files into one unified, elegant workspace.

## 🎯 The Case for a White Label Launcher
When working with multiple isolated programs, your desktop or start menu can quickly become cluttered. This launcher solves that by acting as a central, customizable hub. It is highly effective for:
* **Creating a Custom Suite:** Grouping standalone software (like isolated design, video, or audio editing tools) into a single, cohesive interface.
* **Developer & SysAdmin Toolboxes:** Organizing daily scripts, portable debugging tools, and server management utilities in one clean dashboard.
* **Enterprise & Teams:** Distributing a unified "company portal" to employees that provides one-click access to internal tools and resources.
* **Personal Game Hubs:** Aggregating portable games or emulators into a custom library.

## ✨ Key Features
* **Dynamic App Management:** Add new programs directly from the UI using the built-in "+" interface. Browse for your `.exe` and add it on the fly.
* **Modern Bento Grid UI:** A sleek, minimalist Dark Theme interface featuring rounded corners, smooth hover effects, and a fluid layout that scales perfectly whether you have 4 apps or 40.
* **Portable Configuration:** Application names and paths are saved locally in a lightweight `apps.json` file, making it easy to backup or share your setup.
* **Isolated Execution:** Programs are launched as independent background processes. Closing the launcher won't close your active tools, and a crashing app won't freeze the launcher.
* **Single-File Deployment:** Ready to be compiled into one standalone, self-contained `.exe` file that runs on Windows without needing external installers.

---

## 🛠️ Getting Started

### Prerequisites
* Windows OS (x64)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download) (or newer) installed on your machine.

### Running from Source
1. Clone this repository or download the source code.
2. Open your terminal (Command Prompt or PowerShell) and navigate to the project directory:
   ```bash
   cd path/to/WhiteLabelLauncher
   ```
3. Run the application directly:
   ```bash
   dotnet run
   ```
*Note: On the first launch, the app will automatically generate an `apps.json` file in the root directory.*

### Building the Standalone Executable
To generate a single `.exe` file that you can share with others or pin to your taskbar (without requiring them to have the .NET SDK installed):

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
You will find your ready-to-use executable in `bin\Release\net8.0-windows\win-x64\publish\`.

---

## 🎨 Customization Guide

Because this is a white-label solution, you are encouraged to rebrand it to fit your exact needs!

### 1. Changing the Visual Theme (Colors & Grid)
Open `MainWindow.xaml` to modify the primary aesthetics:
* **Background Colors:** Search for the `Background` attributes (currently utilizing dark hex codes like `#121212` and `#1E1E1E`) and replace them with your brand colors.
* **Grid Layout:** The applications are arranged inside an ItemsControl using a `WrapPanel`. You can adjust the `ItemWidth`, `ItemHeight`, or `Margin` properties to change the card sizes.
* **Window Properties:** The app uses a borderless window (`WindowStyle="None"`). You can adjust the `CornerRadius` to make the window edges sharper or more rounded.

### 2. Changing the Application Icon
To replace the default window and executable icon:
1. Place your own `app.ico` file in the root directory of the project.
2. Open `WhiteLabelLauncher.csproj`.
3. Locate the `<ApplicationIcon>` tag and ensure it points to your new `.ico` file:
   ```xml
   <ApplicationIcon>app.ico</ApplicationIcon>
   ```

### 3. Pre-loading Apps for Distribution
If you are packaging this launcher to give to a team, you can pre-configure the software list. Simply edit the `apps.json` file, add your custom paths, and distribute the JSON file alongside your compiled `.exe`. The launcher will read it automatically on startup.
