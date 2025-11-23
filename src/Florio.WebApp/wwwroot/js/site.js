// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

const popoverTriggerList = document.querySelectorAll('[data-bs-toggle="popover"]');
const popoverList = [...popoverTriggerList].map(popoverTriggerEl => new bootstrap.Popover(popoverTriggerEl, {
    trigger: 'hover',
    html: true
}));

let pendingAbortController = null;

async function autocompleteSearch(target) {
    const query = target.value;
    const resultsTarget = target.dataset.resultsTarget; // maps to data-results-target attribute
    const resultsTargetElement = resultsTarget ? document.querySelector(resultsTarget) : null;

    if (!query || query.trim().length === 0) {
        if (resultsTargetElement) {
            resultsTargetElement.innerHTML = '';
            resultsTargetElement.style.display = 'none';
        }
        return;
    }

    if (pendingAbortController) {
        try {
            pendingAbortController.abort();
        } catch (e) { }
    }

    const controller = new AbortController();
    pendingAbortController = controller;

    try {
        const params = new URLSearchParams({ Search: query });
        const response = await fetch(`/_Autocomplete?${params}`, {
            signal: controller.signal
        });

        if (!response.ok)
            return;

        const data = await response.text();
        if (query === target.value && resultsTargetElement) {
            resultsTargetElement.innerHTML = data;
            resultsTargetElement.style.display = 'block';
        }
        return data;
    } catch (err) {
        if (!err || err.name !== 'AbortError')
            console.error(err);
        return;
    } finally {
        if (pendingAbortController === controller)
            pendingAbortController = null;
    }
}