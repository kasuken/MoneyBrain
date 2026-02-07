window.moneyBrain = {
    selectOnFocus: function () {
        const element = document.activeElement;
        if (element && element.select) {
            element.select();
        }
    }
};
