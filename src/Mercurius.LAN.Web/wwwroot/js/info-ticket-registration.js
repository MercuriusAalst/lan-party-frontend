let handler;

export function attach(dotNetReference) {
    handler = async event => {
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)
            return;

        const link = event.target.closest("a.lan-ticket[data-ticket-kind]");
        if (!link)
            return;

        const popup = window.open("about:blank", "_blank");
        if (!popup)
            return;

        event.preventDefault();
        popup.opener = null;

        try {
            const destination = await dotNetReference.invokeMethodAsync("GetTicketRegistrationUrlAsync", link.dataset.ticketKind);
            popup.location.replace(destination || link.href);
        } catch {
            popup.location.replace(link.href);
        }
    };

    document.addEventListener("click", handler);
}

export function detach() {
    if (handler)
        document.removeEventListener("click", handler);
    handler = undefined;
}
