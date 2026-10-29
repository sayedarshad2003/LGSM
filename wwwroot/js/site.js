const LGMS = {
    toast(message, type = 'info') {
        const toastEl = document.getElementById('lgmsToast');
        if (!toastEl) return;

        const textEl = document.getElementById('toastText');
        const iconEl = document.getElementById('toastIcon');

        textEl.textContent = message;
        toastEl.className = 'toast align-items-center text-white border-0 shadow-lg';

        if (type === 'success') {
            toastEl.classList.add('bg-success');
            iconEl.className = 'bi bi-check-circle-fill fs-5';
        } else if (type === 'error') {
            toastEl.classList.add('bg-danger');
            iconEl.className = 'bi bi-x-circle-fill fs-5';
        } else if (type === 'warning') {
            toastEl.classList.add('bg-warning', 'text-dark');
            iconEl.className = 'bi bi-exclamation-triangle-fill fs-5';
        } else {
            toastEl.classList.add('bg-primary');
            iconEl.className = 'bi bi-info-circle-fill fs-5';
        }

        const toast = new bootstrap.Toast(toastEl, { delay: 4000 });
        toast.show();
    },

    showLoading(title = 'Executing Git Command...', subtitle = 'Please wait while the operation completes') {
        const overlay = document.getElementById('lgms-loading-overlay');
        if (!overlay) return;
        document.getElementById('loading-title').innerText = title;
        document.getElementById('loading-subtitle').innerText = subtitle;
        overlay.classList.remove('d-none');
    },

    hideLoading() {
        const overlay = document.getElementById('lgms-loading-overlay');
        if (overlay) overlay.classList.add('d-none');
    },

    applyTheme(theme) {
        let activeTheme = theme;
        if (theme === 'system') {
            activeTheme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        }

        document.documentElement.setAttribute('data-theme', activeTheme);
        const darkCss = document.getElementById('theme-dark-css');
        const lightCss = document.getElementById('theme-light-css');

        if (darkCss && lightCss) {
            darkCss.disabled = (activeTheme === 'light');
            lightCss.disabled = (activeTheme === 'dark');
        }

        const themeIcon = document.getElementById('theme-icon');
        if (themeIcon) {
            themeIcon.className = activeTheme === 'dark' ? 'bi bi-moon-stars' : 'bi bi-sun';
        }

        localStorage.setItem('lgms_theme', theme);
    },

    toggleTheme() {
        const current = localStorage.getItem('lgms_theme') || 'dark';
        const next = current === 'dark' ? 'light' : 'dark';
        this.applyTheme(next);
    },

    openCloneModal(defaultFolder = '') {
        const modalEl = document.getElementById('cloneRepoGlobalModal');
        if (!modalEl) return;
        const destInput = document.getElementById('cloneRepoDestInput');
        const urlInput = document.getElementById('cloneRepoUrlInput');
        const errAlert = document.getElementById('cloneRepoErrorAlert');
        if (errAlert) errAlert.classList.add('d-none');
        if (urlInput) urlInput.value = '';
        if (destInput) {
            destInput.value = defaultFolder || '';
        }
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();
    },

    async browseFolderForClone() {
        const btn = document.getElementById('btn-browse-clone-dest');
        const destInput = document.getElementById('cloneRepoDestInput');
        if (btn) btn.disabled = true;
        this.toast("Opening folder browser...", "info");
        try {
            const res = await fetch('/api/workspace/browse-native', { method: 'POST' });
            const data = await res.json();
            if (data.success && data.folder) {
                if (destInput) destInput.value = data.folder;
                this.toast(`✓ Selected folder: ${data.folder}`, "success");
            } else if (data.message && data.message !== "No folder selected or dialog cancelled.") {
                this.toast(data.message, "warning");
            }
        } catch (e) {
            this.toast("Failed to open folder browser.", "error");
        } finally {
            if (btn) btn.disabled = false;
        }
    },

    async submitCloneRepo(e) {
        if (e) e.preventDefault();
        const destInput = document.getElementById('cloneRepoDestInput');
        const urlInput = document.getElementById('cloneRepoUrlInput');
        const errAlert = document.getElementById('cloneRepoErrorAlert');
        const submitBtn = document.getElementById('btn-submit-clone');

        const dest = destInput ? destInput.value.trim() : '';
        const url = urlInput ? urlInput.value.trim() : '';

        if (!dest) {
            if (errAlert) {
                errAlert.textContent = "Please select or enter the destination folder first.";
                errAlert.classList.remove('d-none');
            }
            return;
        }

        if (!url) {
            if (errAlert) {
                errAlert.textContent = "Please enter the remote repository URL.";
                errAlert.classList.remove('d-none');
            }
            return;
        }

        if (errAlert) errAlert.classList.add('d-none');
        if (submitBtn) submitBtn.disabled = true;
        this.showLoading("Cloning Remote Repository...", `Cloning ${url} into ${dest}...`);

        try {
            const res = await fetch('/api/repository/clone', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ repositoryUrl: url, destinationPath: dest })
            });
            const data = await res.json();
            this.hideLoading();

            if (data.success) {
                this.toast(data.message || "Repository cloned successfully!", "success");
                const modalEl = document.getElementById('cloneRepoGlobalModal');
                if (modalEl) {
                    const modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();
                }
                setTimeout(() => {
                    if (data.repositoryId && data.repositoryPath) {
                        window.location.href = `/repository/${data.repositoryId}?path=${encodeURIComponent(data.repositoryPath)}`;
                    } else {
                        window.location.href = '/repositories';
                    }
                }, 1000);
            } else {
                if (errAlert) {
                    errAlert.textContent = data.message || "Clone failed.";
                    errAlert.classList.remove('d-none');
                }
                this.toast(data.message || "Clone failed", "error");
            }
        } catch (err) {
            this.hideLoading();
            if (errAlert) {
                errAlert.textContent = `Clone error: ${err.message}`;
                errAlert.classList.remove('d-none');
            }
            this.toast(`Clone error: ${err.message}`, "error");
        } finally {
            if (submitBtn) submitBtn.disabled = false;
        }
    },

    openInitModal(defaultFolder = '') {
        const modalEl = document.getElementById('initProjectGlobalModal');
        if (!modalEl) return;
        const folderInput = document.getElementById('initProjectFolderInput');
        const commitMsgInput = document.getElementById('initProjectCommitMsg');
        const errAlert = document.getElementById('initProjectErrorAlert');
        if (errAlert) errAlert.classList.add('d-none');
        if (folderInput) {
            folderInput.value = defaultFolder || '';
        }
        if (commitMsgInput) {
            commitMsgInput.value = "Initial Commit";
        }
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();
    },

    async browseFolderForInit() {
        const btn = document.getElementById('btn-browse-init-folder');
        const folderInput = document.getElementById('initProjectFolderInput');
        if (btn) btn.disabled = true;
        this.toast("Opening folder browser...", "info");
        try {
            const res = await fetch('/api/workspace/browse-native', { method: 'POST' });
            const data = await res.json();
            if (data.success && data.folder) {
                if (folderInput) folderInput.value = data.folder;
                this.toast(`✓ Selected folder: ${data.folder}`, "success");
            } else if (data.message && data.message !== "No folder selected or dialog cancelled.") {
                this.toast(data.message, "warning");
            }
        } catch (e) {
            this.toast("Failed to open folder browser.", "error");
        } finally {
            if (btn) btn.disabled = false;
        }
    },

    async submitInitProject(e) {
        if (e) e.preventDefault();
        const folderInput = document.getElementById('initProjectFolderInput');
        const commitMsgInput = document.getElementById('initProjectCommitMsg');
        const errAlert = document.getElementById('initProjectErrorAlert');
        const submitBtn = document.getElementById('btn-submit-init');

        const folder = folderInput ? folderInput.value.trim() : '';
        const commitMsg = commitMsgInput ? commitMsgInput.value.trim() : 'Initial Commit';

        if (!folder) {
            if (errAlert) {
                errAlert.textContent = "Please select or enter the project folder to initialize.";
                errAlert.classList.remove('d-none');
            }
            return;
        }

        if (errAlert) errAlert.classList.add('d-none');
        if (submitBtn) submitBtn.disabled = true;
        this.showLoading("Initializing Git Repository...", `Running git init and creating '${commitMsg}' in ${folder}...`);

        try {
            const res = await fetch('/api/repository/init', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ path: folder, commitMessage: commitMsg })
            });
            const data = await res.json();
            this.hideLoading();

            if (data.success) {
                this.toast(data.message || "Project initialized and committed successfully!", "success");
                const modalEl = document.getElementById('initProjectGlobalModal');
                if (modalEl) {
                    const modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();
                }
                setTimeout(() => {
                    if (data.repositoryId && data.repositoryPath) {
                        window.location.href = `/repository/${data.repositoryId}?path=${encodeURIComponent(data.repositoryPath)}`;
                    } else {
                        window.location.href = '/repositories';
                    }
                }, 1000);
            } else {
                if (errAlert) {
                    errAlert.textContent = data.message || "Git initialization failed.";
                    errAlert.classList.remove('d-none');
                }
                this.toast(data.message || "Git initialization failed", "error");
            }
        } catch (err) {
            this.hideLoading();
            if (errAlert) {
                errAlert.textContent = `Initialization error: ${err.message}`;
                errAlert.classList.remove('d-none');
            }
            this.toast(`Initialization error: ${err.message}`, "error");
        } finally {
            if (submitBtn) submitBtn.disabled = false;
        }
    }
};

document.addEventListener('DOMContentLoaded', () => {
    // Initialize theme from storage
    const savedTheme = localStorage.getItem('lgms_theme') || 'dark';
    LGMS.applyTheme(savedTheme);

    // Sidebar toggle for mobile
    const toggleBtn = document.getElementById('sidebar-toggle');
    const sidebar = document.getElementById('lgms-sidebar');
    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener('click', () => {
            sidebar.classList.toggle('open');
        });
    }
});
