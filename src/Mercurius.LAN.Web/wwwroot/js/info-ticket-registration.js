let handler;

export function attach() {
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
            const response = await fetch(`/account/ticket-registration/${encodeURIComponent(link.dataset.ticketKind)}`, {
                credentials: "same-origin",
                cache: "no-store",
                headers: { Accept: "application/json" }
            });
            const result = response.ok ? await response.json() : null;
            const fallback = new URL(link.href);
            const destination = new URL(result?.url || fallback.href);
            const safeDestination = destination.protocol === "https:" &&
                destination.origin === fallback.origin &&
                destination.pathname === fallback.pathname;

            if (!safeDestination) {
                popup.location.replace(fallback.href);
                return;
            }

            if (destination.href !== fallback.href && !result?.proof) {
                popup.location.replace(fallback.href);
                return;
            }

            if (result?.proof) {
                const confirmation = await fetch("/account/ticket-registration/confirm", {
                    method: "POST",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ proof: result.proof }),
                    signal: AbortSignal.timeout(2000)
                });
                if (!confirmation.ok) {
                    popup.location.replace(fallback.href);
                    return;
                }
            }

            popup.location.replace(destination.href);
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
