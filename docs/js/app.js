/* ==========================================================================
   LGMS - Local Git Management System
   Enterprise Application Script & Theme Controller
   ========================================================================== */

document.addEventListener('DOMContentLoaded', () => {
  initThemeEngine();
  initMobileNavigation();
  initWorkflowTabs();
  initPlaygroundTabs();
  initAccountSwitcherSimulator();
  initAiCommitSimulator();
  initDiffViewerMode();
  initTerminalSimulator();
  initFaqAccordion();
  initClipboardActions();
  initScrollSpy();
});

/* --------------------------------------------------------------------------
   Theme Engine (Dark & Light Theme Toggle)
   -------------------------------------------------------------------------- */
function initThemeEngine() {
  const themeToggleBtn = document.getElementById('theme-toggle-btn');
  const themeIcon = document.getElementById('theme-icon');

  // Detect stored or preferred theme
  const savedTheme = localStorage.getItem('lgms-theme');
  const prefersLight = window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches;
  const initialTheme = savedTheme ? savedTheme : (prefersLight ? 'light' : 'dark');

  applyTheme(initialTheme);

  if (themeToggleBtn) {
    themeToggleBtn.addEventListener('click', () => {
      const currentTheme = document.documentElement.getAttribute('data-theme') || 'dark';
      const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
      applyTheme(newTheme);
      showToast(`Switched to ${newTheme === 'dark' ? 'Dark' : 'Light'} theme`);
    });
  }

  function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('lgms-theme', theme);

    if (themeIcon) {
      if (theme === 'light') {
        themeIcon.className = 'fa-solid fa-moon';
        themeToggleBtn.setAttribute('title', 'Switch to Dark Mode');
      } else {
        themeIcon.className = 'fa-solid fa-sun';
        themeToggleBtn.setAttribute('title', 'Switch to Light Mode');
      }
    }
  }
}

/* --------------------------------------------------------------------------
   Mobile Navigation
   -------------------------------------------------------------------------- */
function initMobileNavigation() {
  const toggleBtn = document.querySelector('.mobile-toggle');
  const navLinks = document.querySelector('.nav-links');

  if (toggleBtn && navLinks) {
    toggleBtn.addEventListener('click', () => {
      navLinks.classList.toggle('open');
      const isOpen = navLinks.classList.contains('open');
      toggleBtn.setAttribute('aria-expanded', isOpen);
    });

    navLinks.querySelectorAll('.nav-link').forEach(link => {
      link.addEventListener('click', () => {
        navLinks.classList.remove('open');
      });
    });
  }
}

/* --------------------------------------------------------------------------
   Workflow Tabs
   -------------------------------------------------------------------------- */
function initWorkflowTabs() {
  const tabBtns = document.querySelectorAll('.workflow-tab-btn');
  const panes = document.querySelectorAll('.workflow-pane');

  tabBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      const step = btn.getAttribute('data-step');

      tabBtns.forEach(b => b.classList.remove('active'));
      panes.forEach(p => p.classList.remove('active'));

      btn.classList.add('active');
      const targetPane = document.getElementById(`step-pane-${step}`);
      if (targetPane) {
        targetPane.classList.add('active');
      }
    });
  });
}

/* --------------------------------------------------------------------------
   Playground Simulator Navigation
   -------------------------------------------------------------------------- */
function initPlaygroundTabs() {
  const btns = document.querySelectorAll('.playground-btn');
  const panels = document.querySelectorAll('.playground-panel');

  btns.forEach(btn => {
    btn.addEventListener('click', () => {
      const targetId = btn.getAttribute('data-target');

      btns.forEach(b => b.classList.remove('active'));
      panels.forEach(p => p.classList.remove('active'));

      btn.classList.add('active');
      const panel = document.getElementById(targetId);
      if (panel) {
        panel.classList.add('active');
      }
    });
  });
}

/* --------------------------------------------------------------------------
   Simulator 1: Multi-Account Switcher
   -------------------------------------------------------------------------- */
function initAccountSwitcherSimulator() {
  const accountItems = document.querySelectorAll('.sim-account-item');
  const activeUser = document.getElementById('demo-active-user');
  const activeHost = document.getElementById('demo-active-host');
  const activeHelper = document.getElementById('demo-active-helper');
  const activeLock = document.getElementById('demo-lock-status');
  const accountLog = document.getElementById('demo-account-log');

  const accounts = {
    'sayedarshad2003': {
      user: 'sayedarshad2003',
      name: 'Sayed Arshad',
      org: 'Personal Account',
      host: 'github.com',
      helper: 'Windows Credential Helper (Active)',
      authorStatus: 'Commit identity preserved'
    },
    'work-org': {
      user: 'work-developer',
      name: 'Work Developer',
      org: 'Enterprise SSO Organization',
      host: 'github.com',
      helper: 'Windows Credential Helper (Active)',
      authorStatus: 'Commit identity preserved'
    }
  };

  accountItems.forEach(item => {
    item.addEventListener('click', () => {
      accountItems.forEach(i => i.classList.remove('active'));
      item.classList.add('active');

      const key = item.getAttribute('data-account');
      const acc = accounts[key];

      if (acc && activeUser) {
        activeUser.textContent = acc.user;
        activeHost.textContent = acc.host;
        activeHelper.textContent = acc.helper;
        
        if (activeLock) {
          activeLock.innerHTML = '<span style="color: var(--accent-success);"><i class="fa-solid fa-lock"></i> Mutex Lock Acquired</span>';
        }

        if (accountLog) {
          const timestamp = new Date().toLocaleTimeString();
          accountLog.innerHTML = `
            <div><span class="c-dim">[${timestamp}]</span> Acquiring GitAccountOperationLock...</div>
            <div><span class="c-keyword">SUCCESS:</span> Lock acquired. Invoking: <span class="c-cmd">gh auth switch --user ${acc.user}</span></div>
            <div><span class="c-arg">OK:</span> Active GitHub transport account set to <strong>${acc.user}</strong>.</div>
            <div><span class="c-dim">AUDIT:</span> Identity user.name and user.email remain unchanged. Ready for atomic operation.</div>
          `;
        }

        showToast(`Active account switched to @${acc.user}`);
      }
    });
  });
}

/* --------------------------------------------------------------------------
   Simulator 2: AI Commit Generator
   -------------------------------------------------------------------------- */
function initAiCommitSimulator() {
  const generateBtn = document.getElementById('ai-generate-btn');
  const providerSelect = document.getElementById('ai-provider-select');
  const diffSelect = document.getElementById('ai-diff-select');
  const outputSubject = document.getElementById('ai-output-subject');
  const outputBody = document.getElementById('ai-output-body');
  const diffDisplay = document.getElementById('ai-diff-display');

  const diffSamples = {
    'auth_lock': {
      diff: `+ builder.Services.AddSingleton<GitAccountOperationLock>();\n+ using (await _accountLock.AcquireAsync())\n+ {\n+     await _cli.SwitchAccountAsync(account.Username);\n+     await _git.PushAsync(repoPath);\n+ }`,
      deepseek: {
        subject: `feat(auth): serialize gh account switching via atomic mutex lock`,
        body: `- Prevent race conditions during concurrent push and remote operations\n- Wrap gh auth switch and git push in GitAccountOperationLock\n- Maintain commit author identity across transport credential switches`
      },
      gemini: {
        subject: `refactor(core): integrate GitAccountOperationLock for thread-safe multi-account git flow`,
        body: `Ensures all GitHub CLI auth commands and Git operations execute sequentially without token collisions or shared session corruption.`
      }
    },
    'diff_viewer': {
      diff: `+ <div class="syntax-diff-viewer">\n+   <span class="addition">+ public async Task<bool> CloneAsync()</span>\n+   <span class="deletion">- public void CloneLegacy()</span>\n+ </div>`,
      deepseek: {
        subject: `feat(ui): add syntax-highlighted diff viewer with side-by-side mode`,
        body: `- Display additions and deletions with custom theme tokens\n- Add support for hunk navigation and selective line discard`
      },
      gemini: {
        subject: `feat(diff): enhance diff viewer component with real-time token highlighting`,
        body: `Improves working tree review experience by rendering line-level additions, deletions, and hunk headers.`
      }
    },
    'deepseek_gemini': {
      diff: `+ builder.Services.AddScoped<DeepSeekCommitService>();\n+ builder.Services.AddScoped<GeminiCommitService>();\n+ builder.Services.AddScoped<IAICommitManager, AICommitManager>();`,
      deepseek: {
        subject: `feat(ai): integrate DeepSeek and Gemini dual-engine conventional commit generation`,
        body: `- Add IAICommitManager abstraction for configurable LLM providers\n- Feed staged git diff into structured prompt for standard commits\n- Add API key configuration in settings`
      },
      gemini: {
        subject: `feat(commits): provide smart commit generation using DeepSeek & Gemini API`,
        body: `Automates commit message authoring from staged diffs adhering strictly to Conventional Commit specifications.`
      }
    }
  };

  if (diffSelect && diffDisplay) {
    diffSelect.addEventListener('change', () => {
      const key = diffSelect.value;
      if (diffSamples[key]) {
        diffDisplay.textContent = diffSamples[key].diff;
      }
    });
  }

  if (generateBtn && outputSubject && outputBody) {
    generateBtn.addEventListener('click', () => {
      generateBtn.disabled = true;
      generateBtn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Generating...`;

      outputSubject.textContent = 'Analyzing staged git diff...';
      outputBody.textContent = 'Calling AI model provider...';

      const provider = providerSelect ? providerSelect.value : 'deepseek';
      const sampleKey = diffSelect ? diffSelect.value : 'auth_lock';
      const result = diffSamples[sampleKey][provider];

      setTimeout(() => {
        typewriter(outputSubject, result.subject, 15, () => {
          typewriter(outputBody, result.body, 10, () => {
            generateBtn.disabled = false;
            generateBtn.innerHTML = `<i class="fa-solid fa-wand-magic-sparkles"></i> Generate Smart Commit`;
            showToast('Conventional commit message generated');
          });
        });
      }, 500);
    });
  }
}

function typewriter(element, text, speed, callback) {
  element.textContent = '';
  let index = 0;
  function step() {
    if (index < text.length) {
      element.textContent += text.charAt(index);
      index++;
      setTimeout(step, speed);
    } else if (callback) {
      callback();
    }
  }
  step();
}

/* --------------------------------------------------------------------------
   Simulator 3: Diff Viewer Mode
   -------------------------------------------------------------------------- */
function initDiffViewerMode() {
  const toggleBtn = document.getElementById('diff-mode-toggle');
  const linesBox = document.getElementById('interactive-diff-lines');

  if (toggleBtn && linesBox) {
    toggleBtn.addEventListener('click', () => {
      const isSplit = linesBox.classList.toggle('split-mode');
      toggleBtn.innerHTML = isSplit 
        ? '<i class="fa-solid fa-columns"></i> View: Split' 
        : '<i class="fa-solid fa-bars"></i> View: Unified';
      showToast(isSplit ? 'Switched to Split View' : 'Switched to Unified View');
    });
  }
}

/* --------------------------------------------------------------------------
   Simulator 4: Terminal Simulator
   -------------------------------------------------------------------------- */
function initTerminalSimulator() {
  const screen = document.getElementById('terminal-screen');
  const btns = document.querySelectorAll('.term-btn');

  const responses = {
    'git status': `On branch main\nYour branch is up to date with 'origin/main'.\n\nChanges to be committed:\n  (use "git restore --staged <file>..." to unstage)\n\tmodified:   LGMS.Core/Services/GitAccountOperationLock.cs\n\tmodified:   LGMS.Desktop/Views/MainWindow.xaml\n\nstatus: clean working tree ready to commit`,
    'gh auth status': `github.com\n  ✓ Logged in to github.com account sayedarshad2003 (keyring)\n  - Active account: true\n  - Git protocol: https\n  - Token: ******************** (delegated to Windows Credential Helper)\n  - Scopes: 'gist', 'read:org', 'repo'`,
    'git branch -a': `* main\n  remotes/origin/HEAD -> origin/main\n  remotes/origin/main\n  remotes/origin/feature/ai-commit-assistant`,
    'git log -n 2': `commit 8f3d1e9 (HEAD -> main, origin/main)\nAuthor: Sayed Arshad <sayed@example.com>\nDate:   2026-10-19 18:30:00 +0530\n\n    feat(desktop): publish standalone single-file x64 executable\n\ncommit 2a7c4b1\nAuthor: Sayed Arshad <sayed@example.com>\nDate:   2026-10-19 16:15:00 +0530\n\n    feat(ai): integrate Gemini and DeepSeek commit generation`,
    'clear': '__CLEAR__'
  };

  btns.forEach(btn => {
    btn.addEventListener('click', () => {
      const cmd = btn.getAttribute('data-cmd');
      if (cmd && screen) {
        if (cmd === 'clear') {
          screen.innerHTML = `<div><span style="color: var(--accent-success);">lgms:~$</span> <span class="c-dim"># Terminal cleared. Choose a command below:</span></div>`;
          return;
        }

        const out = responses[cmd] || `Command '${cmd}' completed successfully.`;
        const item = document.createElement('div');
        item.style.marginTop = '0.65rem';
        item.innerHTML = `
          <div><span style="color: var(--accent-success);">lgms:~$</span> <span class="c-cmd">${cmd}</span></div>
          <pre style="margin-top: 0.25rem; font-family: inherit; color: #94a3b8; white-space: pre-wrap;">${out}</pre>
        `;
        screen.appendChild(item);
        screen.scrollTop = screen.scrollHeight;
      }
    });
  });
}

/* --------------------------------------------------------------------------
   FAQ Accordion
   -------------------------------------------------------------------------- */
function initFaqAccordion() {
  const blocks = document.querySelectorAll('.faq-block');

  blocks.forEach(block => {
    const btn = block.querySelector('.faq-btn');
    if (btn) {
      btn.addEventListener('click', () => {
        const isOpen = block.classList.contains('open');

        // Close other items
        blocks.forEach(b => {
          if (b !== block) b.classList.remove('open');
        });

        block.classList.toggle('open', !isOpen);
      });
    }
  });
}

/* --------------------------------------------------------------------------
   Clipboard Copy
   -------------------------------------------------------------------------- */
function initClipboardActions() {
  document.querySelectorAll('[data-copy]').forEach(btn => {
    btn.addEventListener('click', () => {
      const text = btn.getAttribute('data-copy');
      if (!text) return;

      navigator.clipboard.writeText(text).then(() => {
        const orig = btn.innerHTML;
        btn.innerHTML = `<i class="fa-solid fa-check"></i> Copied`;
        showToast(`Copied to clipboard`);
        setTimeout(() => {
          btn.innerHTML = orig;
        }, 2000);
      });
    });
  });
}

function showToast(msg) {
  let toast = document.getElementById('toast-notice');
  if (!toast) {
    toast = document.createElement('div');
    toast.id = 'toast-notice';
    toast.className = 'toast-bar';
    document.body.appendChild(toast);
  }

  toast.innerHTML = `<i class="fa-solid fa-circle-check" style="color: var(--accent-success);"></i> <span>${msg}</span>`;
  toast.classList.add('show');
  setTimeout(() => {
    toast.classList.remove('show');
  }, 2600);
}

/* --------------------------------------------------------------------------
   Scroll Spy
   -------------------------------------------------------------------------- */
function initScrollSpy() {
  const sections = document.querySelectorAll('section[id]');
  const links = document.querySelectorAll('.nav-link[href^="#"]');

  window.addEventListener('scroll', () => {
    let current = '';
    const pos = window.scrollY + 120;

    sections.forEach(sec => {
      if (pos >= sec.offsetTop && pos < sec.offsetTop + sec.clientHeight) {
        current = sec.getAttribute('id');
      }
    });

    links.forEach(l => {
      l.classList.remove('active');
      if (l.getAttribute('href') === `#${current}`) {
        l.classList.add('active');
      }
    });
  });
}
