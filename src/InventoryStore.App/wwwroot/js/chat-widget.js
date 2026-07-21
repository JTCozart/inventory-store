(function () {
    var CONV_KEY = 'it-ai-conversation-id';
    var OPEN_KEY = 'it-ai-open';

    var widget, toggle, panel, messagesEl, typingEl, form, input, notConfiguredEl, newBtn, closeBtn;
    var statusChecked = false;

    document.addEventListener('DOMContentLoaded', function () {
        widget = document.getElementById('ai-chat-widget');
        if (!widget) return; // module disabled — partial not rendered

        toggle          = document.getElementById('ai-chat-toggle');
        panel           = document.getElementById('ai-chat-panel');
        messagesEl      = document.getElementById('ai-chat-messages');
        typingEl        = document.getElementById('ai-chat-typing');
        form            = document.getElementById('ai-chat-form');
        input           = document.getElementById('ai-chat-text');
        notConfiguredEl = document.getElementById('ai-chat-not-configured');
        newBtn          = document.getElementById('ai-chat-new');
        closeBtn        = document.getElementById('ai-chat-close');

        toggle.addEventListener('click', togglePanel);
        closeBtn.addEventListener('click', function () { setOpen(false); });
        newBtn.addEventListener('click', startNewConversation);
        form.addEventListener('submit', onSubmit);

        if (localStorage.getItem(OPEN_KEY) === '1') setOpen(true);
    });

    function togglePanel() {
        setOpen(panel.classList.contains('d-none'));
    }

    function setOpen(open) {
        panel.classList.toggle('d-none', !open);
        localStorage.setItem(OPEN_KEY, open ? '1' : '0');
        if (open) {
            checkStatus();
            var convId = localStorage.getItem(CONV_KEY);
            if (convId && messagesEl.children.length === 0) loadHistory(convId);
            input.focus();
        }
    }

    function startNewConversation() {
        localStorage.removeItem(CONV_KEY);
        messagesEl.innerHTML = '';
        input.focus();
    }

    function checkStatus() {
        fetch('/api/ai/chat/status', { credentials: 'include' })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                var configured = data.status === 'Ready';
                notConfiguredEl.classList.toggle('d-none', configured);
                form.classList.toggle('d-none', !configured);
                statusChecked = true;
            })
            .catch(function () { /* leave the form usable; /ask will report the real error */ });
    }

    function loadHistory(conversationId) {
        fetch('/api/ai/chat/history/' + encodeURIComponent(conversationId), { credentials: 'include' })
            .then(function (r) { if (!r.ok) throw new Error(); return r.json(); })
            .then(function (items) {
                items.forEach(function (m) {
                    appendMessage(m.role === 0 ? 'user' : 'assistant', m.content);
                });
                scrollToBottom();
            })
            .catch(function () { localStorage.removeItem(CONV_KEY); });
    }

    function onSubmit(e) {
        e.preventDefault();
        var text = (input.value || '').trim();
        if (!text) return;

        appendMessage('user', text);
        input.value = '';
        scrollToBottom();
        typingEl.classList.remove('d-none');
        form.querySelector('button').disabled = true;

        var conversationId = localStorage.getItem(CONV_KEY);
        fetch('/api/ai/chat/ask', {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                conversationId: conversationId ? parseInt(conversationId, 10) : null,
                message: text
            })
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data.conversationId) localStorage.setItem(CONV_KEY, data.conversationId);
                appendMessage('assistant', data.message || "Sorry, I couldn't answer that.");
                scrollToBottom();
                if (data.openUrl) {
                    localStorage.setItem(OPEN_KEY, '1'); // keep the panel open across the navigation
                    setTimeout(function () { window.location.assign(data.openUrl); }, 500);
                }
            })
            .catch(function () {
                appendMessage('assistant', "The AI assistant hit a problem answering that — please try again.");
                scrollToBottom();
            })
            .finally(function () {
                typingEl.classList.add('d-none');
                form.querySelector('button').disabled = false;
                input.focus();
            });
    }

    function appendMessage(role, content) {
        var row = document.createElement('div');
        row.className = 'ai-chat-msg ai-chat-msg-' + role;
        row.innerHTML = renderMarkdownLite(content);
        messagesEl.appendChild(row);
    }

    function scrollToBottom() {
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }

    // Minimal, safe markdown: escapes everything first (model output is untrusted text),
    // then re-introduces a small set of formatting marks.
    function renderMarkdownLite(text) {
        var esc = String(text)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
        esc = esc
            .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
            .replace(/\*(.+?)\*/g, '<em>$1</em>')
            .replace(/`(.+?)`/g, '<code>$1</code>')
            .replace(/\n/g, '<br>');
        return esc;
    }
})();
