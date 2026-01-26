// File download utility for CSV export
window.downloadFile = function (fileName, contentBase64) {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = "data:text/csv;base64," + contentBase64;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
