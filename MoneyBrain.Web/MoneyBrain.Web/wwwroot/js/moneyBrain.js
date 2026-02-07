// MoneyBrain JavaScript utilities
window.moneyBrain = {
    // Selects all text in the currently focused element
    // Used as a workaround for MudBlazor 8.15.0 SelectOnClick bug on MudNumericField
    selectOnFocus: function () {
        const element = document.activeElement;
        if (element && element.select) {
            element.select();
        }
    }
};
