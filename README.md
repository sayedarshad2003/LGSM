# 🚀 LGMS — Local Git Management System (.NET 9)

**LGMS (Local Git Management System)** is a comprehensive, local-first Git management suite designed for developers who want a fast, secure, and modern interface for managing multiple Git repositories.

It offers two frontend experiences built on a shared, battle-tested .NET 9 Core engine:
1. 🖥️ **LGMS Desktop**: A high-performance, native **WPF Desktop Application** (.NET 9) with Dark & Light (White) themes, interactive diff viewer, staging manager, branch/stash/remote controls, and integrated sandboxed console.
2. 🌐 **LGMS Web**: An **ASP.NET Core MVC 9 Web Application** with Razor views, RESTful Git endpoints, and responsive IDE layout.

---

## 📑 Table of Contents

- [1. System Requirements](#1-system-requirements)
- [2. Solution Architecture](#2-solution-architecture)
- [3. Complete Installation & Setup Guide](#3-complete-installation--setup-guide)
- [4. How to Run the Applications](#4-how-to-run-the-applications)
- [5. Feature Workflow Walkthrough](#5-feature-workflow-walkthrough)
  - [Workspace Scanning & Discovery](#1-workspace-scanning--discovery)
  - [Working Tree & Staging Manager](#2-working-tree--staging-manager)
  - [Syntax-Highlighted Diff Viewer](#3-syntax-highlighted-diff-viewer)
  - [Commit & Push Flow](#4-commit--push-flow)
  - [Commit History & Patch Inspector](#5-commit-history--patch-inspector)
  - [Branch Management](#6-branch-management)
  - [Stashes & Remotes](#7-stashes--remotes)
  - [Safe Git Console](#8-safe-git-console)
  - [Theming & Settings](#9-theming--settings)
- [6. Building Single-File Executables for Desktop](#6-building-single-file-executables-for-desktop)
- [7. Project File Structure](#7-project-file-structure)
- [8. Troubleshooting & FAQ](#8-troubleshooting--faq)

---

## 1. System Requirements

| Requirement | Minimum Version | Recommended Version |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 (1809+) or Windows 11 | Windows 11 (x64) |
| **.NET SDK** | .NET 9.0 SDK (`9.0.100`+) | .NET 9.0.305+ |
| **Git for Windows** | Git 2.25+ | Git 2.40+ (in system `PATH`) |
| **Memory (RAM)** | 2 GB | 4 GB+ |
| **IDE (Optional)** | VS Code or Visual Studio 2022 (v17.12+) | Visual Studio 2022 / Antigravity IDE |

---

## 2. Solution Architecture

The solution ([`LGMS.sln`](file:///e:/Asp.NET%20Work/Test%20Project/LGMS/LGMS.sln)) is cleanly modularized into 3 dedicated projects:

```text
LGMS.sln
│
├── 🧠 LGMS.Core/         → Shared Core Class Library (.NET 9)
│   ├── Models/           → GitStatus, GitCommit, GitBranch, GitChange, GitStash, WorkspaceSettings
│   └── Services/         → GitService, ProcessService, RepositoryScanner
│
├── 🖥️ LGMS.Desktop/      → Native WPF Desktop App (.NET 9 Windows)
│   ├── Themes/           → DarkTheme.xaml, LightTheme.xaml, Icons.xaml
│   ├── ViewModels/       → MainViewModel, GitItemViewModel (MVVM)
│   ├── Services/         → SettingsService (persists to %LOCALAPPDATA%/LGMS)
│   └── Views/            → MainWindow.xaml
│
└── 🌐 LGMS/              → ASP.NET Core MVC 9 Web App
    ├── Controllers/      → GitController, BranchController, CommitController, TerminalController
    ├── Views/            → Razor Views & Layouts
    └── wwwroot/          → CSS Themes & Vanilla JavaScript
```

### 🔒 Security & Performance Principles:
- **Zero Cloud Leakage**: No telemetry, no third-party cloud uploads. All Git operations run locally on your machine.
- **Safe Process Invocation**: Uses `System.Diagnostics.Process` with UTF-8 encoding and isolated argument lists (preventing command injection / shell quoting issues).
- **Non-blocking Asynchronous I/O**: Operations (Fetch, Pull, Push, Diff parsing) execute without freezing the user interface.

---

## 3. Complete Installation & Setup Guide

### Step 1: Verify .NET 9 Installation
Open PowerShell or Command Prompt and run:
```powershell
dotnet --version
```
> If you don't have .NET 9 SDK installed, download and install it from the official Microsoft portal:  
> 👉 [Download .NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Step 2: Verify Git CLI
Make sure Git is installed and added to your environment `PATH`:
```powershell
git --version
```
*(If Git is not installed, install [Git for Windows](https://git-scm.com/download/win).)*

### Step 3: Navigate to the Solution Directory
```powershell
cd "e:\Asp.NET Work\Test Project\LGMS"
```

### Step 4: Restore and Build All Projects
```powershell
dotnet restore LGMS.sln
dotnet build LGMS.sln
```
All 3 projects (`LGMS.Core`, `LGMS.Desktop`, and `LGMS`) will compile with `0 Errors`.

---

## 4. How to Run the Applications

### Option A: Run the WPF Desktop Application (Recommended)

To launch the native Windows desktop client:
```powershell
dotnet run --project LGMS.Desktop/LGMS.Desktop.csproj
```
*(Or in Visual Studio: Set `LGMS.Desktop` as the Startup Project and press `F5`.)*

---

### Option B: Run the ASP.NET Core MVC Web Application

To launch the web-based version:
```powershell
dotnet run --project LGMS.csproj
```
Once started, open your web browser at:
```text
http://localhost:5000   (or https://localhost:5001)
```

---

## 5. Feature Workflow Walkthrough

### 1. Workspace Scanning & Discovery
- Click **"Browse..."** in the top bar to choose any root directory containing multiple Git repositories (e.g., `C:\Projects` or `e:\Asp.NET Work`).
- LGMS recursively identifies all Git repositories, displaying:
  - Repository name and current branch (`main`, `master`, `feature/*`).
  - Working tree health (`clean` badge or yellow `X chg` badge).
  - Sync counters (`↑2` commits ahead of remote, `↓1` behind).
- Use the **Filter search box** in the left sidebar to filter repositories instantly.

### 2. Working Tree & Staging Manager (`⚡ Changes` Tab)
- **Split Staging Panels**:
  - **Unstaged Changes**: View modified (`M`), added (`A`), deleted (`D`), and untracked (`??`) files.
  - **Staged Changes**: Files prepared for the next commit.
- **Actions**:
  - Click `+` to stage individual files or **"Stage All"** for the whole working tree.
  - Click `−` to unstage files or **"Unstage All"**.
  - Click the **Trash icon** or **"Discard"** button to discard changes (with a confirmation safety prompt).

### 3. Syntax-Highlighted Diff Viewer
- Selecting any changed file renders an instant diff:
  - 🟢 **Green lines (`+`)**: Additions with new line numbers.
  - 🔴 **Red lines (`-`)**: Deletions with old line numbers.
  - 🔵 **Cyan lines (`@@`)**: Hunk headers and code context.

### 4. Commit & Push Flow
- Enter your **Commit Summary** (e.g., `feat: add modern dark theme`) and optional multi-line details.
- Click **"Commit"** to commit locally, or click **"Commit & Push"** to commit and immediately sync with remote upstream.

### 5. Commit History & Patch Inspector (`📜 History` Tab)
- Table view of past commits with hash badges, commit message, author name, and relative timestamps (e.g., `2 hours ago`).
- Click any commit to view its full patch diff and affected files in the inspector drawer.

### 6. Branch Management (`🌿 Branches` Tab)
- View local and remote tracking branches (`origin/*`).
- Active branch is highlighted with a `CURRENT` badge.
- **Create Branch**: Type a branch name (e.g., `feature/login`) and click **"Create & Switch"**.
- **Checkout / Delete**: Switch branches with one click or safely delete merged branches.

### 7. Stashes & Remotes (`📦 Stashes` & `🌐 Remotes` Tabs)
- **Stashes**: Save work-in-progress changes with a custom message. Apply, pop, or drop existing stashes (`stash@{0}`).
- **Remotes**: Inspect remote fetch and push URLs or add new remote origins.

### 8. Safe Git Console (`💻 Git Console` Tab)
- Execute Git commands directly (e.g., `status`, `log --oneline -n 10`, `remote -v`).
- Real-time command output with automatic synchronization with repository status.

### 9. Theming & Settings (`⚙️ Settings` Tab & Top Bar)
- **☀️ Light Theme (White Mode)**: Clean slate and bright card aesthetic for daylight environments.
- **🌙 Dark Theme**: High-contrast obsidian & slate dark theme with cyan/emerald glows.
- **Toggle anytime**: Click the **☀️ / 🌙** button in the top right or configure it under the **Settings** tab.
- **Settings Persistence**: Default workspace path and preferences are stored in `%LOCALAPPDATA%/LGMS/settings.json`.

---

## 6. Building Single-File Executables for Desktop

To generate a standalone Windows desktop executable that runs without needing the .NET SDK installed:

```powershell
dotnet publish LGMS.Desktop/LGMS.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist
```

The compiled standalone executable will be generated at `./dist/LGMS.Desktop.exe`.

---

## 7. Project File Structure

```text
LGMS/
├── LGMS.sln                          # Master Visual Studio Solution
│
├── LGMS.Core/                        # Shared Business & Git Logic
│   ├── LGMS.Core.csproj
│   ├── Models/
│   │   ├── GitBranch.cs              # Branch metadata model
│   │   ├── GitChange.cs              # Staged/unstaged file change model
│   │   ├── GitCommit.cs              # Commit entry model
│   │   ├── GitRemote.cs              # Remote repository model
│   │   ├── GitResult.cs              # Structured execution response
│   │   ├── GitStash.cs               # Stash item model
│   │   ├── GitStatus.cs              # Working tree status & change lists
│   │   ├── Repository.cs             # Local repo summary & sync counts
│   │   └── WorkspaceSettings.cs      # User settings definition
│   └── Services/
│       ├── IProcessService.cs        # Async process runner contract
│       ├── ProcessService.cs         # CLI executor with UTF-8 support
│       ├── IRepositoryScanner.cs     # Recursive repo discoverer contract
│       ├── RepositoryScanner.cs      # Deep repo searcher & sanitizer
│       ├── IGitService.cs            # Git command facade interface
│       └── GitService.cs             # Full Git command implementations
│
├── LGMS.Desktop/                     # Modern WPF Desktop Client (.NET 9)
│   ├── LGMS.Desktop.csproj
│   ├── App.xaml / App.xaml.cs        # DI bootstrap & resource merging
│   ├── MainWindow.xaml / .xaml.cs    # 7-tab modern IDE window & event handling
│   ├── Common/
│   │   ├── ObservableObject.cs       # MVVM INotifyPropertyChanged base
│   │   ├── RelayCommand.cs           # Sync & Async ICommand implementations
│   │   ├── Converters.cs             # ValueConverters (status colors, diff brushes, tab logic)
│   │   └── ThemeManager.cs           # Dynamic runtime theme switcher (Dark / Light)
│   ├── ViewModels/
│   │   ├── MainViewModel.cs          # Core desktop UI state machine & commands
│   │   └── GitItemViewModel.cs       # Change selection & diff wrappers
│   ├── Services/
│   │   └── SettingsService.cs        # Persists settings to %LOCALAPPDATA%/LGMS
│   └── Themes/
│       ├── DarkTheme.xaml            # Modern dark color palette & control templates
│       ├── LightTheme.xaml           # Clean white/light color palette
│       └── Icons.xaml                # Vector geometry path icons
│
└── LGMS/                             # ASP.NET Core MVC 9 Web Application
    ├── LGMS.csproj
    ├── Program.cs                    # Web host configuration & DI
    ├── Controllers/                  # MVC API Controllers
    ├── Views/                        # Razor View Templates
    └── wwwroot/                      # Static web assets & styling
```

---

## 8. Troubleshooting & FAQ

### Q1: "Git not detected in PATH" warning appears on startup.
- **Solution**: Install Git from [git-scm.com](https://git-scm.com) and ensure the checkbox *"Add Git to system PATH"* is checked during installation. Restart LGMS.

### Q2: Why are changes not showing up?
- **Solution**: Click the **🔄 Refresh** button in the top toolbar or press `Fetch`. If you just added new files, make sure they are not ignored in your repository's `.gitignore`.

### Q3: How do I change the default workspace folder?
- **Solution**: Go to the **⚙️ Settings** tab, click **"Browse..."** to select your favorite folder, and click **"Save Path Settings"**. It will automatically load this folder whenever you launch LGMS.

### Q4: Can I run both the Desktop and Web app at the same time?
- **Solution**: Yes. Both applications access local repositories via `LGMS.Core` and standard Git CLI locks, allowing safe side-by-side execution.

---

## 📜 License & Credits

Built with ❤️ using **.NET 9**, **WPF (Windows Presentation Foundation)**, and **ASP.NET Core MVC**.
All operations are local-first, privacy-focused, and open source.
