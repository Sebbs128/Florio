// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

const popoverTriggerList = document.querySelectorAll('[data-bs-toggle="popover"]');
const popoverList = [...popoverTriggerList].map(popoverTriggerEl => new bootstrap.Popover(popoverTriggerEl, {
    trigger: 'hover',
    html: true
}));

let pendingAutocompleteRequest = null;

function autocompleteSearch(target) {
    let query = $(target).val();
    let resultsTarget = $(target).attr('data-results-target');

    if (query.length >= 1 && ~`^\s*$`.match(query)) {
        if (pendingAutocompleteRequest != null) {
            pendingAutocompleteRequest.abort();
        }
        pendingAutocompleteRequest = $.get(
            '_Autocomplete',
            { Search: query },
            function (data) {
                if (query == $(target).val()) {
                    $(resultsTarget).html(data);
                    $(resultsTarget).show();
                }
            });
    } else {
        $(resultsTarget).html('');
        $(resultsTarget).hide();
    }
}