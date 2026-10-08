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
