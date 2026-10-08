# LGMS — Local Git Management System

**LGMS (Local Git Management System)** is a local-first Git management desktop tool built with **ASP.NET Core MVC .NET 9**. It allows developers to select a working folder, automatically discover all local Git repositories inside it, inspect working trees, view unified and side-by-side diffs, stage/unstage files, commit, push, pull, fetch, manage branches, inspect commit history, stash changes, manage remotes, and execute commands in an integrated safe Git terminal.

---

## 1. Requirements

- **.NET 9 SDK** (`dotnet --version` >= 9.0.100)
- **Git** for Windows / macOS / Linux (`git --version` >= 2.25)
- Modern web browser (Chrome, Edge, Firefox, Safari)

---

## 2. Architecture

```text
Browser
   ↓ (Razor Views / AJAX Fetch / HTML5 / CSS3)
ASP.NET Core MVC (.NET 9)
   ↓
GitService & RepositoryScanner
   ↓
IProcessService (System.Diagnostics.Process)
   ↓
git.exe / git CLI
   ↓
Local Git Repositories
```

- **Local-first & Private**: No cloud telemetry, no remote tracking, and no external code uploading.
- **Path Isolation**: Directory validation prevents path traversal outside the chosen workspace.
- **Safe Process Invocation**: Uses `System.Diagnostics.Process` with UTF-8 streams and arguments list without shell concatenation.

---

## 3. Project Structure

```text
LGMS/
├── Controllers/
│   ├── HomeController.cs        # Workspace selection & root dashboard
│   ├── RepositoryController.cs  # Repository discovery & dashboard
│   ├── GitController.cs         # Working tree status, diff, staging, commit, push, pull
│   ├── BranchController.cs      # Branch creation, checkout, merge, deletion
│   ├── CommitController.cs      # Commit log history, details inspector, and patch diff
│   ├── SettingsController.cs    # Git binary detection, workspace configuration, themes
│   └── TerminalController.cs    # Sandboxed Git terminal within active repository
│
├── Services/
│   ├── IProcessService.cs       # Asynchronous process runner with timeout and cancellation
│   ├── ProcessService.cs
│   ├── IRepositoryScanner.cs    # Safe recursive repository discovery (.git detection)
│   ├── RepositoryScanner.cs
│   ├── IGitService.cs           # Core Git command facade
│   └── GitService.cs
│
├── Models/
│   ├── GitResult.cs             # Structured Git execution result
│   ├── Repository.cs            # Repository metadata
│   ├── GitStatus.cs             # Parsed working tree status
│   ├── GitChange.cs             # Staged/unstaged file change model
│   ├── GitCommit.cs             # Commit log entry
│   ├── GitBranch.cs             # Branch model (local & remote)
│   ├── GitRemote.cs             # Remotes configuration
│   ├── GitStash.cs              # Stash entry
│   └── WorkspaceSettings.cs     # App configuration model
│
├── ViewModels/
│   └── ViewModels.cs            # Typed view models for MVC views
│
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml       # Master layout with IDE structure
│   │   ├── _Sidebar.cshtml      # Collapsible IDE navigation
│   │   ├── _TopBar.cshtml       # Top action toolbar and active repo status
│   │   ├── _Toast.cshtml        # Reusable notifications
│   │   └── _LoadingOverlay.cshtml # Live Git command progress overlay
│   ├── Home/Index.cshtml
│   ├── Repository/Index.cshtml
│   ├── Repository/Dashboard.cshtml
│   ├── Git/Changes.cshtml
│   ├── Git/Diff.cshtml
│   ├── Branch/Index.cshtml
│   ├── Commit/Index.cshtml
│   ├── Git/Stashes.cshtml
│   ├── Git/Remotes.cshtml
│   ├── Terminal/Index.cshtml
│   └── Settings/Index.cshtml
│
├── wwwroot/
│   ├── css/
│   │   ├── site.css             # IDE base styling
│   │   ├── dark.css             # Dark theme variables & rules (default)
│   │   └── light.css            # Light theme variables & rules
│   └── js/
│       └── site.js              # Vanilla JavaScript for AJAX and UI
│
├── appsettings.json
├── Program.cs
└── LGMS.csproj
```

---

## 4. Running the Application

### Restore and Run with .NET 9

```bash
cd LGMS
dotnet restore
dotnet build
dotnet run
```

Open your browser at:
```text
http://localhost:5000  (or https://localhost:5001)
```

### Publish for Production

```bash
dotnet publish -c Release -o ./publish
```

---

## 5. Key Features

- **Workspace Selection & Scanning**: Fast recursive directory enumeration without external scripting.
- **Safety First**: Discarding files, force-pushing, and branch deletion require explicit confirmation.
- **Unified & Side-by-Side Diff Viewer**: Real git diff coloring for added (`+`), removed (`-`), and hunk (`@@`) lines.
- **Commit & Push Flow**: Conventional commit message box, staged change checklist, and one-click Commit & Push.
- **Pull Conflict Prevention**: Warns if dirty changes exist before pulling.
- **Dark & Light Themes**: Responsive IDE layout with CSS variables.
