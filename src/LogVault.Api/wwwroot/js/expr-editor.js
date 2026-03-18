/**
 * LvExprEditor — syntax-highlighted expression input with autocomplete.
 * Uses an <input type="text"> (native caret/Enter) with a mirror div for highlighting.
 * No external dependencies.
 */
(function () {
    'use strict';

    // ── Constants ─────────────────────────────────────────────────────────────

    const FIELDS = ['level', 'app', 'application', 'env', 'environment',
        'message', 'msg', 'exception', 'ex', 'trace', 'traceid',
        'timestamp', 'time', 'ts'];
    const LEVEL_FIELDS = new Set(['level']);
    const LEVEL_VALUES = ['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'];
    const LEVEL_OPS   = ['>=', '>', '<=', '<', '=='];
    const DEFAULT_OPS = ['==', '!=', 'contains'];

    // Symbol operators are self-delimiting — no \b needed.
    // 'contains' uses negative lookahead to avoid matching 'containsX'.
    const OP_REGEX    = /^(>=|<=|!=|>|<|==|contains(?!\w))/i;
    const FIELD_REGEX = new RegExp('^(' + FIELDS.join('|') + ')(?!\\w)', 'i');
    const PROP_REGEX  = /^prop:(\w*)/i;

    // Canonical display list — skip verbose aliases in suggestions
    const SUGGEST_FIELDS = ['level', 'app', 'env', 'message', 'exception',
        'trace', 'timestamp', 'prop:'];

    // ── Tokeniser ─────────────────────────────────────────────────────────────

    /**
     * @typedef {{ type: string, value: string, start: number, end: number, fname?: string }} Token
     * types: ws | kw | field | prop | op | str | level-value | unknown
     */
    function tokenize(text) {
        const tokens = [];
        let i = 0;
        // Carry last meaningful tokens for contextual classification
        let ctx = { fieldName: null, hasOp: false };

        while (i < text.length) {
            // Whitespace
            if (/\s/.test(text[i])) {
                let j = i;
                while (j < text.length && /\s/.test(text[j])) j++;
                tokens.push({ type: 'ws', value: text.slice(i, j), start: i, end: j });
                i = j;
                continue;
            }

            // AND keyword
            const andM = text.slice(i).match(/^AND(?!\w)/i);
            if (andM) {
                tokens.push({ type: 'kw', value: andM[0], start: i, end: i + andM[0].length });
                ctx = { fieldName: null, hasOp: false };
                i += andM[0].length;
                continue;
            }

            // prop:Key
            const propM = text.slice(i).match(PROP_REGEX);
            if (propM) {
                tokens.push({ type: 'prop', value: propM[0], start: i, end: i + propM[0].length });
                ctx = { fieldName: 'prop', hasOp: false };
                i += propM[0].length;
                continue;
            }

            // Known field name
            const fieldM = text.slice(i).match(FIELD_REGEX);
            if (fieldM) {
                const fname = fieldM[0].toLowerCase();
                tokens.push({ type: 'field', value: fieldM[0], start: i, end: i + fieldM[0].length, fname });
                ctx = { fieldName: fname, hasOp: false };
                i += fieldM[0].length;
                continue;
            }

            // Operator
            const opM = text.slice(i).match(OP_REGEX);
            if (opM) {
                tokens.push({ type: 'op', value: opM[0], start: i, end: i + opM[0].length });
                ctx = { fieldName: ctx.fieldName, hasOp: true };
                i += opM[0].length;
                continue;
            }

            // Quoted string
            if (text[i] === '"') {
                let j = i + 1;
                while (j < text.length && text[j] !== '"') {
                    if (text[j] === '\\') j++;
                    j++;
                }
                if (j < text.length) j++; // closing quote
                tokens.push({ type: 'str', value: text.slice(i, j), start: i, end: j });
                ctx = { fieldName: null, hasOp: false };
                i = j;
                continue;
            }

            // Bare word — level value if context is right, otherwise unknown
            let j = i;
            while (j < text.length && !/[\s"=!<>]/.test(text[j])) j++;
            const word = text.slice(i, j);
            if (!word) { i++; continue; }

            const isLevelVal = LEVEL_VALUES.some(v => v.toLowerCase() === word.toLowerCase());
            const type = (isLevelVal && LEVEL_FIELDS.has(ctx.fieldName) && ctx.hasOp)
                ? 'level-value' : 'unknown';

            tokens.push({ type, value: word, start: i, end: j });
            if (ctx.hasOp) ctx = { fieldName: null, hasOp: false }; // value consumed
            i = j;
        }
        return tokens;
    }

    // ── HTML renderer ─────────────────────────────────────────────────────────

    const ESC = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' };
    function escHtml(s) { return s.replace(/[&<>"]/g, c => ESC[c]); }

    const TOKEN_CLASS = {
        kw: 'lv-expr-kw', field: 'lv-expr-field', prop: 'lv-expr-prop',
        op: 'lv-expr-op', str: 'lv-expr-str', 'level-value': 'lv-expr-level',
        unknown: 'lv-expr-error',
    };

    function renderHighlight(tokens) {
        return tokens.map(t => {
            if (t.type === 'ws') return escHtml(t.value);
            const cls = TOKEN_CLASS[t.type];
            return cls ? `<span class="${cls}">${escHtml(t.value)}</span>` : escHtml(t.value);
        }).join('');
    }

    // ── Autocomplete context ──────────────────────────────────────────────────

    /**
     * Returns { suggestions: string[], insertFrom: number }
     */
    function getAutocompleteSuggestions(text, caretPos) {
        const slice = text.slice(0, caretPos);
        const tokens = tokenize(slice);
        const meaningful = tokens.filter(t => t.type !== 'ws');
        const last = meaningful[meaningful.length - 1];
        const prev = meaningful[meaningful.length - 2];

        // Determine current partial prefix & where it starts
        let prefix = '';
        let insertFrom = caretPos;
        const lastTok = tokens[tokens.length - 1];
        if (lastTok && lastTok.type !== 'ws' && lastTok.end === caretPos) {
            prefix = lastTok.value;
            insertFrom = lastTok.start;
        }

        function filtered(list) {
            if (!prefix) return list;
            const lp = prefix.toLowerCase();
            return list.filter(s => s.toLowerCase().startsWith(lp));
        }

        // Nothing typed yet / after AND → suggest fields
        if (!last || last.type === 'kw') {
            return { suggestions: filtered(SUGGEST_FIELDS), insertFrom };
        }

        // Partial unknown token where a field is expected (at start or after AND/kw)
        if (last.type === 'unknown' && (!prev || prev.type === 'kw')) {
            return { suggestions: filtered(SUGGEST_FIELDS), insertFrom };
        }

        // After a field → suggest operators
        if (last.type === 'field') {
            const ops = LEVEL_FIELDS.has(last.fname) ? LEVEL_OPS : DEFAULT_OPS;
            return { suggestions: filtered(ops), insertFrom };
        }

        // After prop: → suggest operators
        if (last.type === 'prop') {
            return { suggestions: filtered(DEFAULT_OPS), insertFrom };
        }

        // Partial unknown where an operator is expected (after field)
        if (last.type === 'unknown' && prev && (prev.type === 'field' || prev.type === 'prop')) {
            const ops = (prev.type === 'field' && LEVEL_FIELDS.has(prev.fname)) ? LEVEL_OPS : DEFAULT_OPS;
            return { suggestions: filtered(ops), insertFrom };
        }

        // After operator — suggest level values if applicable
        if (last.type === 'op') {
            if (prev && prev.type === 'field' && LEVEL_FIELDS.has(prev.fname)) {
                return { suggestions: filtered(LEVEL_VALUES), insertFrom };
            }
            return { suggestions: [], insertFrom };
        }

        // After a value (str, level-value, or unknown after op) → suggest AND
        const afterValue =
            last.type === 'str' ||
            last.type === 'level-value' ||
            (last.type === 'unknown' && prev && prev.type === 'op');
        if (afterValue) {
            return { suggestions: filtered(['AND']), insertFrom };
        }

        return { suggestions: [], insertFrom };
    }

    // ── LvExprEditor ──────────────────────────────────────────────────────────

    class LvExprEditor {
        constructor(input) {
            this._selectedIdx = -1;
            this._suggestions = [];
            this._insertFrom = 0;
            this._build(input);
            if (input.value) this._refreshMirror();
        }

        _build(input) {
            // Wrapper inherits form-control visual styling; input goes borderless inside
            const wrap = document.createElement('div');
            // Copy Bootstrap size class from input
            const smClass = input.classList.contains('form-control-sm') ? ' form-control-sm' : '';
            wrap.className = `lv-expr-wrap form-control${smClass}`;

            const mirror = document.createElement('div');
            mirror.className = 'lv-expr-mirror';
            mirror.setAttribute('aria-hidden', 'true');
            this._mirror = mirror;

            const dropdown = document.createElement('ul');
            dropdown.className = 'lv-expr-dropdown list-unstyled m-0 d-none';
            this._dropdown = dropdown;

            // Strip form-control styling from input; keep name for form submission
            input.className = 'lv-expr-input';
            input.setAttribute('spellcheck', 'false');
            input.setAttribute('autocomplete', 'off');
            input.setAttribute('autocorrect', 'off');
            input.setAttribute('autocapitalize', 'off');
            this._input = input;

            // Insert wrap before input, then move input inside
            input.parentNode.insertBefore(wrap, input);
            wrap.appendChild(mirror);
            wrap.appendChild(input);
            wrap.appendChild(dropdown);

            // Focus ring on wrapper when input is focused
            input.addEventListener('focus', () => wrap.classList.add('lv-expr-focused'));
            input.addEventListener('blur', () => {
                wrap.classList.remove('lv-expr-focused');
                // Delay hide so mousedown on dropdown item fires first
                setTimeout(() => this._hideDropdown(), 150);
            });

            // Prevent blur when clicking inside dropdown
            dropdown.addEventListener('mousedown', e => e.preventDefault());

            input.addEventListener('input', () => { this._refreshMirror(); this._refreshDropdown(); });
            input.addEventListener('keydown', e => this._onKeydown(e));
            // Sync mirror scroll when input scrolls horizontally
            input.addEventListener('scroll', () => this._syncScroll());
            // Update suggestions on cursor movement
            input.addEventListener('click', () => this._refreshDropdown());
            input.addEventListener('keyup', e => {
                if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(e.key)) return;
                this._refreshDropdown();
            });
        }

        _refreshMirror() {
            const tokens = tokenize(this._input.value);
            // Trailing non-breaking space keeps the mirror height stable when empty
            this._mirror.innerHTML = renderHighlight(tokens) + '&nbsp;';
            this._syncScroll();
        }

        _syncScroll() {
            this._mirror.scrollLeft = this._input.scrollLeft;
        }

        _refreshDropdown() {
            const text = this._input.value;
            const pos = this._input.selectionEnd ?? text.length;
            const { suggestions, insertFrom } = getAutocompleteSuggestions(text, pos);
            this._suggestions = suggestions;
            this._insertFrom = insertFrom;
            this._selectedIdx = -1;

            if (!suggestions.length) { this._hideDropdown(); return; }

            this._dropdown.innerHTML = '';
            suggestions.forEach(s => {
                const li = document.createElement('li');
                li.className = 'lv-expr-item';
                li.textContent = s;
                // Use click (not mousedown) — dropdown.mousedown already preventDefault'd blur
                li.addEventListener('click', () => this._complete(s));
                this._dropdown.appendChild(li);
            });
            this._dropdown.classList.remove('d-none');
        }

        _hideDropdown() {
            this._dropdown.classList.add('d-none');
            this._selectedIdx = -1;
        }

        _setSelected(idx) {
            const items = this._dropdown.querySelectorAll('.lv-expr-item');
            items.forEach((li, i) => li.classList.toggle('lv-expr-item-active', i === idx));
            this._selectedIdx = idx;
        }

        _complete(suggestion) {
            const text   = this._input.value;
            const before = text.slice(0, this._insertFrom);
            const after  = text.slice(this._input.selectionEnd ?? text.length);
            const trail  = suggestion.endsWith(':') ? '' : ' ';
            const newText  = before + suggestion + trail + after;
            const newCaret = before.length + suggestion.length + trail.length;

            this._input.value = newText;
            this._input.setSelectionRange(newCaret, newCaret);
            this._input.focus();
            this._refreshMirror();
            this._refreshDropdown();
        }

        _onKeydown(e) {
            const open = !this._dropdown.classList.contains('d-none');

            if (e.key === 'ArrowDown') {
                e.preventDefault();
                if (!open) { this._refreshDropdown(); return; }
                this._setSelected(Math.min(this._selectedIdx + 1, this._suggestions.length - 1));
                return;
            }
            if (e.key === 'ArrowUp') {
                e.preventDefault();
                this._setSelected(Math.max(this._selectedIdx - 1, 0));
                return;
            }
            if (e.key === 'Tab' && open && this._suggestions.length) {
                e.preventDefault();
                this._complete(this._suggestions[this._selectedIdx >= 0 ? this._selectedIdx : 0]);
                return;
            }
            if (e.key === 'Enter') {
                if (open && this._selectedIdx >= 0) {
                    // Complete selected item; don't submit
                    e.preventDefault();
                    this._complete(this._suggestions[this._selectedIdx]);
                } else {
                    // Close dropdown; let the form submit naturally
                    this._hideDropdown();
                }
                return;
            }
            if (e.key === 'Escape' && open) {
                e.preventDefault();
                this._hideDropdown();
                return;
            }
        }
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    function init() {
        document.querySelectorAll('[data-expr-editor]:not([data-expr-editor-init])').forEach(el => {
            el.setAttribute('data-expr-editor-init', '1');
            new LvExprEditor(el);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
