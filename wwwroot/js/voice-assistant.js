/**
 * LGMS Voice AI Assistant Controller
 * Real-time Speech-to-Text, Audio Waveform Visualizer, Step-by-Step HUD,
 * Multi-Repository Context Switching, and Text-to-Speech Audio Replay.
 */
const VoiceAssistant = {
    // State
    isListening: false,
    isSpeaking: false,
    isProcessing: false,
    audioMuted: false,
    recognition: null,
    audioContext: null,
    analyser: null,
    dataArray: null,
    mediaStream: null,
    animationFrameId: null,
    activeRepo: null,
    repositories: [],

    // Init
    init() {
        this.bindShortcuts();
        this.loadContext();
        this.initSpeechRecognition();
        this.initAudioVoices();
    },

    bindShortcuts() {
        // Global shortcut: Ctrl+Space to toggle Voice Assistant
        document.addEventListener('keydown', (e) => {
            if ((e.ctrlKey && e.code === 'Space') || (e.altKey && e.key.toLowerCase() === 'v')) {
                e.preventDefault();
                this.toggleModal();
            }
        });
    },

    async loadContext() {
        try {
            const res = await fetch('/api/voice/context');
            if (!res.ok) return;
            const data = await res.json();
            if (data.success) {
                this.repositories = data.repositories || [];
                this.activeRepo = data.activeRepo || null;
                this.renderRepoSelector();
                this.updateActiveRepoBadge();
            }
        } catch (err) {
            console.warn('[VoiceAssistant] Could not load context:', err);
        }
    },

    initSpeechRecognition() {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            console.warn('[VoiceAssistant] Web Speech API not supported in this browser.');
            return;
        }

        const recognition = new SpeechRecognition();
        recognition.continuous = false;
        recognition.interimResults = true;
        recognition.lang = 'en-US';

        recognition.onstart = () => {
            this.isListening = true;
            this.updateOrbState('listening');
            this.setStatusText('Listening... speak your command');
            this.startAudioVisualizer();
        };

        recognition.onresult = (event) => {
            let interimTranscript = '';
            let finalTranscript = '';

            for (let i = event.resultIndex; i < event.results.length; ++i) {
                if (event.results[i].isFinal) {
                    finalTranscript += event.results[i][0].transcript;
                } else {
                    interimTranscript += event.results[i][0].transcript;
                }
            }

            const transcriptEl = document.getElementById('voice-transcript-text');
            if (transcriptEl) {
                transcriptEl.innerHTML = (finalTranscript || interimTranscript) || '<em>Listening...</em>';
            }

            if (finalTranscript.trim()) {
                this.stopListening();
                this.executeVoiceCommand(finalTranscript.trim());
            }
        };

        recognition.onerror = (event) => {
            console.error('[VoiceAssistant] Speech recognition error:', event.error);
            this.isListening = false;
            this.stopAudioVisualizer();
            this.updateOrbState('idle');

            if (event.error === 'not-allowed') {
                this.setStatusText('Microphone permission denied. Please allow microphone access in your browser.');
            } else if (event.error === 'no-speech') {
                this.setStatusText('No speech detected. Click the mic to try again.');
            } else {
                this.setStatusText('Error: ' + event.error);
            }
        };

        recognition.onend = () => {
            if (this.isListening) {
                this.isListening = false;
                this.stopAudioVisualizer();
                if (!this.isProcessing) {
                    this.updateOrbState('idle');
                    this.setStatusText('Click microphone or press Ctrl+Space to speak');
                }
            }
        };

        this.recognition = recognition;
    },

    initAudioVoices() {
        if ('speechSynthesis' in window) {
            window.speechSynthesis.onvoiceschanged = () => {
                this.cachedVoices = window.speechSynthesis.getVoices();
            };
        }
    },

    toggleModal() {
        const modalEl = document.getElementById('voiceAssistantModal');
        if (!modalEl) return;

        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        if (modalEl.classList.contains('show')) {
            modal.hide();
            this.stopListening();
            this.stopSpeaking();
        } else {
            modal.show();
            this.loadContext();
            setTimeout(() => {
                this.startListening();
            }, 300);
        }
    },

    toggleListening() {
        if (this.isListening) {
            this.stopListening();
        } else {
            this.stopSpeaking();
            this.startListening();
        }
    },

    startListening() {
        if (!this.recognition) {
            LGMS.toast('Speech recognition is not supported in this browser. Please use Chrome or Edge.', 'warning');
            return;
        }

        try {
            this.isProcessing = false;
            const transcriptEl = document.getElementById('voice-transcript-text');
            if (transcriptEl) transcriptEl.innerHTML = '<span class="text-muted">Listening for command...</span>';
            this.recognition.start();
        } catch (e) {
            // Might already be active
            console.warn('[VoiceAssistant] Recognition start error:', e);
        }
    },

    stopListening() {
        if (this.recognition && this.isListening) {
            try {
                this.recognition.stop();
            } catch (e) { }
        }
        this.isListening = false;
        this.stopAudioVisualizer();
        this.updateOrbState(this.isProcessing ? 'processing' : 'idle');
    },

    async startAudioVisualizer() {
        try {
            if (!this.mediaStream) {
                this.mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            }
            if (!this.audioContext) {
                const AudioCtx = window.AudioContext || window.webkitAudioContext;
                this.audioContext = new AudioCtx();
            }

            if (this.audioContext.state === 'suspended') {
                await this.audioContext.resume();
            }

            const source = this.audioContext.createMediaStreamSource(this.mediaStream);
            this.analyser = this.audioContext.createAnalyser();
            this.analyser.fftSize = 64;
            source.connect(this.analyser);

            this.dataArray = new Uint8Array(this.analyser.frequencyBinCount);
            this.drawWaveform();
        } catch (err) {
            console.warn('[VoiceAssistant] Audio visualizer mic access failed:', err);
        }
    },

    stopAudioVisualizer() {
        if (this.animationFrameId) {
            cancelAnimationFrame(this.animationFrameId);
            this.animationFrameId = null;
        }
        const canvas = document.getElementById('voice-wave-canvas');
        if (canvas) {
            const ctx = canvas.getContext('2d');
            ctx.clearRect(0, 0, canvas.width, canvas.height);
        }
    },

    drawWaveform() {
        const canvas = document.getElementById('voice-wave-canvas');
        if (!canvas || !this.analyser) return;

        const ctx = canvas.getContext('2d');
        const bufferLength = this.analyser.frequencyBinCount;
        this.analyser.getByteFrequencyData(this.dataArray);

        ctx.clearRect(0, 0, canvas.width, canvas.height);

        const barWidth = (canvas.width / bufferLength) * 1.5;
        let x = 0;

        for (let i = 0; i < bufferLength; i++) {
            const barHeight = (this.dataArray[i] / 255) * (canvas.height * 0.85);

            // Neon cyan to purple gradient
            const gradient = ctx.createLinearGradient(0, canvas.height, 0, 0);
            gradient.addColorStop(0, '#06b6d4');
            gradient.addColorStop(1, '#a855f7');

            ctx.fillStyle = gradient;
            ctx.beginPath();
            ctx.roundRect(x, (canvas.height - barHeight) / 2, barWidth - 2, Math.max(barHeight, 4), [4]);
            ctx.fill();

            x += barWidth;
        }

        if (this.isListening || this.isSpeaking) {
            this.animationFrameId = requestAnimationFrame(() => this.drawWaveform());
        }
    },

    async executeVoiceCommand(command) {
        if (!command) return;

        this.isProcessing = true;
        this.updateOrbState('processing');
        this.setStatusText('Thinking & Executing...');
        this.showWorkingSection();

        // Render initial working step
        this.renderSteps([
            { title: 'Command Received', description: `"${command}"`, status: 'completed' },
            { title: 'Resolving Intent & Context', description: 'Analyzing command against repository catalog...', status: 'running' }
        ]);

        try {
            const payload = {
                command: command,
                targetRepoPath: this.activeRepo ? this.activeRepo.path : null
            };

            const response = await fetch('/api/voice/process', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            const data = await response.json();
            this.isProcessing = false;

            if (data.repository) {
                this.activeRepo = data.repository;
                this.updateActiveRepoBadge();
                this.syncDropdown();
            }

            // Update steps in HUD
            this.renderSteps(data.steps || []);

            // Raw terminal output
            this.renderRawOutput(data.rawOutput);

            // Spoken Reply
            if (data.spokenReply) {
                this.renderSpokenReply(data.spokenReply);
                this.speak(data.spokenReply);
            } else {
                this.updateOrbState('idle');
                this.setStatusText('Ready for next command');
            }

        } catch (err) {
            console.error('[VoiceAssistant] Execution error:', err);
            this.isProcessing = false;
            this.updateOrbState('error');
            this.setStatusText('Command execution failed');
            this.renderSteps([
                { title: 'Network / Server Error', description: err.message, status: 'failed' }
            ]);
            this.speak("An error occurred while executing the command. Please check server logs.");
        }
    },

    speak(text) {
        if (this.audioMuted || !('speechSynthesis' in window) || !text) {
            this.updateOrbState('idle');
            this.setStatusText('Ready for next command');
            return;
        }

        window.speechSynthesis.cancel();

        const utterance = new SpeechSynthesisUtterance(text);
        utterance.rate = 1.05;
        utterance.pitch = 1.0;

        // Pick preferred modern voice
        const voices = window.speechSynthesis.getVoices();
        const preferred = voices.find(v => v.lang.startsWith('en') && (v.name.includes('Natural') || v.name.includes('Google') || v.name.includes('Jenny') || v.name.includes('David')));
        if (preferred) utterance.voice = preferred;

        utterance.onstart = () => {
            this.isSpeaking = true;
            this.updateOrbState('speaking');
            this.setStatusText('Speaking response...');
        };

        utterance.onend = () => {
            this.isSpeaking = false;
            this.updateOrbState('idle');
            this.setStatusText('Ready for next command');
            this.stopAudioVisualizer();
        };

        utterance.onerror = () => {
            this.isSpeaking = false;
            this.updateOrbState('idle');
            this.setStatusText('Ready for next command');
        };

        window.speechSynthesis.speak(utterance);
    },

    stopSpeaking() {
        if ('speechSynthesis' in window) {
            window.speechSynthesis.cancel();
        }
        this.isSpeaking = false;
    },

    toggleMute() {
        this.audioMuted = !this.audioMuted;
        if (this.audioMuted) this.stopSpeaking();

        const muteBtn = document.getElementById('voice-mute-btn');
        if (muteBtn) {
            muteBtn.innerHTML = this.audioMuted
                ? '<i class="bi bi-volume-mute-fill text-danger"></i>'
                : '<i class="bi bi-volume-up-fill text-info"></i>';
            muteBtn.title = this.audioMuted ? 'Unmute Audio Reply' : 'Mute Audio Reply';
        }
        LGMS.toast(this.audioMuted ? 'Voice Audio Muted' : 'Voice Audio Enabled', 'info');
    },

    // UI Renderers
    updateOrbState(state) {
        const orb = document.getElementById('voice-ai-orb');
        if (!orb) return;

        orb.className = 'voice-orb';
        orb.classList.add(`orb-${state}`);
    },

    setStatusText(text) {
        const statusEl = document.getElementById('voice-status-label');
        if (statusEl) statusEl.textContent = text;
    },

    showWorkingSection() {
        const section = document.getElementById('voice-working-section');
        if (section) section.classList.remove('d-none');
    },

    renderSteps(steps) {
        const container = document.getElementById('voice-steps-container');
        if (!container) return;

        container.innerHTML = steps.map((s, idx) => {
            let badgeClass = 'bg-secondary';
            let icon = 'bi-circle';
            if (s.status === 'completed') {
                badgeClass = 'bg-success';
                icon = 'bi-check2-circle';
            } else if (s.status === 'running') {
                badgeClass = 'bg-primary text-white spinner-grow spinner-grow-sm';
                icon = '';
            } else if (s.status === 'failed') {
                badgeClass = 'bg-danger';
                icon = 'bi-x-circle';
            } else if (s.status === 'warning') {
                badgeClass = 'bg-warning text-dark';
                icon = 'bi-exclamation-triangle';
            }

            return `
                <div class="voice-step-card p-2 mb-2 rounded bg-dark-subtle border border-secondary-subtle transition">
                    <div class="d-flex align-items-center justify-content-between">
                        <div class="d-flex align-items-center gap-2">
                            <span class="badge ${badgeClass} p-1 d-inline-flex align-items-center justify-content-center" style="width: 22px; height: 22px;">
                                ${icon ? `<i class="bi ${icon} fs-xs"></i>` : ''}
                            </span>
                            <strong class="fs-sm">${s.title}</strong>
                        </div>
                        <span class="badge text-uppercase fs-xs ${s.status === 'completed' ? 'text-success' : s.status === 'running' ? 'text-primary' : 'text-muted'}">${s.status}</span>
                    </div>
                    ${s.description ? `<p class="fs-xs text-muted mb-0 mt-1 ps-4">${s.description}</p>` : ''}
                    ${s.output ? `<pre class="voice-step-output mt-1 mb-0 ps-4 text-info fs-xs">${s.output}</pre>` : ''}
                </div>
            `;
        }).join('');
    },

    renderRawOutput(output) {
        const drawer = document.getElementById('voice-raw-output-drawer');
        const content = document.getElementById('voice-raw-output-content');
        if (!drawer || !content) return;

        if (output && output.trim()) {
            content.textContent = output;
            drawer.classList.remove('d-none');
        } else {
            drawer.classList.add('d-none');
        }
    },

    renderSpokenReply(text) {
        const replyEl = document.getElementById('voice-spoken-reply');
        const textEl = document.getElementById('voice-spoken-reply-text');
        if (!replyEl || !textEl) return;

        textEl.textContent = text;
        replyEl.classList.remove('d-none');
    },

    renderRepoSelector() {
        const select = document.getElementById('voice-repo-select');
        if (!select) return;

        select.innerHTML = this.repositories.map(r => {
            const isSelected = this.activeRepo && (this.activeRepo.id === r.id || this.activeRepo.path === r.path);
            return `<option value="${r.path}" ${isSelected ? 'selected' : ''}>📂 ${r.name} (${r.currentBranch || 'detached'})</option>`;
        }).join('');

        select.onchange = (e) => {
            const selectedPath = e.target.value;
            const found = this.repositories.find(r => r.path === selectedPath);
            if (found) {
                this.activeRepo = found;
                this.updateActiveRepoBadge();
                this.executeVoiceCommand(`select repository ${found.name}`);
            }
        };
    },

    syncDropdown() {
        const select = document.getElementById('voice-repo-select');
        if (select && this.activeRepo) {
            select.value = this.activeRepo.path;
        }
    },

    updateActiveRepoBadge() {
        const badge = document.getElementById('voice-active-repo-badge');
        if (badge && this.activeRepo) {
            badge.innerHTML = `<i class="bi bi-folder2-open text-primary me-1"></i><strong>${this.activeRepo.name}</strong> <span class="text-muted">(${this.activeRepo.currentBranch || 'main'})</span>`;
        }

        // Also update the main topbar repo info badge if present
        const topbarInfo = document.getElementById('topbar-repo-info');
        if (topbarInfo && this.activeRepo) {
            topbarInfo.innerHTML = `
                <span class="badge repo-pill"><i class="bi bi-folder2-open me-1"></i> ${this.activeRepo.name}</span>
                <span class="text-muted">/</span>
                <span class="badge branch-pill"><i class="bi bi-bezier2 me-1"></i> ${this.activeRepo.currentBranch || 'master'}</span>
                ${this.activeRepo.isClean
                    ? '<span class="badge bg-success-subtle text-success border border-success-subtle"><i class="bi bi-check-circle me-1"></i> clean</span>'
                    : `<span class="badge bg-warning text-dark"><i class="bi bi-circle-fill fs-xs me-1"></i> ${this.activeRepo.changesCount || 0} changes</span>`
                }
            `;
        }
    },

    submitQuickCommand(cmd) {
        const transcriptEl = document.getElementById('voice-transcript-text');
        if (transcriptEl) transcriptEl.textContent = cmd;
        this.executeVoiceCommand(cmd);
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    VoiceAssistant.init();
});
