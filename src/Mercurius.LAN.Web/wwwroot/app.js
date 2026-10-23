// This function will be called from your Blazor component.
function addOutsideClickListener(elementId, dotNetHelper) {
    // Get a reference to the element.
    const element = document.getElementById(elementId);

    // This is the listener function that will be added to the document.
    const listener = (event) => {
        // Check if the clicked element is outside of our component.
        // We check if the element exists and if the click target is not inside it.
        if (element && !element.contains(event.target)) {
            // Invoke the C# method to close the dropdown.
            dotNetHelper.invokeMethodAsync('CloseDropdown');
        }
    };

    // Add the listener to the document.
    document.addEventListener('click', listener);

    // Create a cleanup function to remove the listener later.
    return {
        dispose: () => {
            document.removeEventListener('click', listener);
        }
    };
}

function addNavSearchOutsideClickListener(elementId, dotNetHelper) {
    const listener = (event) => {
        const element = document.getElementById(elementId);
        const eventPath = event.composedPath ? event.composedPath() : [];

        if (!element || element.contains(event.target) || eventPath.includes(element)) {
            return;
        }

        dotNetHelper.invokeMethodAsync('CloseDropdown').catch(() => {});
    };

    document.addEventListener('pointerdown', listener, true);

    return {
        dispose: () => {
            document.removeEventListener('pointerdown', listener, true);
        }
    };
}

function addNavMenuOutsideClickListener(elementId, dotNetHelper) {
    const listener = (event) => {
        const element = document.getElementById(elementId);
        const eventPath = event.composedPath ? event.composedPath() : [];

        if (!element || element.contains(event.target) || eventPath.includes(element)) {
            return;
        }

        dotNetHelper.invokeMethodAsync('CloseAccountDropdowns').catch(() => {});
    };

    document.addEventListener('pointerdown', listener, true);

    return {
        dispose: () => {
            document.removeEventListener('pointerdown', listener, true);
        }
    };
}

function addNavAdminMenuListener(elementId, dotNetHelper) {
    const listener = (event) => {
        const element = document.getElementById(elementId);

        if (event.type === 'keydown' && event.key === 'Escape') {
            event.preventDefault();
            dotNetHelper.invokeMethodAsync('CloseAdminDropdown', true).catch(() => {});
            return;
        }

        const eventPath = event.composedPath ? event.composedPath() : [];
        if (!element || element.contains(event.target) || eventPath.includes(element)) {
            return;
        }

        dotNetHelper.invokeMethodAsync('CloseAdminDropdown', false).catch(() => {});
    };

    document.addEventListener('pointerdown', listener, true);
    document.addEventListener('keydown', listener, true);

    return {
        dispose: () => {
            document.removeEventListener('pointerdown', listener, true);
            document.removeEventListener('keydown', listener, true);
        }
    };
}

function addNavInfoMenuListener(elementId, dotNetHelper) {
    const listener = (event) => {
        const element = document.getElementById(elementId);
        if (event.type === 'keydown') {
            if (event.key !== 'Escape' || !element || element.closest('.brand-nav-shell')?.contains(event.target) || event.target.closest?.('.menu-toggle')) {
                return;
            }

            dotNetHelper.invokeMethodAsync('CloseInfoDropdown', true).catch(() => {});
            return;
        }

        const eventPath = event.composedPath ? event.composedPath() : [];
        if (!element || element.contains(event.target) || eventPath.includes(element)) {
            return;
        }

        dotNetHelper.invokeMethodAsync('CloseInfoDropdown', false).catch(() => {});
    };

    document.addEventListener('click', listener, true);
    document.addEventListener('keydown', listener, true);

    return {
        dispose: () => {
            document.removeEventListener('click', listener, true);
            document.removeEventListener('keydown', listener, true);
        }
    };
}

function activateTeamModalFocusTrap(dialog) {
    if (!dialog) {
        return { dispose: () => {} };
    }

    const previousFocus = document.activeElement;
    const inertedElements = [];
    let current = dialog;

    while (current && current.parentElement) {
        const parent = current.parentElement;
        for (const sibling of parent.children) {
            if (sibling === current || sibling.hasAttribute('inert')) {
                continue;
            }

            sibling.setAttribute('inert', '');
            inertedElements.push(sibling);
        }

        if (parent === document.body) {
            break;
        }

        current = parent;
    }

    const getFocusableElements = () => Array.from(dialog.querySelectorAll(
        'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )).filter(element => element.getClientRects().length > 0 && !element.closest('[inert]'));

    const focusFirst = () => {
        const first = getFocusableElements()[0];
        (first || dialog).focus();
    };

    const handleKeyDown = event => {
        if (event.key !== 'Tab') {
            return;
        }

        const focusableElements = getFocusableElements();
        if (focusableElements.length === 0) {
            event.preventDefault();
            dialog.focus();
            return;
        }

        const first = focusableElements[0];
        const last = focusableElements[focusableElements.length - 1];
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    };

    const handleFocusIn = event => {
        if (!dialog.contains(event.target)) {
            focusFirst();
        }
    };

    dialog.addEventListener('keydown', handleKeyDown);
    document.addEventListener('focusin', handleFocusIn, true);
    requestAnimationFrame(focusFirst);

    return {
        dispose: () => {
            dialog.removeEventListener('keydown', handleKeyDown);
            document.removeEventListener('focusin', handleFocusIn, true);
            inertedElements.forEach(element => element.removeAttribute('inert'));
            if (previousFocus && previousFocus.isConnected && typeof previousFocus.focus === 'function') {
                previousFocus.focus();
            }
        }
    };
}

(() => {
    const key = 'lanParty.imageSelectionDiagnostics';
    if (window.__imageSelectionDiagnosticsInitialized) return;
    window.__imageSelectionDiagnosticsInitialized = true;

    try {
        if (sessionStorage.getItem(key) !== '1') return;
    } catch {
        return;
    }

    const log = (stage, details = {}) => console.debug('[image-selection]', new Date().toISOString(), stage, details);
    const fileSummary = input => Array.from(input.files || [], file => ({ type: file.type, size: file.size }));
    const handleInputEvent = event => {
        const input = event.target;
        if (input?.tagName !== 'INPUT' || input.type !== 'file') return;
        const control = { disabled: input.disabled, connected: input.isConnected };
        if (event.type === 'click') log('native-click', { ...control, selectedCount: input.files?.length || 0 });
        else log(`native-${event.type}`, { ...control, files: fileSummary(input) });
    };

    document.addEventListener('click', handleInputEvent, true);
    document.addEventListener('change', handleInputEvent, true);
    document.addEventListener('cancel', handleInputEvent, true);
    document.addEventListener('visibilitychange', () => log('visibility', { state: document.visibilityState }));
    document.addEventListener('components-reconnect-state-changed', event => log('reconnect-state', { state: event.detail?.state }), true);
    for (const eventName of ['pageshow', 'pagehide', 'freeze', 'resume', 'online', 'offline']) {
        window.addEventListener(eventName, () => log(eventName));
    }

    const selector = '#components-reconnect-modal, #blazor-error-ui, .blazor-error-boundary';
    const lastStates = new Map();
    const reportReconnectState = () => {
        const elements = document.querySelectorAll(selector);
        const state = elements.length
            ? Array.from(elements, element => ({
                selector: element.id ? `#${element.id}` : `.${Array.from(element.classList).join('.')}`,
                className: element.className,
                visible: !element.hidden && element.getAttribute('aria-hidden') !== 'true' && element.getClientRects().length > 0
            }))
            : [];
        const signature = JSON.stringify(state);
        if (lastStates.get(selector) === signature) return;
        lastStates.set(selector, signature);
        log('reconnect-error-dom', { state });
    };

    new MutationObserver(reportReconnectState).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ['aria-hidden', 'class', 'hidden', 'style'],
        childList: true,
        subtree: true
    });
    reportReconnectState();
})();
