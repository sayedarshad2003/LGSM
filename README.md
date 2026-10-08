# LGMS — Local Git Management System (.NET 9)

**LGMS (Local Git Management System)** is a local-first Git management suite featuring both a **Modern Native WPF Desktop Application** and an **ASP.NET Core MVC Web Application**, powered by a shared **.NET 9 Core Engine (`LGMS.Core`)**.

---

## 1. Solution Architecture

```text
LGMS.sln
├── LGMS.Core/        # Shared core library (GitService, ProcessService, RepositoryScanner, Models)
├── LGMS.Desktop/     # Native WPF Desktop Application (.NET 9 Windows with Modern Dark UI)
└── LGMS/             # ASP.NET Core MVC 9 Web Application
```

### Shared Logic Guarantee
- Both the **Desktop App** and **Web App** share `100%` of the same core business logic, safe process invocation, repository discovery, staging, committing, stashing, branch management, and terminal execution from `LGMS.Core`.

---

## 2. Desktop Application Features (`LGMS.Desktop`)

- **Modern Dark UI**: Sleek palette inspired by GitHub Desktop and GitKraken with smooth controls and responsive layouts.
- **Instant Workspace Scanner**: Recursively scans folders for local Git repositories with clean/dirty indicators, ahead/behind pills (`↑2 ↓1`), and branch badges.
- **Working Tree & Staging Manager**:
  - Unstaged & Staged file listings with individual and batch toggle checkboxes.
  - One-click "Stage All", "Unstage All", and safe file discard with confirmation prompts.
- **Syntax Colored Diff Viewer**:
  - Line-by-line diff inspector highlighting additions (`+ green`), deletions (`- red`), and hunks (`@@ cyan`).
- **Commit & Push Flow**:
  - Conventional commit subject & multi-line description editor.
  - "Commit" and "Commit & Push" instant action buttons.
- **Commit History & Log Inspector**:
  - Formatted commit table with hash, author, relative time, subject, and detailed patch viewer.
- **Branch Management**:
  - View local and remote branches, switch branches, create new branches, and delete branches.
- **Stash Management**:
  - Save stashes with custom messages, apply, pop, and drop stashes.
- **Remote Repositories**:
  - Inspect fetch & push URLs, add new remotes, and remove remotes.
- **Safe Sandboxed Git Console**:
  - Execute Git commands with real-time output and live repository status synchronization.
- **External Tool Integration**:
  - Direct launch shortcuts to open active repository in **File Explorer**, **Visual Studio Code**, or **PowerShell**.

---

## 3. How to Run

### Run the Native Desktop App:
```bash
cd LGMS
dotnet run --project LGMS.Desktop/LGMS.Desktop.csproj
```

### Run the ASP.NET Core MVC Web App:
```bash
cd LGMS
dotnet run --project LGMS.csproj
```
Open browser at `http://localhost:5000` (or `https://localhost:5001`).

### Build Entire Solution:
```bash
cd LGMS
dotnet build LGMS.sln
```
