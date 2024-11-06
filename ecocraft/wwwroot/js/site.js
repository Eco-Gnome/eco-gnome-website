function triggerFileDownload(fileName, base64Content) {
    const link = document.createElement("a");
    link.href = "data:application/octet-stream;base64," + base64Content;
    link.download = fileName;
    link.click();
    link.remove();
}
