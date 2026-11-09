// Survey progressive enhancement (ado:2566). The survey works without this: the server
// enforces pick limits and re-shows the step with a message. This only disables the
// remaining boxes once the limit is reached, and stops a double submit.
(function () {
    document.querySelectorAll('fieldset[data-max-picks]').forEach(function (set) {
        var max = parseInt(set.getAttribute('data-max-picks'), 10);
        var boxes = set.querySelectorAll('input[type=checkbox]');
        function update() {
            var picked = 0;
            boxes.forEach(function (b) { if (b.checked) picked++; });
            boxes.forEach(function (b) {
                var off = !b.checked && picked >= max;
                b.disabled = off;
                b.closest('.survey-option').classList.toggle('is-disabled', off);
            });
        }
        boxes.forEach(function (b) { b.addEventListener('change', update); });
        update();
    });

    document.querySelectorAll('form.survey-form').forEach(function (form) {
        form.addEventListener('submit', function () {
            var button = form.querySelector('button[type=submit]');
            if (button) setTimeout(function () { button.disabled = true; }, 0);
        });
    });
})();
